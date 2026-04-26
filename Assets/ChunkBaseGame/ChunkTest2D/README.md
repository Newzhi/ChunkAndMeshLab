# ChunkTest2D（2D 区块原型）说明

本目录是 ChunkTest 的 2D 版：把 3D 的 chunk 加载/卸载/存档闭环简化到 2D（默认 **XY 平面**）。
目标仍然是分层解耦：Manager 只做窗口调度；Generator 负责生成/实例化/卸载回写；Storager 负责磁盘读写。

## 目录结构（按职责）

- `Chunk/Config/ChunkConfig2D.cs`：Inspector 配置入口，输出 `ChunkSettings2D`
- `Chunk/Data/ChunkData2D.cs`：坐标/边界/状态/存档 DTO
- `Chunk/Utils/ChunkUtil2D.cs`：World ↔ ChunkCoord/Local 的纯函数转换
- `Chunk/Server/ChunkManager2D.cs`：窗口刷新 + Load/Unload 调度 + 缓存
- `Chunk/Server/ChunkGenerator2D.cs`：内容生成 + 实例化 + 卸载回写（当前用 prefab 验证闭环）
- `Chunk/Server/ChunkStorager2D.cs`：TempData 下 JSON 读写

> 如需改为 XZ 平面（俯视 2D），把 `ChunkManager2D` 里玩家坐标取值从 `(x,y)` 改为 `(x,z)`，并相应调整 `ChunkUtil2D.WorldToChunkCoord` 的输入即可。

