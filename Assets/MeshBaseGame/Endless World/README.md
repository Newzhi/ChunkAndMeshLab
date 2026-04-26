# Endless World（3D 体素密度场 + Marching Cubes）设计说明

本目录是一套 3D “无尽世界/体素地形”原型，实现目标是：**在有限视距内动态维护 Chunk，并通过密度场 + Marching Cubes 生成可渲染网格**。该实现以 Demo 为主，优先展示链路（生成 → 网格化 → 可编辑），而非完整游戏级工程化（存档/版本/并发任务等）。

---

## 目录结构（按职责）

- `Assets/Endless World/3D/Core/`
  - `Chunk3D.cs`：Chunk 容器，负责持有 `MeshFilter/MeshRenderer/MeshCollider`，并把 `MeshData` 应用到 Unity `Mesh`。
  - `Chunk3DManager.cs`：按 `viewer` 位置创建“可见 Chunk”（偏演示：只创建，不回收）。
  - `Chunk3DReuseManager.cs`：按 `viewer` 位置维护“可见 Chunk”，并把离开视距的 Chunk 放入回收队列复用（更符合无尽世界滚动需求）。
  - `MarchingCube.compute`：Marching Cubes 核心 kernel（GPU 侧从 density field 生成三角形）。
  - `MarchingCubesTable.compute`：三角化查表（256 种 cubeIndex 的 triangulation 表与边映射）。

- `Assets/Endless World/3D/Demo 1/`
  - `NoiseTerrainGenerator.cs`：密度场生成（噪声参数），写入 `pointsBuffer`（`float4 xyz + density(w)`）。
  - `MarchingCubesMesh.cs`：Demo1 入口（继承 `Chunk3DReuseManager`），负责在每个 Chunk 上生成 mesh。

- `Assets/Endless World/3D/Demo 2/`
  - `EditableGenerator.cs`：编辑笔刷，基于 `RaycastHit`（点击位置/法线/半径）修改密度场。
  - `MarchingCubesMeshEditable.cs`：Demo2 入口（继承 `Chunk3DManager`），左键编辑地形并重建 mesh。

---

## 核心概念

### 1) Chunk（区块）

- 用 `Vector3Int coord` 标识区块网格坐标。
- Chunk 的空间中心由 `coord * chunkSize` 得到（`CenterFromCoord`）。
- 每个 Chunk 表现为 **一个 Mesh**（可选碰撞体 MeshCollider）。

### 2) Density Field（密度场）

- 采样网格为 `numPointsPerAxis^3` 个点。
- 每个点用 `float4` 表示：
  - `xyz`：世界空间坐标
  - `w`：密度/标量值
- 给定 `isoLevel`，Marching Cubes 会抽取等值面（surface）并生成三角网格。

### 3) Marching Cubes（GPU 三角化）

- 每个 voxel（立方体单元）由 8 个角点密度决定 `cubeIndex (0..255)`。
- 使用 `MarchingCubesTable.compute` 的 triangulation 表确定应生成哪些三角形边。
- 对边端点做线性插值求顶点位置，并将三角形 append 到 GPU 的 `AppendStructuredBuffer<Triangle>`。

---

## 运行流程（从移动到看到地形）

### 1) 视距内 Chunk 的确定

`Chunk3DManager/Chunk3DReuseManager` 都基于：
- `viewer.position` 计算 `viewerCoord`
- `viewDistance/chunkSize` 推导视距内 chunk 坐标范围
- 再用“视距距离 + 相机视锥（frustum）”筛掉不需要的 chunk

### 2) Chunk 创建/复用

- Demo1 使用 `Chunk3DReuseManager`：
  - 离开视距：移出缓存并入回收队列（Queue）
  - 新进入视距：优先复用回收的 Chunk，减少频繁 `Instantiate/Destroy` 导致的卡顿与 GC
- Demo2 使用 `Chunk3DManager`（不回收）：
  - 主要是为了简化“编辑状态”的管理（见下文 Demo2 的数据缓存取舍）

### 3) 密度场生成（ComputeShader）

- Demo1：`NoiseTerrainGenerator.Generate` 根据噪声参数填充 `pointsBuffer` 的 `float4` 数据。
- Demo2：初次仍可使用噪声生成；编辑时由 `EditableGenerator.Generate` 按笔刷修改密度。

