# GPU Instancing 与相关概念（通用说明）

本文说明 **GPU 实例化** 在渲染里是什么、**Unity 中与实例化相关的关键 API 及参数**，以及典型 **使用案例（代码思路）**。不绑定具体工程目录；**具体重载与默认值请以你所用 Unity 版本的官方文档为准**。

---

## 1. 它是什么

**GPU Instancing** 是一种绘制技术：用 **同一份网格（Mesh）和同一份着色资源（如材质/Shader）**，在 **一次或少数几次 Draw Call** 里，让 GPU 画出 **许多份** 几何体。

每一份拷贝称为一个 **实例（Instance）**。实例之间通常只在 **世界变换（位置/旋转/缩放）** 或 **少量逐实例参数（如颜色）** 上不同。

可以理解为：CPU 说「这批东西长得一样、着色方式一样，这是 N 个不同的变换」，GPU 按变换把同一模型画 N 次，而不是为每个物体单独发 N 次完整绘制命令（在理想情况下）。

---

## 2. 为什么要用（解决什么问题）

- **Draw Call / 批次**：大量相同物体时，实例化把「多次相似绘制」合并成「少量带实例数据的绘制」。
- **场景对象数量**：**渲染**可与**逻辑/物理**解耦，不必为每个可见体都挂一个完整游戏对象。
- **适用典型场景**：草、树、碎石、重复道具、体素块状世界等。

代价：**不产生**「每个实例一个带 Collider/脚本的物体」；**拾取、碰撞**需另做。

---

## 3. 与「合批 / Batching」的关系（易混概念）

| 概念 | 大致含义 |
|------|----------|
| **Static Batching** | 把静态网格合并成大网格，减批次；占内存，物体需静态。 |
| **Dynamic Batching** | 运行时合并小网格；有顶点数等限制。 |
| **GPU Instancing** | **不合并顶点**，一次 Draw 带 **多组实例数据**（多份世界矩阵等）。 |
| **SRP Batcher** | 降低材质状态切换；与 Instancing **是否同时生效**取决于 Shader/变体。 |

---

## 4. Unity：关键 API 与参数说明

### 4.1 `Graphics.DrawMeshInstanced`（最常用、由 CPU 传矩阵）

**作用**：提交 **最多一批** 实例；每个实例一个 **`Matrix4x4`**（世界变换）。材质应开启 **GPU Instancing**，否则可能低效或表现异常。

**重要限制**：单次调用有效实例数 **`count` 不得超过 1023**（Unity 固定上限）。更多实例必须 **循环分批**，每批 ≤1023。

下面按常见重载逐项说明（名称与可选参数在不同 Unity 版本可能略有增减）：

| 参数 | 类型 | 含义 |
|------|------|------|
| `mesh` | `Mesh` | 所有实例共用的网格。 |
| `submeshIndex` | `int` | 子网格索引；未使用多材质槽时一般为 **0**。 |
| `material` | `Material` | 绘制所用材质；建议 **`enableInstancing == true`**。 |
| `matrices` | `Matrix4x4[]` | 变换矩阵数组；**仅前 `count` 个**参与本次绘制。 |
| `count` | `int` | 本批实例个数，**1～1023**，且 ≤ `matrices.Length`。 |
| `properties` | `MaterialPropertyBlock` | **可选**。用于在 **同一材质** 下传入逐实例不同的属性（需 Shader 支持实例化属性）。不传则各实例外观一致（仅矩阵不同）。 |
| `castShadows` | `ShadowCastingMode` | 是否投射阴影。 |
| `receiveShadows` | `bool` | 是否接收阴影。 |
| `layer` | `int` | 层；影响相机 Culling Mask 等。 |
| `camera` | `Camera` | **null** 时常表示按当前上下文绘制（例如在 Game 视图中可见）；指定相机则仅对该相机相关渲染生效（行为以文档为准）。 |

**矩阵含义**：`Matrix4x4.TRS(position, rotation, scale)` 把模型空间顶点变到世界空间，等价于「同一个 Mesh 画在不同位置/朝向/缩放」。

---

### 4.2 `Graphics.DrawMeshInstancedIndirect`

**作用**：实例数量与/或实例数据来自 **GPU 可读缓冲区（如 ComputeBuffer）**，由 **Indirect Args** 驱动，适合 **超大批量**、减轻 CPU 逐实例遍历。

**典型涉及**：`ComputeBuffer`（或 GraphicsBuffer）、**args buffer**（存放 index/instance 数量等）、常配合 **Compute Shader** 做剔除、LOD。

**与 `DrawMeshInstanced` 对比**：前者 CPU 直接填矩阵数组；Indirect 把「画多少、从哪读」更多交给 GPU/缓冲，**实现成本更高**，扩展性更好。

---

### 4.3 `Graphics.RenderMeshInstanced` / `RenderParams`（较新路线）

Unity 较新版本提供的绘制入口，仍围绕 **网格 + 材质 + 多实例**，参数打包方式与 `DrawMeshInstanced` 不同（如使用 `RenderParams`）。**选型时以项目 Unity 版本文档为准**。

