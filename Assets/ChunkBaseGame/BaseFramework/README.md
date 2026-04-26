# BaseFramework 说明：异步与事件

本文档约定 `Assets/BaseFramework` 内的通用用法，目的是让项目里所有“异步/事件”写法风格一致、避免常见坑。

---

## 异步（UniTask）基本操作

本项目异步以 **UniTask** 为主（用于把 IO/生成/网格化等耗时任务移出主线程），但要牢记：**Unity 大部分 API 只能在主线程调用**。

### 常用模式

- **把阻塞操作丢到线程池**
  - 典型：文件读写、数据压缩/解压、体素生成、网格顶点数组构建（纯数据）。
  - 使用：`await UniTask.RunOnThreadPool(() => { ... })`
- **把结果应用回主线程**
  - 典型：创建/销毁 `GameObject`、修改 `Transform`、创建/赋值 `Mesh`、访问 `Resources/Addressables/YooAsset` 的 Unity 对象等。
  - 使用：回到 PlayerLoop 再做 Unity API 操作（不要在 `RunOnThreadPool` 内调用 Unity API）。
- **取消（Cancellation）**
  - 典型：玩家快速移动导致旧 chunk 的加载任务过期。
  - 做法：每个加载链路持有 `CancellationToken`，在关键步骤检查并尽早退出。
- **异常处理**
  - 异步链路必须在入口处捕获异常并记录，避免静默失败导致状态卡死。
  - fire-and-forget（`.Forget()`）只能用于“允许失败且内部已处理异常”的后台循环/监听任务。

---

## 异步允许的操作 / 不允许的操作

### 允许的操作（建议放线程池）

- **文件 IO**
  - `File.ReadAllText/WriteAllText`、二进制读写、目录创建等。
- **纯计算**
  - 噪声、体素填充、索引计算、构建顶点/索引数组、压缩/解压、序列化（若不依赖 Unity API）。
- **数据结构整理**
  - 合并队列、去重、生成中间缓存（例如 `byte[]/int[]/Vector3[]`）。

### 不允许的操作（禁止放线程池）

以下内容若放到线程池，容易出现崩溃、数据竞争或“偶发 bug”：

- **任何 UnityEngine 对象的创建/销毁/访问**
  - `GameObject/Transform/Component/Mesh/Material/Texture` 等的创建、赋值、读取属性。
  - `Instantiate/Destroy`、`GetComponent`、`transform.position = ...`、`mesh.vertices = ...` 等。
- **Unity 的大部分静态 API**
  - 例如很多 `UnityEngine.*` 调用都默认要求主线程（部分纯数学结构例外，但不建议赌“某个版本恰好没事”）。
- **依赖主线程上下文的逻辑**
  - 与帧循环强绑定的状态机推进、UI 更新、Gizmos 绘制等。

### “能跑但不推荐”的操作（需要明确边界）

- **序列化/反序列化**
  - 如果使用 `JsonUtility` 等 Unity 相关实现：建议在主线程执行序列化，把“写盘”放线程池。
  - 如果换成纯 .NET 序列化库：可以考虑放线程池，但要保证不会触发 Unity API。

---

## 事件机制（GameEventBus）

事件用于解耦模块：**发布者不关心谁在监听**，订阅者也不需要直接引用发布者的具体实现。

本项目的事件总线位于：
- `Assets/BaseFramework/EventBus/IGameEvent.cs`
- `Assets/BaseFramework/EventBus/GameEventBus.cs`

### 事件定义

- 所有事件都实现 `framework.IGameEvent`
- 推荐每个事件是“小数据载体”，只包含必要字段（避免把大对象/Unity 引用塞进事件）

示例（建议写在 `Assets/ChunkTest/Chunk/Events/` 或你的模块内 Events 文件夹）：

```csharp
using framework;

public struct ChunkLoadedEvent : IGameEvent
{
    public long ChunkId;
    public int ChunkX;
    public int ChunkZ;
}
```

### 订阅 / 退订

- 订阅建议写在 `OnEnable`，退订写在 `OnDisable`（或 `OnDestroy`），避免对象销毁后仍被回调（内存泄漏/空引用）。

```csharp
using framework;
using UnityEngine;

public class ChunkDebugListener : MonoBehaviour
{
    private void OnEnable()
    {
        GameEventBus.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<ChunkLoadedEvent>(OnChunkLoaded);
    }

    private void OnChunkLoaded(ChunkLoadedEvent e)
    {
        Debug.Log($"Chunk loaded: {e.ChunkId} ({e.ChunkX},{e.ChunkZ})");
    }
}
```

### 发布（Publish）

- 发布点应放在“状态已经确定”的位置，避免订阅者拿到半初始化对象。
- 事件处理函数应尽量短小；耗时操作请转为异步或投递到队列。

```csharp
GameEventBus.Publish(new ChunkLoadedEvent { ChunkId = id, ChunkX = x, ChunkZ = z });
```

---

## 事件系统的注意事项（很重要）

- **不要在事件里传 UnityEngine 引用作为长期持有对象**
  - 尤其不要把 `Transform/GameObject` 存到全局单例或长期缓存里；如需定位对象，传 `Id`/坐标/路径等可重建信息更稳。
- **避免事件递归/风暴**
  - 事件 A 的处理函数里又发布事件 A 或链式触发大量事件，会造成难以追踪的调用栈与性能尖峰。
- **订阅一定要配对退订**
  - 事件总线是静态全局，忘记退订就是典型内存泄漏来源。
- **线程约束**
  - `GameEventBus.Publish` 设计上默认在主线程调用；如果你要从后台线程发布事件，必须先切回主线程或确保订阅者不会触发 Unity API（通常不建议在后台发布）。

