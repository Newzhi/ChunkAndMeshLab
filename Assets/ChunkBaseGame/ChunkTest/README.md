# ChunkTest（MC 区块原型）说明

本目录是一套“像 MC 一样的区块加载/卸载/存档”原型，用 **prefab** 先验证链路（生成 → 实例化 → 卸载保存 → 再加载还原），并通过分层解耦保证后续能平滑演进到 **体素数据 + Mesh** 的真正 MC 架构。

---

## 目录结构（按职责）

- `Assets/ChunkBaseGame/ChunkTest/Chunk/Config/`
  - `ChunkConfig.cs`：**配置入口**（Inspector 配置），统一输出 `ChunkSettings`
- `Assets/ChunkBaseGame/ChunkTest/Chunk/Data/`
  - `ChunkData.cs`：**运行时聚合数据**（Chunk 身份/边界/状态/运行时托管集合/存档 DTO）
  - `ChunkVoxelData.cs`：**体素数据（演进方向）**（Chunk 内 blockId 网格；以 local 坐标访问）
- `Assets/ChunkBaseGame/ChunkTest/Chunk/Utils/`
  - `ChunkUtil.cs`：**工具类**（纯函数坐标转换：World ↔ ChunkCoord/Local）
- `Assets/ChunkBaseGame/ChunkTest/Chunk/Server/`
  - `ChunkManager.cs`：**区块与内容管线**（窗口、Load/Unload、读档/写盘、实例化、卸载回写、场景根节点）
  - `ChunkGenerator.cs`：**纯生成逻辑**（每个区块生成哪些东西、怎么生成、chunk-local 格点在哪；产出 `ChunkObjectSaveData`，不碰 IO 与 Instantiate）
  - `ChunkStorager.cs`：**存储层**（磁盘 IO；当前 JSON 原型；支持异步合并写入）
- `Assets/ChunkBaseGame/ChunkTest/Test/`
  - `Debugger/ShowChunkDebugger.cs`：调试面板（Chunks、FPS、CPU 等）
  - `PlayerAndCarmera/`：玩家与相机控制脚本（测试用）
  - `TestScene.unity`：测试场景

- `Assets/BaseFramework/`
  - `EventBus/`：**事件总线**（解耦模块通信：发布/订阅）
  - `Async/`：**异步工具**（为 UniTask 提供轻量信号/等待原语）

---

## 核心设计思路（为什么这样分层）

目标是：**将来可以随时替换“生成算法 / 存储格式 / 渲染方式”，而不用重写区块调度。**

### 1) Config：所有可调参数只放配置
`ChunkConfig -> ChunkSettings` 是唯一配置来源。  
Manager、Generator 与 Storager 都只读 `ChunkSettings`，避免散落的 Inspector 字段导致配置漂移。

### 2) Manager：区块调度 + 内容加载/卸载管线
`ChunkManager` 负责：
- 根据玩家位置计算窗口（方形/圆形）
- 加载缺失 chunk、卸载离开窗口的 chunk
- 维护 `Dictionary<long, ChunkData>` 缓存
- 读档 / 无档时调用 `ChunkGenerator` 得到数据 / 异步写盘 / 实例化 prefab / 卸载时写回 DTO 再保存 / 销毁场景根节点

**不**在 Manager 里写具体地形算法；算法只在 `ChunkGenerator`。

### 3) Data：运行时数据与存档数据分离（以 `ChunkData` 为核心）
`ChunkData` 是区块的**运行时聚合体**：它把“身份、边界、状态、运行时托管对象、存档 DTO”聚在一起供 Manager 缓存与调度，但会刻意区分哪些是**身份/派生数据**，哪些是**运行时引用**，哪些是**可落盘数据**。

