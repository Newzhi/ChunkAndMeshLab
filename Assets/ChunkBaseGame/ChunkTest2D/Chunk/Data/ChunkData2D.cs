using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChunkState2D
{
    Unloaded,
    Loading,
    Ready,
    Active,
}

public readonly struct ChunkCoord2D
{
    public int X { get; }
    public int Y { get; }

    public ChunkCoord2D(int x, int y)
    {
        X = x;
        Y = y;
    }

    public long Id => ((long)X << 32) | (uint)Y;
}

public sealed class ChunkBounds2D
{
    public ChunkCoord2D Coord { get; }
    public ChunkSettings2D Settings { get; }
    public int Size => Settings.Size;

    public int MinX => Coord.X * Size;
    public int MinY => Coord.Y * Size;
    public int MaxXExclusive => MinX + Size;
    public int MaxYExclusive => MinY + Size;

    public ChunkBounds2D(ChunkCoord2D coord, ChunkSettings2D settings)
    {
        Coord = coord;
        Settings = settings;
    }

    public bool ContainsWorld(int worldX, int worldY)
    {
        return worldX >= MinX && worldX < MaxXExclusive
            && worldY >= MinY && worldY < MaxYExclusive;
    }

    public Vector2Int WorldToLocal(int worldX, int worldY)
        => ChunkUtil2D.WorldToLocal(worldX, worldY, this);

    public Vector2Int LocalToWorld(int localX, int localY)
        => ChunkUtil2D.LocalToWorld(localX, localY, this);
}

public sealed class ChunkData2D
{
    public long Id => Coord.Id;
    public ChunkCoord2D Coord { get; }
    public ChunkSettings2D Settings { get; }
    public ChunkBounds2D Bounds { get; }
    public ChunkState2D State { get; private set; }

    public Transform ObjectRoot { get; set; }

    public ChunkObjectSaveData2D ObjectSaveData { get; set; }
    public readonly List<Transform> SpawnedInstances = new List<Transform>();

    public ChunkData2D(ChunkCoord2D coord, ChunkSettings2D settings, ChunkState2D state)
    {
        Coord = coord;
        Settings = settings;
        Bounds = new ChunkBounds2D(coord, settings);
        State = state;
    }

    public void SetState(ChunkState2D state) => State = state;
}

[Serializable]
public class ChunkObjectSaveData2D
{
    public long chunkId;
    public List<ChunkSpawnData2D> spawns;
}

[Serializable]
public class ChunkSpawnData2D
{
    public int prefabIndex;
    public int x;
    public int y;
}

