using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BaseFramework.Async;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 实验：与默认 JSON 分文件（chunk_*_terrain.json），避免与 prefab 柱体存档互相覆盖；异步队列独立。
[Serializable]
public sealed class JsonHeightmapTerrainChunkStorager : IChunkObjectStorager
{
    private static readonly ConcurrentDictionary<long, PendingSave> PendingSaves = new ConcurrentDictionary<long, PendingSave>();
    private static readonly AsyncAutoResetEventLite SaveSignal = new AsyncAutoResetEventLite(initialState: false);
    private static int workerStarted;

    public bool TryLoad(long chunkId, ChunkSettings settings, out ChunkObjectSaveData data)
    {
        data = null;

        if (!settings.EnableChunkObjectDiskCache)
        {
            return false;
        }

        try
        {
            string path = GetTerrainFilePath(chunkId, settings);
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
            Debug.LogError($"[JsonHeightmapTerrainChunkStorager] 读取失败 chunkId={chunkId} err={ex}");
            data = null;
            return false;
        }
    }

    public bool Save(ChunkObjectSaveData data, ChunkSettings settings)
    {
        if (!settings.EnableChunkObjectDiskCache || data is null)
        {
            return false;
        }

        try
        {
            string dir = GetTempDataAbsolutePath(settings);
            Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(GetTerrainFilePath(data.chunkId, settings), json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JsonHeightmapTerrainChunkStorager] 保存失败 chunkId={data.chunkId} err={ex}");
            return false;
        }
    }

    public void SaveAsync(ChunkObjectSaveData data, ChunkSettings settings)
    {
        if (!settings.EnableChunkObjectDiskCache || data is null)
        {
            return;
        }

        EnsureWorkerStarted();

        string dir = GetTempDataAbsolutePath(settings);
        string path = GetTerrainFilePath(data.chunkId, settings);
        string json = JsonUtility.ToJson(data, prettyPrint: true);

        PendingSaves[data.chunkId] = new PendingSave(dir, path, json, data.chunkId);
        SaveSignal.Set();
    }

    private static void EnsureWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref workerStarted, 1, 0) != 0)
        {
            return;
        }

        SaveWorkerLoop().Forget();
    }

    private static async UniTaskVoid SaveWorkerLoop()
    {
        while (true)
        {
            try
            {
                await SaveSignal.WaitAsync();

                await UniTask.RunOnThreadPool(() =>
                {
                    foreach (var kv in PendingSaves)
                    {
                        long chunkId = kv.Key;
                        if (!PendingSaves.TryRemove(chunkId, out PendingSave save))
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
                Debug.LogError($"[JsonHeightmapTerrainChunkStorager] 异步保存异常 err={ex}");
            }
        }
    }

    private static string GetTempDataAbsolutePath(ChunkSettings settings)
    {
        return Path.Combine(Application.dataPath, settings.TempDataFolderUnderAssets.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetTerrainFilePath(long chunkId, ChunkSettings settings)
    {
        return Path.Combine(GetTempDataAbsolutePath(settings), $"chunk_{chunkId}_terrain.json");
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