### 4) Marching Cubes 三角化（ComputeShader）

`MarchingCube.compute` 对每个 voxel 并行生成三角形，写入 triangleBuffer。

### 5) CPU 回读并组装 Unity Mesh

在 C# 侧：
- 读取 triangleBuffer 的三角形数量与数据
- 展开为 `vertices[]` 与 `triangles[]`（Demo2 还会生成 `colors[]`）
- 应用到 `Chunk3D.UpdateMesh(meshData)`，并 `RecalculateNormals()` 更新法线/碰撞体

> 当前实现是“GPU 生成 + CPU 回读 + Unity Mesh”，优点是链路清晰、接入 Unity API 简单；缺点是回读会带来额外开销。更极致的方案是 GPU 侧直接绘制（procedural/indirect），避免回读，但工程复杂度更高。

---

## Demo 说明

### Demo 1（噪声地形 + 复用滚动）

- 场景：`Assets/Endless World/3D/Demo 1/Demo 1.unity`
- 特点：
  - 使用 `Chunk3DReuseManager`，适合无尽移动时的平滑性能
  - 可通过 `onlyDebugBounds/showBoundsGizmo` 仅显示边界框用于调试

### Demo 2（可编辑地形）

- 场景：`Assets/Endless World/3D/Demo 2/Demo 2.unity`
- 操作：
  - 鼠标左键射线命中地形后，使用“笔刷”修改密度并重建 mesh
- 数据取舍：
  - 通过 `Dictionary<Vector3Int, Vector4[]> chunkDatas` 缓存每个 Chunk 的密度点数据，以保证编辑结果在运行期内可持续存在
  - 代价：已生成/已编辑的 chunk 越多，内存占用越大

---

## 与 `Assets/ChunkBaseGame/ChunkTest/README.md` 方案的对比（实现差异）

该对比的目的：帮助你理解两套方案分别解决什么问题、各自的“下一步工程化缺口”在哪里。

### 1) 关注点：工程分层 vs 算法演示

- `ChunkTest`（README 明确分层：Config/Manager/Data/Generator/Storager）目标是：
  - 将来可随时替换“生成算法/存储格式/渲染方式”
  - 并且优先打通 **加载/卸载/存档/还原** 闭环
- `Endless World` 目标是：
  - 快速展示 **density → Marching Cubes → mesh** 的整条渲染链路（含 GPU compute）
  - 当前没有独立 Storager/存档 DTO 分层，更多是 demo 级实现

### 2) 世界表示：离散方块（blockId） vs 连续密度（iso-surface）

- `ChunkTest` 当前用 prefab 验证闭环，演进方向是 `ChunkVoxelData`（离散 blockId 网格），天然适配 MC 的“放置/破坏方块”。
- `Endless World` 用密度场（float）表示体积，Marching Cubes 输出连续表面，更适配“洞穴/雕刻/平滑地形”，不是传统方块外表面。

### 3) 网格化与性能路径不同

- `ChunkTest` 把 meshing 作为后续阶段（朴素/贪婪网格，配合 Job/Burst），当前 prefab 明确是性能不可持续的原型。
- `Endless World` 直接使用 GPU compute 做密度生成与三角化，并在 CPU 回读组 mesh；性能瓶颈集中在：
  - 回读 triangleBuffer
  - 每次编辑触发的重建频率
  - Demo2 的密度缓存内存

### 4) 生命周期与持久化能力不同

- `ChunkTest`：Unload 时保存 DTO、写盘、再 Load 还原，是“可持久化世界原型”。
- `Endless World`：编辑结果仅在运行期内存中存在，未实现写盘与跨会话恢复。

---

## 下一步工程化方向（可选）

如果要把 `Endless World` 发展成可长期演进的系统，通常会补齐：
- **数据层**：Chunk 的密度数据/编辑增量（可选压缩）
- **存储层**：按 chunk 或 region 写盘，支持版本与增量保存
- **异步/并行**：把密度生成/三角化/回读/mesh 应用解耦，避免主线程峰值
- **跨 chunk 边界一致性**：编辑影响邻接 chunk 时的边界共享点处理

