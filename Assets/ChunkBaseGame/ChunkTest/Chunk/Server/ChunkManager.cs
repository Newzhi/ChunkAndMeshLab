using System.Collections.Generic;
using UnityEngine;

// 职责：区块缓存管理（仅保留加载/卸载核心逻辑）。
public class ChunkManager : MonoBehaviour
{
    #region 定义

    #region 定义 — Inspector：配置与引用

    [Header("Chunk Config Reference")]
    // 直接引用 ChunkConfig，统一读取区块基础配置。
    [SerializeField] private ChunkConfig chunkConfig;

    [Header("Player Center")]
    // TODO(Architecture): 当前直接引用玩家 Transform，后续改为订阅玩家位置变化事件。
    [SerializeField] private Transform player;

    #endregion

    #region 定义 — Inspector：加载窗口

    [Header("Load Window")]
    // Window 配置已移动到 ChunkConfig/ChunkSettings（UseCircularRange / MaxRenderDistance）。

    #endregion

    #region 定义 — 运行时：区块缓存与窗口刷新状态

    // 区块缓存：key 为 ChunkId，value 为 ChunkData。
    private readonly Dictionary<long, ChunkData> chunks = new Dictionary<long, ChunkData>();
    // 当前管理器生效的区块基础配置。
    private ChunkSettings settings;
    private ChunkCoord lastCenterCoord;
    private bool hasLastCenterCoord;

    // 复用容器，减少每次刷新分配。
    private readonly HashSet<long> targetChunkIds = new HashSet<long>();
    private readonly List<long> unloadIds = new List<long>();

    // 对外只读暴露缓存，避免外部直接改写字典。
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

    // 加载单个区块：若已存在则复用并设为 Active。
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
        ChunkGenerator.OnChunkLoaded(chunk, settings);
        return chunk;
    }

    // 卸载单个区块：Active/Ready -> Unloaded，再从缓存移除。
    public bool UnloadChunk(ChunkCoord coord)
    {
        long id = coord.Id;
        if (!chunks.TryGetValue(id, out ChunkData chunk))
        {
            return false;
        }

        chunk.SetState(ChunkState.Unloaded);
        ChunkGenerator.OnChunkUnloading(chunk, settings);
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

        // 1) 世界坐标 -> 中心区块坐标
        ChunkCoord center = ChunkUtil.WorldToChunkCoord(playerPos, settings.Size);

        // 仅在跨区块（或强制刷新）时更新窗口，避免每帧全量扫描。
        bool centerChanged = !hasLastCenterCoord || center.X != lastCenterCoord.X || center.Z != lastCenterCoord.Z;
        if (!force && !centerChanged)
        {
            return;
        }

        if (settings.LogPlayerEnterChunk && centerChanged)
        {
            Debug.Log($"[ChunkManager] Player 进入区块 id={center.Id} coord=({center.X}, {center.Z})");
        }

        // 2) 读取渲染半径（你配置里有 MaxRenderDistance）
        int radius = settings.MaxRenderDistance;

        // 3) 生成窗口内目标集合，并加载缺失区块
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

        // 卸载已离开玩家窗口范围的区块。
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

    // Chunk 对象的生成/读写/实例化/保存由 ChunkGenerator 负责，Manager 仅做 Load/Unload 调度。
}
