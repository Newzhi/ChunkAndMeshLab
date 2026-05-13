using System.Collections.Generic;
using UnityEngine;

// 职责：区块生命周期与窗口调度。
// - 调度：窗口、Load/Unload、维护 ChunkData 缓存。
// - 内容：委托 <see cref="IChunkObjectGenerator"/>（内部自行编排 storager、生成、实例化与卸载回写）；本类不实现具体生成逻辑。
public class ChunkManager : MonoBehaviour, IChunkManager
{
    #region 定义

    #region 定义 — Inspector：配置与引用

    [Header("Chunk Config Reference")]
    [SerializeField] private ChunkConfig chunkConfig;

    [Header("Player Center")]
    [SerializeField] private Transform player;

    [Header("Content Strategies (可选覆盖)")]
    [Tooltip("为空则使用默认 HeightColumnChunkObjectGenerator。地形实验选 MeshNoiseTerrainChunkGenerator。")]
    [SerializeReference]
    private IChunkObjectGenerator chunkObjectGenerator;

    [Header("Storage Strategy (可选覆盖)")]
    [Tooltip("为空则使用 JsonChunkObjectStorager。地形高度图实验选 JsonHeightmapTerrainChunkStorager（chunk_*_terrain.json）。")]
    [SerializeReference]
    private IChunkObjectStorager chunkObjectStorager;

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
        EnsureContentStrategies();
        InitPlayerReference();
    }

    private void EnsureContentStrategies()
    {
        if (chunkObjectGenerator is null)
        {
            chunkObjectGenerator = new MeshNoiseTerrainChunkGenerator();
        }

        if (chunkObjectStorager is null)
        {
            chunkObjectStorager = new JsonHeightmapTerrainChunkStorager();
        }
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

    #region IChunkManager

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

    public bool TryGetChunk(long chunkId, out ChunkData chunk) => chunks.TryGetValue(chunkId, out chunk);

    #endregion

    #region 区块加载策略

    private void InitPlayerReference()
    {
        if (player is not null)
        {
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj is not null)
        {
            player = playerObj.transform;
        }
    }

    private void RefreshSettings()
    {
        if (chunkConfig is null)
        {
            Debug.LogError("[ChunkManager] 必须绑定 ChunkConfig，当前已禁用 ChunkManager。");
            enabled = false;
            return;
        }

        settings = chunkConfig.ToSettings();
    }

    private void RefreshChunksAroundPlayer(bool force)
    {
        if (player is null)
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

    #region 区块内容（委托生成器）

    private void LoadChunkContent(ChunkData chunk)
    {
        chunkObjectGenerator.LoadContent(chunk, settings, chunkObjectStorager);
    }

    private void UnloadChunkContent(ChunkData chunk)
    {
        chunkObjectGenerator.UnloadContent(chunk, settings, chunkObjectStorager);
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
            if (chunk is not { State: ChunkState.Active })
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
