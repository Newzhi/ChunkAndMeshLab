using UnityEngine;

public class PlayerChunkLoadInput2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ChunkManager2D chunkManager;

    [Header("Input")]
    [SerializeField] private KeyCode loadKey = KeyCode.G;

    [Header("Behavior")]
    [SerializeField] private float centerDeadzoneWorld = 0.05f;
    [SerializeField] private bool logOnLoad = true;

    private void Awake()
    {
        if (chunkManager == null)
        {
            chunkManager = FindFirstObjectByType<ChunkManager2D>();
        }
    }

    private void Update()
    {
        if (chunkManager == null)
        {
            return;
        }

        if (!Input.GetKeyDown(loadKey))
        {
            return;
        }

        Vector2 playerPos2 = new Vector2(transform.position.x, transform.position.y);
        if (chunkManager.TryLoadNeighborByRelativePosition(playerPos2, centerDeadzoneWorld, out ChunkCoord2D loaded))
        {
            if (logOnLoad)
            {
                Debug.Log($"[PlayerChunkLoadInput2D] Load neighbor chunk ({loaded.X}, {loaded.Y})");
            }
        }
    }
}

