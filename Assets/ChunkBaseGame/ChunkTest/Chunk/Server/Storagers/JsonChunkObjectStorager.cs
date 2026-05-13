using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BaseFramework.Async;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 默认存储策略：TempData 下 JSON 文件 + 异步合并写入。
// 通过实现 <see cref="IChunkObjectStorager"/> 供 ChunkManager 组合注入；换 Region/二进制时新增类型即可。
[Serializable]
public sealed class JsonChunkObjectStorager : IChunkObjectStorager
{
    private readonly ConcurrentDictionary<long, PendingSave> pendingSaves = new ConcurrentDictionary<long, PendingSave>();
    private readonly AsyncAutoResetEventLite saveSignal = new AsyncAutoResetEventLite(initialState: false);
    private int workerStarted;

    public bool TryLoad(long chunkId, ChunkSettings settings, out ChunkObjectSaveData data)
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
            Debug.LogError($"[JsonChunkObjectStorager] 读取 chunk 对象失败 chunkId={chunkId} err={ex}");
            data = null;
            return false;
        }
    }

    public bool Save(ChunkObjectSaveData data, ChunkSettings settings)
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
            Debug.LogError($"[JsonChunkObjectStorager] 保存 chunk 对象失败 chunkId={data.chunkId} err={ex}");
            return false;
        }
    }

    public void SaveAsync(ChunkObjectSaveData data, ChunkSettings settings)
    {
        if (!settings.EnableChunkObjectDiskCache || data == null)
        {
            return;
        }

        EnsureWorkerStarted();

        string dir = GetTempDataAbsolutePath(settings);
        string path = GetChunkObjectFilePath(data.chunkId, settings);
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        pendingSaves[data.chunkId] = new PendingSave(dir, path, json, data.chunkId);
        saveSignal.Set();
    }

    private void EnsureWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref workerStarted, 1, 0) != 0)
        {
            return;
        }

        SaveWorkerLoop().Forget();
    }

    private async UniTaskVoid SaveWorkerLoop()
    {
        while (true)
        {
            try
            {
                await saveSignal.WaitAsync();

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
                Debug.LogError($"[JsonChunkObjectStorager] 异步保存线程异常 err={ex}");
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
