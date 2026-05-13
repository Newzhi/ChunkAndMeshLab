using System;
using System.Collections.Generic;
using UnityEngine;

#region 文件说明

// 设计目标：
// - 哈希性：用位运算得到稳定 ID，作为 Dictionary/HashMap 的 key（O(1) 查找）。
// - 随机性：放到“区块内容生成器”中，不耦合到区块管理数据。
// - 边界定义：由 chunk 坐标 + chunkSize 推导世界坐标边界，并提供常用边界/坐标转换工具。
//
// 结构上将 “坐标/ID（ChunkCoord）” 与 “边界（ChunkBounds）” 分离：
// - 坐标是区块的身份（identity），决定 ID/Hash；边界是派生数据（derived），由坐标+大小计算得出。
// - 这样不会出现“改了边界忘了改 ID”的一致性问题，也更利于后续扩展（不同维度、不同高度范围等）。

#endregion

#region 核心类型（坐标 / 边界 / 状态）

// 职责：描述区块当前生命周期状态（是否已加载、是否正在使用）
public enum ChunkState
{
    // 不在内存缓存中。
    Unloaded,
    // 正在进行加载流程（读档/初始化数据）。
    Loading,
    // 数据已可用，但当前不一定在玩家活跃范围内。
    Ready,
    // 活跃状态（例如在视距内、参与渲染/交互）。
    Active,
}

// 职责：定义区块在网格中的身份坐标，并提供可哈希的唯一 ID。
public readonly struct ChunkCoord
{
    // 区块网格坐标（chunkX, chunkZ），不是世界坐标。
    public int X { get; }
    public int Z { get; }

    public ChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    // 64-bit ID：高 32 位存 X，低 32 位存 Z
    // (chunkX << 32) | chunkZ
    // 这里对 Z 用 uint 视角，避免负数在 OR 时的符号扩展带来歧义。
    public long Id => ((long)X << 32) | (uint)Z;
}

// 职责：根据区块坐标和配置推导边界，提供边界判定与坐标转换工具。
public sealed class ChunkBounds
{
    // 用来推导边界的身份信息：coord + settings
    public ChunkCoord Coord { get; }
    public ChunkSettings Settings { get; }
    public int Size => Settings.Size;
    public int MinY => Settings.MinY;
    public int MaxYInclusive => Settings.MaxYInclusive;

    // X/Z 采用半开区间：[Min, MaxExclusive)
    // - 能把 “是否在边界内” 的判断写成 < MaxExclusive，避免 +1/-1 的 off-by-one。
    // - 更符合数组/体素索引的常见习惯（0..Size-1）。
    public int MinX => Coord.X * Size;
    public int MinZ => Coord.Z * Size;
    public int MaxXExclusive => MinX + Size;
    public int MaxZExclusive => MinZ + Size;

    public ChunkBounds(ChunkCoord coord, ChunkSettings settings)
    {
        Coord = coord;
        Settings = settings;
    }

    public bool ContainsWorld(int worldX, int worldY, int worldZ)
    {
        return worldX >= MinX && worldX < MaxXExclusive
            && worldZ >= MinZ && worldZ < MaxZExclusive
            && worldY >= MinY && worldY <= MaxYInclusive;
    }

    public Vector3Int WorldToLocal(int worldX, int worldY, int worldZ)
    {
        // 将世界坐标转换为区块内局部坐标（通常用于访问体素数组）。
        return ChunkUtil.WorldToLocal(worldX, worldY, worldZ, this);
    }

    public Vector3Int LocalToWorld(int localX, int localY, int localZ)
    {
        // 将区块内局部坐标转换回世界坐标。
        return ChunkUtil.LocalToWorld(localX, localY, localZ, this);
    }
}

#endregion

#region 区块运行时数据（ChunkData）

// 职责：作为区块元数据聚合体，组合 Coord/Settings/Bounds/State，供管理器缓存与调度。
public sealed class ChunkData
{
    #region 定义 — 身份与边界

    // 基本的区块信息定义
    public long Id => Coord.Id;    // (1) 哈希性：ID 完全由坐标位运算得到；用于缓存/加载表的 key。
    public ChunkCoord Coord { get; }
    public ChunkSettings Settings { get; }
    public ChunkBounds Bounds { get; }
    public ChunkState State { get; private set; }

    #endregion

    #region 定义 — 场景根节点（对象挂载）

    // 该区块实例化出来的对象根节点（可挂到外部指定的父节点下）。
    public Transform ObjectRoot { get; set; }

    #endregion

    #region 定义 — 简易存档与生成实例

    // ChunkTest 简易存档：该区块生成/加载出来的“预制体数据”。
    public ChunkObjectSaveData ObjectSaveData { get; set; }

    // 运行时实例列表：索引与 ObjectSaveData.spawns 一一对应（用于卸载时回写位置/旋转）。
    public readonly List<Transform> SpawnedInstances = new List<Transform>();

    #endregion

    #region 构造

    public ChunkData(ChunkCoord coord, ChunkSettings settings, ChunkState state)
    {
        Coord = coord;
        Settings = settings;
        Bounds = new ChunkBounds(coord, settings);
        State = state;
    }

    public void SetState(ChunkState state) => State = state;

    #endregion
}

#endregion

#region 可序列化存档 DTO（区块对象）

[Serializable]
public class ChunkObjectSaveData
{
    public long chunkId;
    public List<ChunkSpawnData> spawns;

    /// <summary>实验：地形高度图，行主序 z 后 x，长度 (chunkSize+1)²，值为相对 chunk 原点 Y 的偏移。</summary>
    public List<float> terrainHeights;
}

[Serializable]
public class ChunkSpawnData
{
    public int prefabIndex;
    // 方块占一格：chunk-local 的格点坐标（整数）。
    public int x;
    public int y;
    public int z;
}

#endregion
