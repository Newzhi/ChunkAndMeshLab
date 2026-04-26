using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BaseFramework.Async;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 职责：区块对象数据的持久化（TempData 下的 JSON 读写）。
// 目标：把“存储”从生成/实例化逻辑中解耦出来，便于后续切换为二进制/Region 文件/多世界目录等。
public static class ChunkStorager
{
    // 异步保存：把短时间内重复保存合并（chunkId 最后一次覆盖前一次）。
    private static readonly ConcurrentDictionary<long, PendingSave> pendingSaves = new ConcurrentDictionary<long, PendingSave>();
    private static readonly AsyncAutoResetEventLite saveSignal = new AsyncAutoResetEventLite(initialState: false);
    private static int workerStarted;

    public static bool TryLoadChunkObjects(long chunkId, ChunkSettings settings, out ChunkObjectSaveData data)
    {
        data = null;

        if (!settings.EnableChunkObjectDiskCache)
        {
            return false;
        }

        try
        {
            string path = GetChunkObjectFilePath(chunkId, settings);
            if (!File.Exists(path))
            {
                return false;
            }

            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<ChunkObjectSaveData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChunkStorager] 读取 chunk 对象失败 chunkId={chunkId} err={ex}");
            data = null;
            return false;
        }
    }

    public static bool SaveChunkObjects(ChunkObjectSaveData data, ChunkSettings settings)
    {
        if (!settings.EnableChunkObjectDiskCache || data == null)
        {
            return false;
        }

        try
        {
            string dir = GetTempDataAbsolutePath(settings);
            Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(GetChunkObjectFilePath(data.chunkId, settings), json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChunkStorager] 保存 chunk 对象失败 chunkId={data.chunkId} err={ex}");
            return false;
        }
    }

    // 异步保存：仅负责将请求加入队列并唤醒写线程；写盘在后台进行。
    public static void SaveChunkObjectsAsync(ChunkObjectSaveData data, ChunkSettings settings)
    {
        if (!settings.EnableChunkObjectDiskCache || data == null)
        {
            return;
        }

        EnsureWorkerStarted();

        // 注意：这里直接序列化成 JSON，确保后台写入阶段不依赖 Unity API。
        string dir = GetTempDataAbsolutePath(settings);
        string path = GetChunkObjectFilePath(data.chunkId, settings);
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        pendingSaves[data.chunkId] = new PendingSave(dir, path, json, data.chunkId);
        saveSignal.Set();
    }

    private static void EnsureWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref workerStarted, 1, 0) != 0)
        {
            return;
        }

        // fire-and-forget：后台循环永不退出
        SaveWorkerLoop().Forget();
    }

    private static async UniTaskVoid SaveWorkerLoop()
    {
        while (true)
        {
            try
            {
                await saveSignal.WaitAsync();

                // 批量取出当前所有 pending，减少频繁唤醒/抖动。
                // 写盘放到线程池，避免阻塞 PlayerLoop。
                await UniTask.RunOnThreadPool(() =>
                {
                    foreach (var kv in pendingSaves)
                    {
                        long chunkId = kv.Key;
                        if (!pendingSaves.TryRemove(chunkId, out PendingSave save))
                        {
                            continue;
                        }

                        Directory.CreateDirectory(save.Dir);
                        File.WriteAllText(save.Path, save.Json);
                    }
                });
            }
            catch (Exception ex)
            {
                // 后台线程：这里只能记录错误，避免线程退出导致后续保存永久丢失。
                Debug.LogError($"[ChunkStorager] 异步保存线程异常 err={ex}");
            }
        }
    }

    private static string GetTempDataAbsolutePath(ChunkSettings settings)
    {
        return Path.Combine(Application.dataPath, settings.TempDataFolderUnderAssets.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetChunkObjectFilePath(long chunkId, ChunkSettings settings)
    {
        return Path.Combine(GetTempDataAbsolutePath(settings), $"chunk_{chunkId}.json");
    }

    private readonly struct PendingSave
    {
        public readonly string Dir;
        public readonly string Path;
        public readonly string Json;
        public readonly long ChunkId;

        public PendingSave(string dir, string path, string json, long chunkId)
        {
            Dir = dir;
            Path = path;
            Json = json;
            ChunkId = chunkId;
        }
    }
}

