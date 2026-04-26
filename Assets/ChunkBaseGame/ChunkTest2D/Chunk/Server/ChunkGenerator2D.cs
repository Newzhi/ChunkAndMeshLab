using System.Collections.Generic;
using UnityEngine;

public static class ChunkGenerator2D
{
    // forcedPrefabIndex: -1 表示不强制（使用存档中的 prefabIndex 或默认 0）
    public static void OnChunkLoaded(ChunkData2D chunk, ChunkSettings2D settings, int forcedPrefabIndex)
    {
        if (chunk == null)
        {
            return;
        }

        GameObject[] prefabs = settings.SpawnPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        EnsureChunkObjectRoot(chunk, settings);

        if (!ChunkStorager2D.TryLoadChunkObjects(chunk.Id, settings, out ChunkObjectSaveData2D data))
        {
            data = GenerateData(chunk);
            ChunkStorager2D.SaveChunkObjects(data, settings);
        }

        chunk.ObjectSaveData = data;
        chunk.SpawnedInstances.Clear();

        if (data.spawns == null)
        {
            return;
        }
        
        if (data.spawns.Count == 0)
        {
            int centerLocal = chunk.Bounds.Size / 2;
            data.spawns.Add(new ChunkSpawnData2D { prefabIndex = 0, x = centerLocal, y = centerLocal });
            ChunkStorager2D.SaveChunkObjects(data, settings);
        }
        else if (data.spawns.Count > 1)
        {
            data.spawns.RemoveRange(1, data.spawns.Count - 1);
            ChunkStorager2D.SaveChunkObjects(data, settings);
        }

        // 若指定强制 prefabIndex，则覆盖存档并保存（方便“按 G 显示第二个 prefab”的直观效果）
        if (forcedPrefabIndex >= 0)
        {
            int clamped = Mathf.Clamp(forcedPrefabIndex, 0, prefabs.Length - 1);
            if (data.spawns[0].prefabIndex != clamped)
            {
                data.spawns[0].prefabIndex = clamped;
                ChunkStorager2D.SaveChunkObjects(data, settings);
            }
        }

        Vector3 worldOrigin3 = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, 0f);
        ChunkSpawnData2D s0 = data.spawns[0];
        if ((uint)s0.prefabIndex >= (uint)prefabs.Length)
        {
            return;
        }

        GameObject prefab0 = prefabs[s0.prefabIndex];
        if (prefab0 == null)
        {
            return;
        }

        GameObject go0 = UnityEngine.Object.Instantiate(prefab0, chunk.ObjectRoot);
        go0.name = $"{prefab0.name} ({chunk.Coord.X},{chunk.Coord.Y})";
        go0.transform.position = worldOrigin3 + new Vector3(s0.x, s0.y, 0f);
        go0.transform.rotation = Quaternion.identity;

        chunk.SpawnedInstances.Add(go0.transform);
    }

    public static void OnChunkUnloading(ChunkData2D chunk, ChunkSettings2D settings)
    {
        if (chunk == null)
        {
            return;
        }

        if (chunk.ObjectSaveData != null && chunk.ObjectSaveData.spawns != null)
        {
            int count = Mathf.Min(chunk.ObjectSaveData.spawns.Count, chunk.SpawnedInstances.Count);
            count = Mathf.Min(count, 1);
            Vector3 worldOrigin3 = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, 0f);

            for (int i = 0; i < count; i++)
            {
                Transform t = chunk.SpawnedInstances[i];
                if (t == null)
                {
                    continue;
                }

                ChunkSpawnData2D s = chunk.ObjectSaveData.spawns[i];
                Vector3 local = t.position - worldOrigin3;
                s.x = Mathf.RoundToInt(local.x);
                s.y = Mathf.RoundToInt(local.y);
            }

            ChunkStorager2D.SaveChunkObjects(chunk.ObjectSaveData, settings);
        }

        chunk.SpawnedInstances.Clear();
        DestroyChunkObjectRoot(chunk);
    }

    private static ChunkObjectSaveData2D GenerateData(ChunkData2D chunk)
    {
        ChunkObjectSaveData2D data = new ChunkObjectSaveData2D
        {
            chunkId = chunk.Id,
            spawns = new List<ChunkSpawnData2D>(capacity: 1)
        };

        // 2D 原型：不做随机/噪声生成。
        // 约定：每个 chunk 默认只生成 1 个对象（例如“背景图 prefab”），放在 chunk-local 的中心点。
        int centerLocal = chunk.Bounds.Size / 2;
        data.spawns.Add(new ChunkSpawnData2D
        {
            prefabIndex = 0,
            x = centerLocal,
            y = centerLocal
        });

        return data;
    }

    private static void EnsureChunkObjectRoot(ChunkData2D chunk, ChunkSettings2D settings)
    {
        if (chunk.ObjectRoot != null)
        {
            return;
        }

        Transform parent = settings.ChunkObjectParent;
        GameObject go = new GameObject($"ChunkObjects2D ({chunk.Coord.X}, {chunk.Coord.Y})");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, 0f);
        chunk.ObjectRoot = go.transform;
    }

    private static void DestroyChunkObjectRoot(ChunkData2D chunk)
    {
        if (chunk.ObjectRoot == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(chunk.ObjectRoot.gameObject);
        chunk.ObjectRoot = null;
    }
}