---

### 4.4 材质侧：`Material.enableInstancing`

- **`true`**：使用支持 **GPU Instancing** 的 Shader 变体，实例化数据（如逐实例矩阵）才能按预期参与着色。
- 修改资源上的材质会影响整个工程；运行时试用可 **`new Material(原材质)`** 克隆再改，并在适当时机 **`Destroy`** 克隆，避免泄漏。

---

## 5. 使用案例（代码思路）

### 案例 A：固定数量实例，且总数 ≤ 1023

适用于：一片不超过 1023 个的相同模型（如一片小草地标）。

```csharp
// 伪代码：准备网格、材质（enableInstancing = true）、矩阵数组
Matrix4x4[] matrices = new Matrix4x4[instanceCount];
for (int i = 0; i < instanceCount; i++)
{
    matrices[i] = Matrix4x4.TRS(positions[i], rotation, scale);
}
Graphics.DrawMeshInstanced(mesh, 0, material, matrices, instanceCount);
```

---

### 案例 B：超过 1023 个实例：固定长度缓冲 + 循环分批

**原因**：单次 API 最多 **1023**，必须拆成多轮 `DrawMeshInstanced`。

```csharp
const int MaxPerBatch = 1023;
var batch = new Matrix4x4[MaxPerBatch];
int total = allMatrices.Count; // 假设已装入 List<Matrix4x4>
int offset = 0;

while (offset < total)
{
    int n = Mathf.Min(MaxPerBatch, total - offset);
    for (int i = 0; i < n; i++)
        batch[i] = allMatrices[offset + i];

    Graphics.DrawMeshInstanced(mesh, 0, material, batch, n);
    offset += n;
}
```

**注意**：每帧若矩阵变化，需要更新 `allMatrices` 或 `batch` 中内容；`batch` 可复用以减少 GC。

---

### 案例 C：每帧在 `Update` / `LateUpdate` 中绘制（无场景 Renderer）

`DrawMeshInstanced` **不会**自动像 `MeshRenderer` 那样随物体启用/禁用；通常由脚本 **每帧主动调用**（或在你自定义的渲染回调里调用），否则只调用一次则只画一帧。

常见写法：在 **`LateUpdate`** 中调用，以便同一帧内相机、逻辑先更新完再画（非强制，视项目而定）。

---

### 案例 D：逐实例不同颜色（需 Shader + `MaterialPropertyBlock`）

思路概要：

1. Shader 中声明 **per-instance** 属性（如 `_BaseColor` 的 `UNITY_INSTANCING` 用法，以 URP/HDRP 文档为准）。
2. 为每一批准备 **`MaterialPropertyBlock`**，或使用 API 允许的逐实例数据路径。
3. 注意：**一批内**若用 MPB，常见模式是「每实例一个颜色」与 API 限制、Shader 写法强相关，需对照官方示例实现。

（具体 Shader 代码与 MPB 填法因管线而异，此处只保留「案例类型」说明。）

---

### 案例 E：需要碰撞 / 射线检测

实例化 **只负责画**；**不会**给每个实例加 `Collider`。

常见做法：

- **粗粒度**：每个区块一个合并 **`MeshCollider`**，网格由体素/占用数据烘焙；
- **细粒度**：仅对少量交互物使用真实 GameObject + Collider，与 GPU 实例化分层。

---

## 6. 变换矩阵：`Matrix4x4` / TRS

每个实例在世界里放在哪、怎么转、多大，通常打包成一个 **4×4 齐次变换矩阵**。  
常用：`Matrix4x4.TRS(position, rotation, scale)`。  
GPU 顶点阶段把 **模型空间 → 世界空间**，再经视图投影到屏幕。

---

## 7. 限制与常见误区

1. **不是「创建了物体」**：多数情况下场景层级里 **没有** 每个实例一个对象，因此 **没有** 自动 Collider / 每实例 `Update`。
2. **拾取**：需额外逻辑（实例 ID、深度重建、Compute 等）。
3. **材质/关键字不一致** 会 **拆批**，实例化收益下降。
4. **1023**：超过必须分批；注意 CPU 填矩阵的成本。

---

## 8. 延伸方向（进阶）

- **Indirect + Compute**：剔除、LOD、超大批量。
- **底层 API**：D3D `DrawInstanced`、OpenGL `glDrawArraysInstanced`、Vulkan `vkCmdDraw` 的 `instanceCount` 等，思想一致：**一次调用，多实例**。

---

## 9. 推荐阅读关键词

**GPU Instancing**，**Graphics.DrawMeshInstanced**，**DrawMeshInstancedIndirect**，**MaterialPropertyBlock**，**SRP Batcher**，**UNITY_INSTANCING**，**RenderMeshInstanced**，**GPU Driven**。

---

*具体 API 重载、参数默认值、`camera` 为 null 时的精确行为，请以 **Unity 官方手册对应版本** 为准。*
