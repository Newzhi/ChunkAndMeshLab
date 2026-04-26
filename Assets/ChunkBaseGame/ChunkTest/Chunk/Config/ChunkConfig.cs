using UnityEngine;

// 职责：区块配置定义文件（配置类/配置结构统一放在这里）。

// 运行时区块配置（用于构造 ChunkData / ChunkBounds）。
public readonly struct ChunkSettings
{
    public int Size { get; }
    public int MinY { get; }
    public int MaxYInclusive { get; }
    // 以玩家为中心的最大区块渲染半径（单位：区块格）。
    public int MaxRenderDistance { get; }
    // true=圆形窗口（按半径），false=方形窗口（按 dx/dz 范围）。
    public bool UseCircularRange { get; }

    // Terrain (Heightmap)
    public int WorldSeed { get; }
    // 平滑度/尺度：越大越平滑（采样坐标除以该值）
    public float NoiseSmoothness { get; }

    // Debug Visualization
    public bool DrawActiveChunkWireframe { get; }
    public Color ActiveChunkWireColor { get; }
    public bool LogPlayerEnterChunk { get; }

    // Chunk Objects (Simple Save/Load)
    public GameObject[] SpawnPrefabs { get; }
    public Transform ChunkObjectParent { get; }
    public bool EnableChunkObjectDiskCache { get; }
    public string TempDataFolderUnderAssets { get; }

    public ChunkSettings(int size, int minY, int maxYInclusive)
        : this(size, minY, maxYInclusive, 3, true, 0, 32f, true, Color.green, true, null, null, true, "ChunkTest/TempData")
    {
    }

    public ChunkSettings(int size, int minY, int maxYInclusive, int maxRenderDistance)
        : this(size, minY, maxYInclusive, maxRenderDistance, true, 0, 32f, true, Color.green, true, null, null, true, "ChunkTest/TempData")
    {
    }

    public ChunkSettings(int size, int minY, int maxYInclusive, int maxRenderDistance, bool useCircularRange)
        : this(size, minY, maxYInclusive, maxRenderDistance, useCircularRange, 0, 32f, true, Color.green, true, null, null, true, "ChunkTest/TempData")
    {
    }

    public ChunkSettings(
        int size,
        int minY,
        int maxYInclusive,
        int maxRenderDistance,
        bool useCircularRange,
        int worldSeed,
        float noiseSmoothness,
        bool drawActiveChunkWireframe,
        Color activeChunkWireColor,
        bool logPlayerEnterChunk,
        GameObject[] spawnPrefabs,
        Transform chunkObjectParent,
        bool enableChunkObjectDiskCache,
        string tempDataFolderUnderAssets)
    {
        Size = size;
        MinY = minY;
        MaxYInclusive = maxYInclusive;
        MaxRenderDistance = maxRenderDistance;
        UseCircularRange = useCircularRange;

        WorldSeed = worldSeed;
        NoiseSmoothness = Mathf.Max(0.0001f, noiseSmoothness);

        DrawActiveChunkWireframe = drawActiveChunkWireframe;
        ActiveChunkWireColor = activeChunkWireColor;
        LogPlayerEnterChunk = logPlayerEnterChunk;

        SpawnPrefabs = spawnPrefabs;
        ChunkObjectParent = chunkObjectParent;
        EnableChunkObjectDiskCache = enableChunkObjectDiskCache;
        TempDataFolderUnderAssets = tempDataFolderUnderAssets ?? "ChunkTest/TempData";
    }
}

// Inspector 配置容器：可挂在场景对象上，统一配置区块参数。
public class ChunkConfig : MonoBehaviour
{
    [Header("Chunk Basic")]
    [SerializeField] private int size = 16;
    [SerializeField] private int minY = 0;
    [SerializeField] private int maxYInclusive = 255;

    [Header("Terrain (Heightmap)")]
    [SerializeField] private int worldSeed = 0;
    [SerializeField] private float noiseSmoothness = 32f;

    [Header("Load Window")]
    [SerializeField] private int maxRenderDistance = 3;
    [SerializeField] private bool useCircularRange = true;

    [Header("Chunk Objects (Simple Save/Load)")]
    [SerializeField] private GameObject[] spawnPrefabs;
    [SerializeField] private Transform chunkObjectParent;
    [SerializeField] private bool enableChunkObjectDiskCache = true;
    [SerializeField] private string tempDataFolderUnderAssets = "ChunkTest/TempData";

    [Header("Debug Visualization")]
    [SerializeField] private bool drawActiveChunkWireframe = true;
    [SerializeField] private Color activeChunkWireColor = Color.green;
    [SerializeField] private bool logPlayerEnterChunk = true;

    public ChunkSettings ToSettings()
    {
        int normalizedSize = Mathf.Max(1, size);
        int normalizedMaxY = Mathf.Max(minY, maxYInclusive);
        int normalizedRenderDistance = Mathf.Max(0, maxRenderDistance);

        // 配置层兜底：确保对象父节点存在，避免运行时逻辑偷偷创建多个根节点。
        if (chunkObjectParent == null)
        {
            GameObject parent = new GameObject("[ChunkTest] ChunkObjects");
            chunkObjectParent = parent.transform;
        }

        return new ChunkSettings(
            normalizedSize,
            minY,
            normalizedMaxY,
            normalizedRenderDistance,
            useCircularRange,
            worldSeed,
            noiseSmoothness,
            drawActiveChunkWireframe,
            activeChunkWireColor,
            logPlayerEnterChunk,
            spawnPrefabs,
            chunkObjectParent,
            enableChunkObjectDiskCache,
            tempDataFolderUnderAssets);
    }
}

