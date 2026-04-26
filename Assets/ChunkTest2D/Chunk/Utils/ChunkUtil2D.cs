using UnityEngine;

public static class ChunkUtil2D
{
    public static Vector2Int WorldToLocal(int worldX, int worldY, ChunkBounds2D bounds)
        => new Vector2Int(worldX - bounds.MinX, worldY - bounds.MinY);

    public static Vector2Int LocalToWorld(int localX, int localY, ChunkBounds2D bounds)
        => new Vector2Int(bounds.MinX + localX, bounds.MinY + localY);

    public static ChunkCoord2D WorldToChunkCoord(Vector2 worldPosition, int chunkSize)
    {
        int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
        int chunkY = Mathf.FloorToInt(worldPosition.y / chunkSize);
        return new ChunkCoord2D(chunkX, chunkY);
    }
}