- **Chunk 身份：`Coord` 与 `Id` 都存在但不重复存储**
  - `Coord(X,Z)` 是区块的语义身份（用于坐标计算/推导边界/调试）。
  - `Id` 是哈希身份（用于 `Dictionary` key 与存档索引），由 `Coord` 位运算派生得到（`Id => Coord.Id`），避免出现“坐标改了但 ID 没更新”的一致性问题。
- **边界：`Bounds` 是由 `Coord + Settings` 推导的派生数据**
  - 边界不是区块身份，属于可计算结果；分离后更利于扩展不同高度范围/不同维度等。
- **运行时实例：`SpawnedInstances` 与存档 `spawns` 一一对应**
  - 当前原型用 `List<Transform>` 与 `ObjectSaveData.spawns[i]` 对齐，便于卸载时回写格点坐标；不再维护第二套 `HashSet` 以免双写漂移。
  - 若未来需要“动态进出 chunk 的实体”（与 spawns 索引无关），再单独引入托管集合或事件流，而不是在原型阶段重复维护两份引用容器。
- **存档 DTO：单独的 `[Serializable]` 数据结构**
  - 运行时的 `Transform`、集合引用、状态等不适合作为跨会话持久化数据；存档只保留“可重建”的最小信息（如 `chunkId`、`prefabIndex`、整数格点 `(x,y,z)`），从而让运行时结构可以自由演进而不绑死存档格式。
- **局部坐标：用 Chunk Local 作为“区块内地址”**
  - 区块内的绝大多数数据（方块、光照、邻接更新）都应以局部坐标表达：`(localX, localY, localZ)`。
  - 推荐约定：`localX/localZ ∈ [0, Size-1]`，`localY = worldY - MinY`（使局部 Y 从 0 开始，数组更紧凑）。
  - World ↔ Local 的换算统一通过 `ChunkBounds.MinX/MinY/MinZ` 做偏移（见 `ChunkUtil.WorldToLocal/LocalToWorld`），避免各处自己算导致 off-by-one。

### 3) Generator：只负责「生成数据」（`ChunkGenerator`，当前 prefab 占位验证）
- 输出 `ChunkObjectSaveData`：`prefabIndex` + chunk-local `(x,y,z)`
- 当前实现：Perlin 高度图 + 每列竖条占位（**不**读盘、不 Instantiate）
- 实例化与回写存档由 `ChunkManager` + `ChunkStorager` 完成

> 注意：这只是验证管线的临时手段，性能无法支撑 MC 规模（见下文“注意事项/瓶颈”）。

### 4) Storager：存储完全解耦（存档结构不反向污染运行时结构）
`ChunkStorager` 只管：
- `TryLoadChunkObjects(chunkId, settings, out data)`
- `SaveChunkObjects(data, settings)`

后续切换为二进制/Region 文件/多世界目录时，只需要改 Storager；换地形/内容规则时主要改 `ChunkGenerator.GenerateChunkObjectSaveData` 及其分支。

---

## 当前原型的“可运行闭环”

1. 玩家进入窗口 → `ChunkManager.LoadChunk` 创建 `ChunkData`
2. `ChunkManager.LoadChunkContent`：
   - `ChunkStorager.TryLoadChunkObjects`；若无则 `ChunkGenerator.GenerateChunkObjectSaveData` 再异步写盘
   - 按 `spawns` 实例化 prefab
3. 玩家离开窗口 → `ChunkManager.UnloadChunk`
4. `ChunkManager.UnloadChunkContent`：
   - 将 Transform 写回 `spawns` 的 chunk-local 格点，有变化则异步保存
   - 清空 `SpawnedInstances`，销毁 chunk 对象根节点

---

## 注意事项（很重要）

### 1) prefab 方案的性能瓶颈（必然卡）
每个方块一个 GameObject 会导致：
- Transform/Renderer 数量爆炸（成千上万）
- `Instantiate/Destroy` 产生严重卡顿和 GC 压力
- JSON 存档体积与读写时间迅速增长

所以 prefab 只用于：
- 验证 **加载/卸载/存档/还原** 链路
- 验证多世界目录、存档版本等系统问题

