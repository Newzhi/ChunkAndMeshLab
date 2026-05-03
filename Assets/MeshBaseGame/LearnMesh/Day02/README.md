# Day02 — NativeArray、Job System、Burst 与程序化网格框架

本目录包含两块内容，可对照学习：

1. **数据与调度基础**：`NativeArray`、Job System、Burst（含最小 `IJobParallelFor` 示例）。
2. **程序化网格 Job 框架**（`ProceduralMeshes/`）：在 **`Mesh.MeshData`** 上配置顶点流与索引缓冲，用 **`IJobFor` + Burst** 写入网格；示例生成器 **`SquareGrid`** 在 **XZ 平面**上输出 **R×R** 个四边形。

可与 Day01 `SimpleProceduralMesh`（托管数组 + `Mesh`）对照理解进阶 Mesh API 与多线程生成流程。

---

## 1. `NativeArray<T>` 是什么？

- 命名空间：`Unity.Collections`
- **定长数组**，数据在 **非托管（native）内存**，不是 GC 堆上的 `T[]`。
- **典型用途**：与 **Job System**、**Burst**、网格/大批量顶点计算配合；生命周期由 **`Allocator`** 决定，用完必须 **`Dispose()`**，否则会泄漏。

| | 托管 `T[]` | `NativeArray<T>` |
|--|------------|------------------|
| 内存 | GC 管理 | 原生分配，显式释放 |
| Job/Burst | 受限 | 一等公民 |
| 释放 | 自动 | 必须 `Dispose()` |

**常用 Allocator 直觉**：`Temp`（极短、同一帧）、`TempJob`（四帧内、Job 友好）、`Persistent`（长期存活）。

---

## 2. Burst 编译器是什么？

- **Burst** 将符合 **HPC#（高性能 C# 子集）** 的代码通过 **LLVM** 编译为 **高度优化的原生机器码**（含 **SIMD** 等）。
- 在 **`struct`** 实现的 **`IJob` / `IJobParallelFor`** 等上标注 **`[BurstCompile]`**，Unity 在调度前编译 `Execute`，工作线程执行的是 Burst 版本。
- **不在 Burst 里做的事**：随意调用 `UnityEngine` 大部分 API、`string`、托管容器、`new` 引用类型、依赖 GC 的逻辑等。数值与向量多用 **`Unity.Mathematics`**（如 `float3`）。

**可选属性示例**：`FloatPrecision`、`FloatMode`、`CompileSynchronously` —— 在浮点一致性与性能、编辑器首次编译体验之间权衡。

---

## 3. 最小案例：`IJobParallelFor` + Burst

前置 Package：**Burst**、**Collections**、**Mathematics**、**Jobs**。

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct LengthSquaredJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> Inputs;
    public NativeArray<float> Outputs;

    public void Execute(int index)
    {
        float3 v = Inputs[index];
        Outputs[index] = v.x * v.x + v.y * v.y + v.z * v.z;
    }
}

// 调度示例（主线程）：
// var handle = job.Schedule(count, 64);
// handle.Complete();
// inputs.Dispose(); outputs.Dispose();
```

要点：

- **`[ReadOnly]`**：只读 `NativeArray` 便于优化与安全检测。
- **`Schedule(length, batchSize)`**：并行粒度；`batchSize` 可调试用 Profiler 对比。
- 去掉 **`[BurstCompile]`** 逻辑不变，通常更慢 —— 可用 Profiler 对比。

---

## 4. `IJob` 与 `IJobParallelFor`：区别和用法

Unity Job System 里最常对比的是这两个接口。

### `IJob`：整份任务只执行一次

- **入口**：`void Execute()`。每个 Job 实例在某个 **worker 线程上只调用一次** `Execute`；循环遍历数据由你在 `Execute` **自己写 for**。
- **调度**：`job.Schedule()` 或 `job.Schedule(JobHandle dependsOn)`。
- **适合**：工作量不大、或 **必须顺序执行**；对 `NativeArray` 做 **单次线性扫描**；逻辑不适合拆成「每个下标互不依赖」的并行。

```csharp
struct SumJob : IJob {
    public NativeArray<float> Values;
    public NativeArray<float> Result; // length 1

