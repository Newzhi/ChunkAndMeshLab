using System;
using System.Collections.Generic;
using UnityEngine;

// 职责：仅负责「区块里要生成哪些内容、怎么生成、生成在哪」。
// - 输入：区块身份与边界（ChunkData）、配置（ChunkSettings）。
// - 输出：可序列化的 ChunkObjectSaveData（prefabIndex + chunk-local 格点 x,y,z）。
// 不做：读档/写盘、Instantiate、场景根节点、运行时回写；这些由 ChunkManager 负责。
public static class ChunkGenerator
{
    /// <summary>
    /// 无存档时由 ChunkManager 调用。在此处分支不同生成策略（高度柱 / 体素 / 表驱动等）。
    /// </summary>
    public static ChunkObjectSaveData GenerateChunkObjectSaveData(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk == null || settings.SpawnPrefabs == null)
        {
            return null;
        }

        int prefabCount = settings.SpawnPrefabs.Length;
        return GenerateHeightColumnPrefabSpawns(chunk, prefabCount);
    }

    /// <summary>原型：Perlin 高度图 + 每列竖条 prefab 占位；坐标为 chunk-local 整数格点。</summary>
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
}