要做 MC 规模，必须转向：**体素数据 + ChunkMesher（网格化渲染）**。

### 2) TempData 写盘位置
当前 JSON 写到：
- `Assets/ChunkBaseGame/ChunkTest/TempData/`（由 `ChunkSettings.TempDataFolderUnderAssets` 决定）

Unity Project 面板不一定即时刷新文件显示；必要时手动刷新/重启编辑器确认。

### 3) prefabIndex 的含义
存档里用 `prefabIndex` 指向 `ChunkConfig` 中的 `spawnPrefabs` 数组下标。  
如果你重排数组，下标语义会变化（原型阶段可接受；正式版建议改为稳定的 `blockId`）。

---

## 扩展方向（向 MC 演进路线）

### 阶段 A：从 prefab 过渡到“体素数据”
新增 `ChunkVoxelData`（例如 `byte[] blocks` 或 `ushort[] blocks`）：
- **Voxel（体素）是什么**：三维空间中的离散格子（volumetric pixel）。在 MC 中就是“方块占据的一格”。  
  `ChunkVoxelData` 表示“每个格子里是什么方块（blockId）”的**数据本体**，不是渲染网格。
- **局部坐标为何必要**：体素数据天然用 chunk-local 的 `(localX,localY,localZ)` 索引；world 坐标只是表现层，最终由 `LocalToWorld` 推导。
- Generator 生成体素数组（含 y 轴），常见索引展开方式为一维数组：`index = (localY * Size + localZ) * Size + localX`（只要工程内统一即可）。
- Storager 存/取体素数组（建议二进制 + 压缩；JSON 仅适合原型验证）

### 阶段 B：ChunkMesher（网格化渲染）
新增 `ChunkMesher`：
- **Mesh（网格）是什么**：渲染用的三角形集合（顶点/索引/法线/UV）。它是把体素数据的“外表面”转换为可渲染几何。
- 输入：体素数据 + 邻接 chunk 边界（决定边界面是否可见）
- 输出：`Mesh`（挂 `MeshFilter/MeshRenderer`），通常每个 chunk 1 个（或少量）GameObject

常见算法：
- **朴素 Meshing**：实现简单，面数多（适合第一版）
- **贪婪网格 Greedy Meshing**：合并共面方块面，显著减面（MC 常用优化方向）

### 阶段 C：多线程 / Job System
可并行的任务：
- 生成（Generator：噪声、结构、群系）
- 网格化（Mesher：构建顶点/索引）
- 光照、邻接更新

Unity 推荐：
- C# Job System + Burst（数据结构用 NativeArray）
- 主线程只做：提交任务、应用 Mesh、管理 GameObject 生命周期

### 阶段 D：存储升级（Region / 增量保存 / 版本迁移）
从 `chunk_<id>.json` 过渡到：
- Region 文件（按 32×32 chunk 打包）
- 二进制压缩（LZ4/Zstd）
- 存档版本号（支持升级/兼容）
- 多世界目录（worldName/worldSeed/meta）

### 阶段 E：渲染区块 vs 模拟区块
像 MC 一样区分：
- **RenderRadius**：生成 Mesh/渲染
- **SimRadius**：只保留数据/运行简化模拟（作物生长、红石、AI 等）

---

## 建议的下一步（最短路径）

1. 保持 `ChunkManager` 调度不动
2. 把 prefab 生成的密度降到可交互程度（否则编辑器无法验证其它系统）
3. 新增 `ChunkVoxelData` 与 **朴素 Mesher**，让每个 chunk 只有 1 个 Mesh（性能立刻好一个数量级）

---

如需我继续把 prefab 原型迁移到体素数据 + Mesher（保持现有解耦风格与目录规范），可以直接指定“先朴素网格”还是“直接贪婪网格”，以及你想用的 blockId 规模（1 字节/2 字节）。

