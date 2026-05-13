using System;
using System.Collections.Generic;
using UnityEngine;

// 默认策略：Perlin 高度柱 + prefab 实例；读档/生成/写盘/实例化/卸载回写均在本类完成。
[Serializable]
public sealed class HeightColumnChunkObjectGenerator : IChunkObjectGenerator
{
    public void LoadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager)
    {
        if (chunk == null || storager == null)
        {
            return;
        }

        GameObject[] prefabs = settings.SpawnPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        EnsureChunkObjectRoot(chunk, settings);

        ChunkObjectSaveData data;
        if (!storager.TryLoad(chunk.Id, settings, out data))
        {
            data = BuildHeightColumnSaveData(chunk, settings);
            if (data != null)
            {
                storager.SaveAsync(data, settings);
            }
        }

        chunk.ObjectSaveData = data;
        chunk.SpawnedInstances.Clear();

        if (data == null || data.spawns == null)
        {
            return;
        }

        Vector3 worldOrigin = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
        for (int i = 0; i < data.spawns.Count; i++)
        {
            ChunkSpawnData s = data.spawns[i];
            if ((uint)s.prefabIndex >= (uint)prefabs.Length)
            {
                chunk.SpawnedInstances.Add(null);
                continue;
            }

            GameObject prefab = prefabs[s.prefabIndex];
            if (prefab == null)
            {
                chunk.SpawnedInstances.Add(null);
                continue;
            }

            GameObject go = UnityEngine.Object.Instantiate(prefab, chunk.ObjectRoot);
            go.name = $"{prefab.name} ({chunk.Coord.X},{chunk.Coord.Z})#{i}";
            go.transform.position = worldOrigin + new Vector3(s.x, s.y, s.z);
            go.transform.rotation = Quaternion.identity;

            chunk.SpawnedInstances.Add(go.transform);
        }
    }

    public void UnloadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager)
    {
        if (chunk == null || storager == null)
        {
            return;
        }

        if (chunk.ObjectSaveData != null && chunk.ObjectSaveData.spawns != null)
        {
            int count = Mathf.Min(chunk.ObjectSaveData.spawns.Count, chunk.SpawnedInstances.Count);
            Vector3 worldOrigin = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
            bool anyDirty = false;
            for (int i = 0; i < count; i++)
            {
                Transform t = chunk.SpawnedInstances[i];
                if (t == null)
                {
                    continue;
                }

                ChunkSpawnData s = chunk.ObjectSaveData.spawns[i];
                Vector3 local = t.position - worldOrigin;
                int nx = Mathf.RoundToInt(local.x);
                int ny = Mathf.RoundToInt(local.y);
                int nz = Mathf.RoundToInt(local.z);
                if (s.x != nx || s.y != ny || s.z != nz)
                {
                    s.x = nx;
                    s.y = ny;
                    s.z = nz;
                    anyDirty = true;
                }
            }

            if (anyDirty)
            {
                storager.SaveAsync(chunk.ObjectSaveData, settings);
            }
        }

        chunk.SpawnedInstances.Clear();
        DestroyChunkObjectRoot(chunk);
    }

    private static ChunkObjectSaveData BuildHeightColumnSaveData(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk == null || settings.SpawnPrefabs == null)
        {
            return null;
        }

        int prefabCount = settings.SpawnPrefabs.Length;
        return GenerateHeightColumnPrefabSpawns(chunk, prefabCount);
    }

    private static ChunkObjectSaveData GenerateHeightColumnPrefabSpawns(ChunkData chunk, int prefabCount)
    {
        if (chunk == null)
        {
            return null;
        }

        if (prefabCount <= 0)
        {
            return new ChunkObjectSaveData
            {
                chunkId = chunk.Id,
                spawns = new List<ChunkSpawnData>()
            };
        }

        long seedLong = chunk.Id;
        int seed = unchecked((int)(seedLong ^ (seedLong >> 32)));
        System.Random rng = new System.Random(seed);

        int size = Mathf.Max(1, chunk.Bounds.Size);
        int heightRange = Mathf.Max(1, chunk.Bounds.MaxYInclusive - chunk.Bounds.MinY + 1);
        int maxColumnHeight = Mathf.Min(16, heightRange);

        ChunkObjectSaveData data = new ChunkObjectSaveData
        {
            chunkId = chunk.Id,
            spawns = new List<ChunkSpawnData>(size * size * Mathf.Max(1, maxColumnHeight / 2))
        };

        float smooth = chunk.Settings.NoiseSmoothness;
        int seedX = chunk.Settings.WorldSeed * 1013;
        int seedZ = chunk.Settings.WorldSeed * 1999;

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                int worldX = chunk.Bounds.MinX + x;
                int worldZ = chunk.Bounds.MinZ + z;
                float nx = (worldX + seedX) / smooth;
                float nz = (worldZ + seedZ) / smooth;
                float n = Mathf.PerlinNoise(nx, nz);
                int h = Mathf.Clamp(1 + Mathf.FloorToInt(n * maxColumnHeight), 1, maxColumnHeight);
                int prefabIndex = rng.Next(0, prefabCount);
                for (int y = 0; y < h; y++)
                {
                    data.spawns.Add(new ChunkSpawnData
                    {
                        prefabIndex = prefabIndex,
                        x = x,
                        y = y,
                        z = z
                    });
                }
            }
        }

        return data;
    }

    private static void EnsureChunkObjectRoot(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk.ObjectRoot != null)
        {
            return;
        }

        Transform parent = settings.ChunkObjectParent;
        GameObject go = new GameObject($"ChunkObjects ({chunk.Coord.X}, {chunk.Coord.Z})");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
        chunk.ObjectRoot = go.transform;
    }

    private static void DestroyChunkObjectRoot(ChunkData chunk)
    {
        if (chunk.ObjectRoot == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(chunk.ObjectRoot.gameObject);
        chunk.ObjectRoot = null;
    }
}
