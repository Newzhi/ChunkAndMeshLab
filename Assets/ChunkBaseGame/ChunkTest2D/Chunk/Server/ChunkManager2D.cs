using System.Collections.Generic;
using UnityEngine;

public class ChunkManager2D : MonoBehaviour
{
    [Header("Chunk Config Reference")]
    [SerializeField] private ChunkConfig2D chunkConfig;

    [Header("Player Center")]
    [SerializeField] private Transform player;

    private readonly Dictionary<long, ChunkData2D> chunks = new Dictionary<long, ChunkData2D>();
    private ChunkSettings2D settings;

    private ChunkCoord2D lastCenterCoord;
    private bool hasLastCenterCoord;

    private readonly HashSet<long> targetChunkIds = new HashSet<long>();
    private readonly List<long> unloadIds = new List<long>();

    public IReadOnlyDictionary<long, ChunkData2D> Chunks => chunks;

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

    public ChunkData2D LoadChunk(ChunkCoord2D coord)
    {
        long id = coord.Id;
        if (chunks.TryGetValue(id, out ChunkData2D existing))
        {
            existing.SetState(ChunkState2D.Active);
            return existing;
        }

        ChunkData2D chunk = new ChunkData2D(coord, settings, ChunkState2D.Loading);
        chunk.SetState(ChunkState2D.Active);
        chunks.Add(id, chunk);
        ChunkGenerator2D.OnChunkLoaded(chunk, settings, forcedPrefabIndex: -1);
        return chunk;
    }

    public bool UnloadChunk(ChunkCoord2D coord)
    {
        long id = coord.Id;
        if (!chunks.TryGetValue(id, out ChunkData2D chunk))
        {
            return false;
        }

        chunk.SetState(ChunkState2D.Unloaded);
        ChunkGenerator2D.OnChunkUnloading(chunk, settings);
        return chunks.Remove(id);
    }

    // 按玩家在当前 chunk 内的位置决定方向，加载相邻 chunk。
    // 返回 true 表示触发了加载；false 表示玩家在中心死区内（不触发）或参数不合法。
    public bool TryLoadNeighborByRelativePosition(Vector2 playerWorldPos, float centerDeadzoneWorld, out ChunkCoord2D loadedCoord)
    {
        loadedCoord = default;

        int size = settings.Size;
        if (size <= 0)
        {
            return false;
        }

        ChunkCoord2D current = ChunkUtil2D.WorldToChunkCoord(playerWorldPos, size);

        float localX = playerWorldPos.x - current.X * size;
        float localY = playerWorldPos.y - current.Y * size;

        float dx = localX - size * 0.5f;
        float dy = localY - size * 0.5f;

        // 中心死区：在中心附近不触发（适配“启动时吸附到中心”的情况）
        float deadzone = Mathf.Max(0f, centerDeadzoneWorld);
        if (Mathf.Abs(dx) <= deadzone && Mathf.Abs(dy) <= deadzone)
        {
            return false;
        }

        ChunkCoord2D next;
        if (Mathf.Abs(dy) >= Mathf.Abs(dx))
        {
            next = dy >= 0f
                ? new ChunkCoord2D(current.X, current.Y + 1)
                : new ChunkCoord2D(current.X, current.Y - 1);
        }
        else
        {
            next = dx >= 0f
                ? new ChunkCoord2D(current.X + 1, current.Y)
                : new ChunkCoord2D(current.X - 1, current.Y);
        }

        LoadChunk(next);
        loadedCoord = next;

        // 为了直观演示：按 G 加载的 chunk 强制使用 SpawnPrefabs[1]（第二个元素）。
        // 这里直接对刚加载/已存在的 chunk 进行一次“强制重载”以应用 forcedPrefabIndex。
        if (chunks.TryGetValue(next.Id, out ChunkData2D loadedChunk))
        {
            ChunkGenerator2D.OnChunkUnloading(loadedChunk, settings);
            ChunkGenerator2D.OnChunkLoaded(loadedChunk, settings, forcedPrefabIndex: 1);
        }
        return true;
    }

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
            Debug.LogError("[ChunkManager2D] 必须绑定 ChunkConfig2D，当前已禁用。");
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

        // 默认：2D 使用 XY 平面
        Vector2 playerPos2 = new Vector2(player.position.x, player.position.y);
        ChunkCoord2D center = ChunkUtil2D.WorldToChunkCoord(playerPos2, settings.Size);

        bool centerChanged = !hasLastCenterCoord || center.X != lastCenterCoord.X || center.Y != lastCenterCoord.Y;
        if (!force && !centerChanged)
        {
            return;
        }

        if (settings.LogPlayerEnterChunk && centerChanged)
        {
            Debug.Log($"[ChunkManager2D] Player 进入区块 id={center.Id} coord=({center.X}, {center.Y})");
        }

        int radius = settings.MaxRenderDistance;

        targetChunkIds.Clear();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (settings.UseCircularRange && (dx * dx + dy * dy > radius * radius))
                {
                    continue;
                }

                ChunkCoord2D coord = new ChunkCoord2D(center.X + dx, center.Y + dy);
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
            if (chunks.TryGetValue(unloadId, out ChunkData2D chunk))
            {
                UnloadChunk(chunk.Coord);
            }
        }

        lastCenterCoord = center;
        hasLastCenterCoord = true;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!settings.DrawActiveChunkWireframe || chunks.Count == 0)
        {
            return;
        }

        Gizmos.color = settings.ActiveChunkWireColor;
        foreach (ChunkData2D chunk in chunks.Values)
        {
            if (chunk == null || chunk.State != ChunkState2D.Active)
            {
                continue;
            }

            ChunkBounds2D b = chunk.Bounds;
            Vector3 center = new Vector3(
                b.MinX + b.Size * 0.5f,
                b.MinY + b.Size * 0.5f,
                0f);
            Vector3 size = new Vector3(b.Size, b.Size, 0.01f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}

