using System;
using System.IO;
using UnityEngine;

public static class ChunkStorager2D
{
    public static bool TryLoadChunkObjects(long chunkId, ChunkSettings2D settings, out ChunkObjectSaveData2D data)
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
            data = JsonUtility.FromJson<ChunkObjectSaveData2D>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChunkStorager2D] 读取失败 chunkId={chunkId} err={ex}");
            data = null;
            return false;
        }
    }

    public static bool SaveChunkObjects(ChunkObjectSaveData2D data, ChunkSettings2D settings)
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
            Debug.LogError($"[ChunkStorager2D] 保存失败 chunkId={data.chunkId} err={ex}");
            return false;
        }
    }

    private static string GetTempDataAbsolutePath(ChunkSettings2D settings)
    {
        return Path.Combine(
            Application.dataPath,
            settings.TempDataFolderUnderAssets.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetChunkObjectFilePath(long chunkId, ChunkSettings2D settings)
    {
        return Path.Combine(GetTempDataAbsolutePath(settings), $"chunk_{chunkId}.json");
    }
}

