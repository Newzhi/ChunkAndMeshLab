using System.Collections.Generic;
using UnityEngine;

// 职责：区块生命周期与内容管线调度。
// - 调度：窗口、Load/Unload、维护 ChunkData 缓存。
// - 内容：读档 → 无则调用 ChunkGenerator 生成数据 → 写盘；实例化/销毁；卸载时把 Transform 写回 DTO 再按需保存。
// ChunkGenerator 只负责「生成什么、怎么生成、生成在哪」（产出 ChunkObjectSaveData），不碰 IO 与场景对象。
public class ChunkManager : MonoBehaviour
{
    #region 定义

    #region 定义 — Inspector：配置与引用

    [Header("Chunk Config Reference")]
    [SerializeField] private ChunkConfig chunkConfig;

    [Header("Player Center")]
    [SerializeField] private Transform player;

    #endregion

    #region 定义 — Inspector：加载窗口

    [Header("Load Window")]
    // Window：ChunkConfig / ChunkSettings（UseCircularRange / MaxRenderDistance）。

    #endregion

    #region 定义 — 运行时：区块缓存与窗口刷新状态

    private readonly Dictionary<long, ChunkData> chunks = new Dictionary<long, ChunkData>();
    private ChunkSettings settings;
    private ChunkCoord lastCenterCoord;
    private bool hasLastCenterCoord;

    private readonly HashSet<long> targetChunkIds = new HashSet<long>();
    private readonly List<long> unloadIds = new List<long>();

    public IReadOnlyDictionary<long, ChunkData> Chunks => chunks;

    #endregion

    #endregion

    #region 生命周期

    private void Awake()
    {
        RefreshSettings();
        InitPlayerReference();
    }

    private void Start()
    {
        RefreshChunksAroundPlayer(force: true);
    }

    private void LateUpdate()
    {
        RefreshChunksAroundPlayer(force: false);
    }

    #endregion

    #region 基本操作

    public ChunkData LoadChunk(ChunkCoord coord)
    {
        long id = coord.Id;
        if (chunks.TryGetValue(id, out ChunkData existing))
        {
            existing.SetState(ChunkState.Active);
            return existing;
        }

        ChunkData chunk = new ChunkData(coord, settings, ChunkState.Loading);
        chunk.SetState(ChunkState.Active);
        chunks.Add(id, chunk);
        LoadChunkContent(chunk);
        return chunk;
    }

    public bool UnloadChunk(ChunkCoord coord)
    {
        long id = coord.Id;
        if (!chunks.TryGetValue(id, out ChunkData chunk))
        {
            return false;
        }

        chunk.SetState(ChunkState.Unloaded);
        UnloadChunkContent(chunk);
        return chunks.Remove(id);
    }

    #endregion

    #region 根据玩家位置加载区块

    private void InitPlayerReference()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    private void RefreshSettings()
    {
        if (chunkConfig == null)
        {
            Debug.LogError("[ChunkManager] 必须绑定 ChunkConfig，当前已禁用 ChunkManager。");
            enabled = false;
            return;
        }

        settings = chunkConfig.ToSettings();
    }

    private void RefreshChunksAroundPlayer(bool force)
    {
        if (player == null)
        {
            return;
        }

        Vector3 playerPos = player.position;
        ChunkCoord center = ChunkUtil.WorldToChunkCoord(playerPos, settings.Size);

        bool centerChanged = !hasLastCenterCoord || center.X != lastCenterCoord.X || center.Z != lastCenterCoord.Z;
        if (!force && !centerChanged)
        {
            return;
        }

        if (settings.LogPlayerEnterChunk && centerChanged)
        {
            Debug.Log($"[ChunkManager] Player 进入区块 id={center.Id} coord=({center.X}, {center.Z})");
        }

        int radius = settings.MaxRenderDistance;

        targetChunkIds.Clear();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (settings.UseCircularRange && (dx * dx + dz * dz > radius * radius))
                {
                    continue;
                }

                ChunkCoord coord = new ChunkCoord(center.X + dx, center.Z + dz);
                targetChunkIds.Add(coord.Id);
                LoadChunk(coord);
            }
        }

        unloadIds.Clear();
        foreach (long loadedId in chunks.Keys)
        {
            if (!targetChunkIds.Contains(loadedId))
            {
                unloadIds.Add(loadedId);
            }
        }

        foreach (long unloadId in unloadIds)
        {
            if (chunks.TryGetValue(unloadId, out ChunkData chunk))
            {
                UnloadChunk(chunk.Coord);
            }
        }

        lastCenterCoord = center;
        hasLastCenterCoord = true;
    }

    #endregion

    #region 区块内容（读档 · 生成 · 写盘 · 实例化 · 回写）

    private void LoadChunkContent(ChunkData chunk)
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

        EnsureChunkObjectRoot(chunk);//确保挂载根节点正确以及设置根节点

        ChunkObjectSaveData data;
        if (!ChunkStorager.TryLoadChunkObjects(chunk.Id, settings, out data))
        {
            data = ChunkGenerator.GenerateChunkObjectSaveData(chunk, settings);
            if (data != null)
            {
                ChunkStorager.SaveChunkObjectsAsync(data, settings);
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

            GameObject go = Object.Instantiate(prefab, chunk.ObjectRoot);
            go.name = $"{prefab.name} ({chunk.Coord.X},{chunk.Coord.Z})#{i}";
            go.transform.position = worldOrigin + new Vector3(s.x, s.y, s.z);
            go.transform.rotation = Quaternion.identity;

            chunk.SpawnedInstances.Add(go.transform);
        }
    }

    private void UnloadChunkContent(ChunkData chunk)
    {
        if (chunk == null)
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
                ChunkStorager.SaveChunkObjectsAsync(chunk.ObjectSaveData, settings);
            }
        }

        chunk.SpawnedInstances.Clear();
        DestroyChunkObjectRoot(chunk);
    }

    private void EnsureChunkObjectRoot(ChunkData chunk)
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

        Object.Destroy(chunk.ObjectRoot.gameObject);
        chunk.ObjectRoot = null;
    }

    #endregion

    #region 调试方法

    private void OnDrawGizmos()
    {
        if (!settings.DrawActiveChunkWireframe || chunks.Count == 0)
        {
            return;
        }

        Gizmos.color = settings.ActiveChunkWireColor;
        foreach (ChunkData chunk in chunks.Values)
        {
            if (chunk.State != ChunkState.Active)
            {
                continue;
            }

            ChunkBounds bounds = chunk.Bounds;
            float sizeY = bounds.MaxYInclusive - bounds.MinY + 1;
            Vector3 center = new Vector3(
                bounds.MinX + bounds.Size * 0.5f,
                bounds.MinY + sizeY * 0.5f,
                bounds.MinZ + bounds.Size * 0.5f);
            Vector3 size = new Vector3(bounds.Size, sizeY, bounds.Size);

            Gizmos.DrawWireCube(center, size);
        }
    }

    #endregion
}
