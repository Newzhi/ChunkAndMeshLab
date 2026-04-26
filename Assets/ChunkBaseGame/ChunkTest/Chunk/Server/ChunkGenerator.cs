using System;
using System.Collections.Generic;
using UnityEngine;

// 职责：根据 chunk 身份与边界，生成一份可序列化的“区块对象数据”。
// 最小原型目标：
// - ChunkManager 只负责区块加载/卸载调度
// - 生成/读写/实例化/保存统一放在这里
public static class ChunkGenerator
{
    #region Public API（供 ChunkManager 调用）

    public static void OnChunkLoaded(ChunkData chunk, ChunkSettings settings)
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

        if (!settings.UseGpuInstancingForChunkObjects)
        {
            EnsureChunkObjectRoot(chunk, settings);
        }

        if (!ChunkStorager.TryLoadChunkObjects(chunk.Id, settings, out ChunkObjectSaveData data))
        {
            data = GenerateData(chunk, prefabs.Length);
            // 第一次生成：必定为脏数据，走异步写盘，避免阻塞主线程。
            chunk.MarkObjectSaveDirty();
            ChunkStorager.SaveChunkObjectsAsync(data, settings);
            chunk.ClearObjectSaveDirty();
        }

        chunk.ObjectSaveData = data;
        chunk.SpawnedInstances.Clear();

        if (settings.UseGpuInstancingForChunkObjects)
        {
            chunk.GpuInstanceMatricesByPrefabIndex = BuildGpuInstanceMatrices(chunk, data, prefabs);
            EnsureChunkCollision(chunk, data, settings);
            return;
        }

