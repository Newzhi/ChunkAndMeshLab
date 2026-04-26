using System;
using UnityEngine;

// 职责：Chunk 的体素数据本体（blockId 网格），以 chunk-local 坐标访问。
// 约定：
// - localX/localZ ∈ [0, Size-1]
// - localY ∈ [0, Height-1]，其中 Height = MaxYInclusive - MinY + 1
// - localY = worldY - MinY（与 ChunkBounds/ChunkUtil 约定一致）
public sealed class ChunkVoxelData
{
    public int Size { get; }
    public int MinY { get; }
    public int Height { get; }

    // blockId: 0=Air，其它值由你定义（后续可扩展到 ushort / palette 压缩）。
    public byte[] Blocks { get; }

    public ChunkVoxelData(int size, int minY, int maxYInclusive)
    {
        Size = Mathf.Max(1, size);
        MinY = minY;
        Height = Mathf.Max(1, maxYInclusive - minY + 1);
        Blocks = new byte[Size * Height * Size];
    }

    public int IndexOf(int localX, int localY, int localZ)
    {
        if ((uint)localX >= (uint)Size) throw new ArgumentOutOfRangeException(nameof(localX));
        if ((uint)localZ >= (uint)Size) throw new ArgumentOutOfRangeException(nameof(localZ));
        if ((uint)localY >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(localY));

        // 一维展开：Y-major（先按层，再按行列）
        return (localY * Size + localZ) * Size + localX;
    }

    public byte GetBlock(int localX, int localY, int localZ)
    {
        return Blocks[IndexOf(localX, localY, localZ)];
    }

    public void SetBlock(int localX, int localY, int localZ, byte blockId)
    {
        Blocks[IndexOf(localX, localY, localZ)] = blockId;
    }

    public bool TryWorldToLocal(Vector3 worldPos, ChunkBounds bounds, out Vector3Int local)
    {
        int wx = Mathf.FloorToInt(worldPos.x);
        int wy = Mathf.FloorToInt(worldPos.y);
        int wz = Mathf.FloorToInt(worldPos.z);

        if (!bounds.ContainsWorld(wx, wy, wz))
        {
            local = default;
            return false;
        }

        local = bounds.WorldToLocal(wx, wy, wz);
        return true;
    }
}

[Serializable]
public class ChunkVoxelSaveData
{
    public long chunkId;
    public int size;
    public int minY;
    public int height;
    public byte[] blocks;
}

