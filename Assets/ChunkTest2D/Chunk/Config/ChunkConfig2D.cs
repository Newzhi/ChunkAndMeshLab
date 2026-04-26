using UnityEngine;

public readonly struct ChunkSettings2D
{
    public int Size { get; }
    public int MaxRenderDistance { get; }
    public bool UseCircularRange { get; }

    public bool DrawActiveChunkWireframe { get; }
    public Color ActiveChunkWireColor { get; }
    public bool LogPlayerEnterChunk { get; }

    public GameObject[] SpawnPrefabs { get; }
    public Transform ChunkObjectParent { get; }
    public bool EnableChunkObjectDiskCache { get; }
    public string TempDataFolderUnderAssets { get; }

    public ChunkSettings2D(
        int size,
        int maxRenderDistance,
        bool useCircularRange,
        bool drawActiveChunkWireframe,
        Color activeChunkWireColor,
        bool logPlayerEnterChunk,
        GameObject[] spawnPrefabs,
        Transform chunkObjectParent,
        bool enableChunkObjectDiskCache,
        string tempDataFolderUnderAssets)
    {
        Size = Mathf.Max(1, size);
        MaxRenderDistance = Mathf.Max(0, maxRenderDistance);
        UseCircularRange = useCircularRange;

        DrawActiveChunkWireframe = drawActiveChunkWireframe;
        ActiveChunkWireColor = activeChunkWireColor;
        LogPlayerEnterChunk = logPlayerEnterChunk;

        SpawnPrefabs = spawnPrefabs;
        ChunkObjectParent = chunkObjectParent;
        EnableChunkObjectDiskCache = enableChunkObjectDiskCache;
        TempDataFolderUnderAssets = string.IsNullOrEmpty(tempDataFolderUnderAssets)
            ? "ChunkTest2D/TempDataFolder"
            : tempDataFolderUnderAssets;
    }
}

public class ChunkConfig2D : MonoBehaviour
{
    [Header("Chunk Basic (2D)")]
    [SerializeField] private int size = 16;

    [Header("Load Window")]
    [SerializeField] private int maxRenderDistance = 3;
    [SerializeField] private bool useCircularRange = true;

    [Header("Chunk Objects (Simple Save/Load)")]
    [SerializeField] private GameObject[] spawnPrefabs;
    [SerializeField] private Transform chunkObjectParent;
    [SerializeField] private bool enableChunkObjectDiskCache = true;
    [SerializeField] private string tempDataFolderUnderAssets = "ChunkTest2D/TempDataFolder";

    [Header("Debug Visualization")]
    [SerializeField] private bool drawActiveChunkWireframe = true;
    [SerializeField] private Color activeChunkWireColor = Color.green;
    [SerializeField] private bool logPlayerEnterChunk = true;

    public ChunkSettings2D ToSettings()
    {
        if (chunkObjectParent == null)
        {
            GameObject parent = new GameObject("[ChunkTest2D] ChunkObjects");
            chunkObjectParent = parent.transform;
        }

        return new ChunkSettings2D(
            size,
            maxRenderDistance,
            useCircularRange,
            drawActiveChunkWireframe,
            activeChunkWireColor,
            logPlayerEnterChunk,
            spawnPrefabs,
            chunkObjectParent,
            enableChunkObjectDiskCache,
            tempDataFolderUnderAssets);
    }
}