        if (data.spawns == null)
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
            chunk.OnEnterChunk(go.transform);
        }
    }

    public static void OnChunkUnloading(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk == null)
        {
            return;
        }

        if (!settings.UseGpuInstancingForChunkObjects
            && chunk.ObjectSaveData != null
            && chunk.ObjectSaveData.spawns != null)
        {
            // 回写：把运行时 transform 的位置/旋转写回 chunk-local 数据（若实例数量对齐）。
            int count = Mathf.Min(chunk.ObjectSaveData.spawns.Count, chunk.SpawnedInstances.Count);
            Vector3 worldOrigin = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
            for (int i = 0; i < count; i++)
            {
                Transform t = chunk.SpawnedInstances[i];
                if (t == null)
                {
                    continue;
                }

                ChunkSpawnData s = chunk.ObjectSaveData.spawns[i];
                Vector3 local = t.position - worldOrigin;
                s.x = Mathf.RoundToInt(local.x);
                s.y = Mathf.RoundToInt(local.y);
                s.z = Mathf.RoundToInt(local.z);
                chunk.MarkObjectSaveDirty();
            }

            if (chunk.IsObjectSaveDirty)
            {
                // 卸载：只在脏时写盘，写盘异步化，减少离开窗口时的卡顿尖峰。
                ChunkStorager.SaveChunkObjectsAsync(chunk.ObjectSaveData, settings);
                chunk.ClearObjectSaveDirty();
            }
        }

        chunk.GpuInstanceMatricesByPrefabIndex = null;
        chunk.SpawnedInstances.Clear();
        chunk.DetachAllEntities();

        DestroyChunkCollisionRoot(chunk);
        DestroyChunkObjectRoot(chunk);
    }

    #endregion

    #region GPU Instancing（按 prefab 分组矩阵）

    private static Dictionary<int, List<Matrix4x4>> BuildGpuInstanceMatrices(ChunkData chunk, ChunkObjectSaveData data, GameObject[] prefabs)
    {
        if (chunk == null || data == null || data.spawns == null || data.spawns.Count == 0)
        {
            return null;
        }

        Vector3 worldOrigin = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
        Dictionary<int, List<Matrix4x4>> dict = new Dictionary<int, List<Matrix4x4>>();

        for (int i = 0; i < data.spawns.Count; i++)
        {
            ChunkSpawnData s = data.spawns[i];
            if ((uint)s.prefabIndex >= (uint)prefabs.Length)
            {
                continue;
            }

            if (prefabs[s.prefabIndex] == null)
            {
                continue;
            }

            if (!dict.TryGetValue(s.prefabIndex, out List<Matrix4x4> list))
            {
                list = new List<Matrix4x4>(256);
                dict[s.prefabIndex] = list;
            }

            Vector3 pos = worldOrigin + new Vector3(s.x, s.y, s.z);
            list.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        }

        return dict.Count == 0 ? null : dict;
    }

    #endregion

    #region Data Generation（原型：含 Y 轴的“高度柱”）

    private static ChunkObjectSaveData GenerateData(ChunkData chunk, int prefabCount)
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

        // 用 chunkId 做 seed，保证“哈希性/稳定性”：同一 chunk 第一次生成可复现。
        long seedLong = chunk.Id;
        int seed = unchecked((int)(seedLong ^ (seedLong >> 32)));
        System.Random rng = new System.Random(seed);

        // 原型验证：把 prefab 当作“方块”，每个方块占据 (x,y,z) 一格。
        // 生成方式：对每个 (x,z) 生成一个高度 h，然后填充 y=0..h-1。
        int size = Mathf.Max(1, chunk.Bounds.Size);
        int heightRange = Mathf.Max(1, chunk.Bounds.MaxYInclusive - chunk.Bounds.MinY + 1);
        // prefab 实验阶段避免生成过多 GameObject，先做一个上限（后续切体素网格再放开）。
        int maxColumnHeight = Mathf.Min(16, heightRange);

        ChunkObjectSaveData data = new ChunkObjectSaveData
        {
            chunkId = chunk.Id,
            spawns = new List<ChunkSpawnData>(size * size * Mathf.Max(1, maxColumnHeight / 2))
        };

        // 使用 PerlinNoise 生成高度图（连续地形）。
        // worldSeed 通过偏移注入，noiseSmoothness 控制采样尺度（越大越平滑）。
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
                float n = Mathf.PerlinNoise(nx, nz); // [0,1]
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

    #endregion

    #region Chunk Collision（GPU 路径：合并 MeshCollider）

    private static void EnsureChunkCollision(ChunkData chunk, ChunkObjectSaveData data, ChunkSettings settings)
    {
        DestroyChunkCollisionRoot(chunk);

        if (data?.spawns == null || data.spawns.Count == 0)
        {
            return;
        }

        var occupied = new HashSet<Vector3Int>();
        for (int i = 0; i < data.spawns.Count; i++)
        {
            ChunkSpawnData s = data.spawns[i];
            occupied.Add(new Vector3Int(s.x, s.y, s.z));
        }

        int sizeXZ = Mathf.Max(1, chunk.Bounds.Size);
        int heightY = Mathf.Max(1, chunk.Bounds.MaxYInclusive - chunk.Bounds.MinY + 1);
        Mesh mesh = ChunkCollisionMeshBuilder.Build(occupied, sizeXZ, heightY);
        if (mesh.vertexCount == 0)
        {
            UnityEngine.Object.Destroy(mesh);
            return;
        }

        Transform parent = settings.ChunkObjectParent;
        GameObject go = new GameObject($"ChunkCollision ({chunk.Coord.X}, {chunk.Coord.Z})");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.SetPositionAndRotation(
            new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ),
            Quaternion.identity);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
        mc.convex = false;

        chunk.ChunkCollisionRoot = go;
    }

    private static void DestroyChunkCollisionRoot(ChunkData chunk)
    {
        if (chunk == null || chunk.ChunkCollisionRoot == null)
        {
            return;
        }

        GameObject go = chunk.ChunkCollisionRoot;
        chunk.ChunkCollisionRoot = null;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        Mesh mesh = mf != null ? mf.sharedMesh : null;
        UnityEngine.Object.Destroy(go);
        if (mesh != null)
        {
            UnityEngine.Object.Destroy(mesh);
        }
    }

    #endregion

    #region Object Root（挂载与销毁）

    private static void EnsureChunkObjectRoot(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk.ObjectRoot != null)
        {
            return;
        }

        Transform parent = GetOrCreateChunkObjectParent(settings);
        GameObject go = new GameObject($"ChunkObjects ({chunk.Coord.X}, {chunk.Coord.Z})");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = new Vector3(chunk.Bounds.MinX, 0f, chunk.Bounds.MinZ);
        chunk.ObjectRoot = go.transform;
    }

    private static Transform GetOrCreateChunkObjectParent(ChunkSettings settings)
    {
        // 约定：由 ChunkConfig/ChunkSettings 保证该引用不为空。
        // Generator 不负责“兜底创建”，避免隐藏行为与生命周期难追踪。
        return settings.ChunkObjectParent;
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

    #endregion
}