    public void Execute() {
        float s = 0;
        for (int i = 0; i < Values.Length; i++) s += Values[i];
        Result[0] = s;
    }
}
// new SumJob(...).Schedule().Complete();
```

### `IJobParallelFor`：按「下标」并行多次 `Execute`

- **入口**：`void Execute(int index)`。调度器在 **多个 worker** 上 **并行**调用，每个 `index` 一份；通过 **`Schedule(arrayLength, batchSize)`** 指定范围和批量大小。
- **`batchSize`**：一批连续下标交给一个 worker，并行粒度可调（可用 Profiler 对比）。
- **适合**：每个下标计算 **互不依赖**（或只读共享），典型如顶点逐点变换、逐元素数值内核。

### 对照

| | `IJob` | `IJobParallelFor` |
|--|--------|-------------------|
| 调用次数 | 每个实例 **`Execute()` 一次** | **`Execute(index)` 多次**（并行） |
| 调度 | `Schedule()` | `Schedule(length, batchSize)` |
| 并行 | 通常单线程跑完整个 `Execute` | 多核分担不同 `index` |

### 共用注意点

- Job 必须是 **`struct`**，字段符合 **NativeContainer / blittable** 等限制。
- **`IJobParallelFor` 避免写冲突**：一般「不同 `index` 写不同输出槽」；需要汇总时用单独 Job 或原子操作。

### 顺带：`IJobFor`

也是 `Execute(int index)`，但是 **按顺序、单线程** 执行每个 index。适合想按下标拆分结构、又不要并行的场景。**本目录 `MeshJob<G,S>`** 继承 **`IJobFor`**：并行 units 为「网格行」（`JobLength = Resolution`），行内用 `for` 写多个四边形，避免「每个四边形一个 Job」的开销。

---

## 5. Job 的线程从哪来？如何被调用？

**不需要在业务代码里 `new Thread()`。** Unity 使用 **内置 Worker 线程池 + 调度器**。

1. **线程来源**：一组 Worker 线程由引擎维护；**`Schedule` 并不等价于「立刻新建一条线程」**，而是把任务 **入队**，由调度器分配到空闲 worker。
2. **「创建 Job」**：Job 是 **值类型 `struct`**。你 **构造 struct、填入 Native 容器等字段**，再调用 **`Schedule`**，相当于向调度器登记「要执行多少次 `Execute`、依赖关系」；之后由 **worker 在线程池里调用你的 `Execute` / `Execute(index)`**。
3. **主线程**：负责 `Schedule`、`Complete`、以及访问 **非线程安全** 的 `UnityEngine` API。
4. **`Complete()`**：当前线程 **阻塞等待**该 `JobHandle` 上的工作结束。若马上要读 `NativeArray` 或 `Dispose`，应先保证已完成。
5. **依赖链**：`jobB.Schedule(jobA.Schedule())` 等，调度器保证顺序，执行仍在线程池上进行。

本目录 `BurstDemoLog.cs` 中的 `Schedule(Count, Batch).Complete()` 表示：**登记并行任务 → 当前线程等到跑完**；并行发生在 **`Complete` 返回前**这段时间的 worker 上。

**Profiler**：可在 Editor 中查看 Job/Burst 相关时间线（名称随 Unity 版本略有不同）。

---

## 6. 与程序化网格的关系（学习路线）

- **Day01**：用 **`Vector3[]` / `int[]`** 在主线程构建四边形并赋给 `Mesh`。
- **Day02**：
  - **`Code/BurstDemoLog.cs`**：同一数值内核在「仅 Job」与「Job + Burst」下的耗时对比。
  - **`ProceduralMeshes/`**：使用 **`Mesh.AllocateWritableMeshData`** → **`MeshJob`（Burst `IJobFor`）** → **`Mesh.ApplyAndDisposeWritableMeshData`**，在 **可写 `MeshData`** 上直接填充顶点流与 **16 位**三角形索引；生成器只面向逻辑类型 **`Vertex`**，具体布局由 **`SingleStream` / `MultiStream`** 隐藏。

---

## 7. 常见踩坑

1. **`Native*` 容器忘记 `Dispose`** → 内存泄漏，编辑器控制台可能有泄漏告警。
2. **在 Burst 内 `Debug.Log`** → 需 **`[BurstDiscard]`** 包装或仅在主线程日志。
3. **随机数** → 使用 **`Unity.Mathematics.Random`** 等 Burst 兼容 API，而非 `UnityEngine.Random`。
4. **Job 内捕获托管对象** → Job 必须是纯 **struct**，字段类型需符合 Job/Burst 限制。
5. **网格 Job 调度前调用 `SetSubMesh` 默认会校验索引**：缓冲区尚未写入有效三角形时会失败 —— 需 **`MeshUpdateFlags.DontRecalculateBounds | DontValidateIndices`**，并在生成器侧提供 **`Bounds`**（网格与子网格）。
6. **`GetVertexData` 与 `GetIndexData` 指向同一块 Mesh 底层内存的不同视图**：Unity 安全系统可能误判「别名」—— 本工程在流结构体上对顶点缓冲与索引视图标注 **`[NativeDisableContainerSafetyRestriction]`**，仅在确认读写区间不重叠时使用。

---

## 8. 官方与扩展阅读

- Unity Manual：**Job System**、**Burst Compiler**、**Unity.Collections**、**Advanced Mesh API**（`MeshData`）
- 安装路径：**Package Manager** 搜索 `Burst`、`Collections`、`Mathematics`、`Jobs`
- 外部连载（思路参考）：Catlike Coding — *Procedural Meshes*（Square Grid Mesh Jobs 等）

---

## 9. 本文件夹资产一览

| 路径 | 说明 |
|------|------|
| `Day02.unity` | 配套场景 |
| `Code/BurstDemoLog.cs` | Play 后 Console 对比「仅 Job」与「Job + Burst」耗时（含 Burst 预热） |
| `ProceduralMeshes/` | 程序化网格框架（见下文 §10–§15） |
| `README.md` | 本说明 |
| `程序化网格框架-设计思路与代码详解.md` | `ProceduralMeshes/` 分层动机与逐文件代码走读 |

**运行 Burst 演示**：场景中创建空物体，挂载 **`BurstDemoLog`**，进入 Play，查看 Console。负载由脚本内 `Count` / `Times` 等常量控制。

---

## 10. `ProceduralMeshes` 框架在做什么？

目标是把「**几何逻辑**」和「**顶点/索引在 GPU 缓冲里的具体布局**」拆开：

- **`IMeshGenerator`**：根据分辨率等参数，在 **`Execute`** 里调用 **`streams.SetVertex` / `SetTriangle`**，只关心 **`Vertex`** 与拓扑。
- **`IMeshStreams`**：在 **`Setup`** 里声明 **`VertexAttributeDescriptor`**、索引格式、子网格；持有 **`NativeArray`** 视图并 **`SetVertex` / `SetTriangle`** 写入 **`Mesh.MeshData`**。
- **`MeshJob<G, S>`**：Burst **`IJobFor`**，把调度下标 `i` 转发给 **`generator.Execute(i, streams)`**；**`[WriteOnly] S streams`** 提示仅写入网格缓冲。

调度入口：**`MeshJob<G, S>.ScheduleParallel(mesh, meshData, resolution, dependency)`** —— 内部会先设置 **`generator.Resolution`**，再用生成器的 **`Bounds`、`VertexCount`、`IndexCount`** 调用 **`streams.Setup`**，最后 **`ScheduleParallel(generator.JobLength, 1, …)`**。

---

## 11. 目录结构与命名空间

```
Day02/
├── Code/
│   └── BurstDemoLog.cs
├── ProceduralMeshes/
│   ├── Vertex.cs                 # 逻辑顶点（float3/float4/float2）
│   ├── IMeshStreams.cs
│   ├── IMeshGenerator.cs
│   ├── MeshJob.cs
│   ├── ProceduralMesh.cs         # MonoBehaviour（全局命名空间）
│   ├── Streams/
│   │   ├── TriangleUInt16.cs    # 三个 ushort，对应一个三角形
│   │   ├── SingleStream.cs       # 单一流：交错顶点属性
│   │   └── MultiStream.cs        # 多流：position/normal/tangent/uv 各占一流
│   └── Generators/
│       └── SquareGrid.cs         # XZ 平面 R×R 四边形
└── README.md
```

- 根命名空间：**`MeshBaseGame.LearnMesh.Day02.ProceduralMeshes`**
- 子命名空间：**`.Streams`**、**`.Generators`**
- **`ProceduralMesh`** 类位于 **全局命名空间**，便于在 Inspector 里与其他教程脚本风格一致。

---

## 12. 接口约定摘要

### `IMeshStreams`

- **`Setup(meshData, bounds, vertexCount, indexCount)`**：配置顶点缓冲参数、`IndexFormat.UInt16`、单个子网格（带 **`bounds` / `vertexCount`** 的 **`SubMeshDescriptor`**），并 **`MeshUpdateFlags.DontRecalculateBounds | DontValidateIndices`**。
- **`SetVertex(index, Vertex)`**：写入第 `index` 个逻辑顶点。
- **`SetTriangle(index, int3)`**：写入第 `index` 个三角形（三个顶点索引）。

### `IMeshGenerator`

- **`Execute<S>(i, streams)`**：第 `i` 个并行单位的具体生成逻辑。
- **`VertexCount` / `IndexCount` / `JobLength`**：分配与调度规模。
- **`Bounds`**：写入 **`Mesh.bounds`** 并传入 **`Setup`**。
- **`Resolution`**：由 **`ScheduleParallel`** 在调度前赋值（当前 **`SquareGrid`** 用它表示每边四边形数量）。

---

## 13. `SquareGrid` 行为说明

- **平面**：**XZ**，法线朝 **+Y**；整体居中，范围约 **1×1**（**`Bounds`** 为 **`Vector3.zero` + `(1,0,1)`**）。
- **拓扑**：每个格子 4 顶点、2 三角形；总计 **`VertexCount = 4·R²`**，**`IndexCount = 6·R²`**。
- **并行粒度**：**`JobLength = R`**，每个 **`Execute(z, …)`** 处理 **固定 z 的一整行** `x = 0…R-1`，行内循环更新 **`vi` / `ti`**，减少 Job 数量、便于 Burst 优化内层循环。

---

## 14. 如何使用 `ProceduralMesh` 组件

1. 物体上添加 **`MeshFilter`**、**`MeshRenderer`**（脚本带 **`[RequireComponent]`** 可自动补全）。
2. 挂载 **`ProceduralMesh`**。
3. 在 Inspector 调节 **`Resolution`**（1–10）；通过 **`OnValidate` + `Update` 内生成一次并关闭组件** 的方式，在编辑模式下改参数也会触发重建。
4. 默认使用 **`MeshJob<SquareGrid, MultiStream>`**。若要对比 **单流**，打开 **`ProceduralMesh.cs`**，将 **`MultiStream`** 改为 **`SingleStream`** 即可（生成器代码无需修改）。

---

## 15. `SingleStream` 与 `MultiStream`

| | `SingleStream` | `MultiStream` |
|--|----------------|---------------|
| 顶点布局 | 单缓冲交错 **`Stream0`**（与 `Vertex` 字段顺序一致） | 四路：`float3`×2、`float4`、`float2` |
| 索引 | **`ushort`** + **`NativeArray<TriangleUInt16>`** | 同左 |
| 典型用途 | 与单缓冲 GPU 布局一致、教程对照 | 与分 stream 的顶点属性布局一致 |

二者均在 **`Setup`** 末尾通过 **`GetVertexData` / `GetIndexData`** 取得 **`NativeArray`**，并对顶点数组与三角形数组使用 **`[NativeDisableContainerSafetyRestriction]`**（见 §7）。

---

## 16. 工程依赖提示

工程通常在 `Packages/manifest.json` 中包含 **`com.unity.collections`**、**`com.unity.jobs`**，Burst / Mathematics 随 URP 等常见模板引入。若 Unity 提示包版本解析问题，以 Package Manager 建议为准。

建议在场景中运行 **`BurstDemoLog`** 或 **`ProceduralMesh`** 时，结合 **Profiler** 确认 Burst 与 Job 实际参与执行。
