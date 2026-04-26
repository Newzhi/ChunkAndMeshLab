# Unity 网络 API 与设计指南

本文面向在 Unity 里做「联网」的客户端开发者：先分清**你要解决的是哪一类问题**，再选 API，最后用清晰的分层把代码稳住。文中的代码示例均为**示意**，与具体工程目录或类名无关。

---

## 目录

1. [三类常见的「网络」需求](#1-三类常见的网络需求)
2. [HTTP 与资源下载：`UnityWebRequest`](#2-http-与资源下载unitywebrequest)
3. [多人游戏与实时同步](#3-多人游戏与实时同步)
4. [长连接：WebSocket 等](#4-长连接websocket-等)
5. [客户端架构与设计思路](#5-客户端架构与设计思路)
6. [应用场景速查表](#6-应用场景速查表)
7. [调试与常见坑](#7-调试与常见坑)

---

## 1. 三类常见的「网络」需求

很多人一说「Unity 网络」就混在一块，其实至少有三条完全不同的技术线：

| 类型 | 你在解决什么 | 典型问题 |
|------|----------------|----------|
| **A. HTTP / REST / 资源** | 请求某个 URL、拿 JSON、下文件、下 AssetBundle | 超时、缓存、HTTPS、弱网重试 |
| **B. 多人实时同步** | 多个客户端在同一局里看到一致或可接受的状态 | 谁说了算（权威）、延迟、作弊 |
| **C. 长连接 / 自定义协议** | 与服务器保持一条连接，双向推送 | 心跳、断线重连、协议版本 |

**选型原则：**  
- 只是「拉配置、登录、下一张图、下资源包」→ 走 **A**。  
- 「两个人在同一房间里互相看见」→ 走 **B**（专用框架），不要指望用 HTTP 轮询硬扛。  
- 「聊天室、实时推送、与自研网关长连」→ 走 **C**。

下面按类型展开 API 与例子。

---

## 2. HTTP 与资源下载：`UnityWebRequest`

### 2.1 它是什么

`UnityWebRequest` 是 Unity 官方推荐的 HTTP 客户端，用来发 GET/POST、附加 Header、下载或上传数据。旧版 `WWW` 应视为遗留，新项目优先使用本类。

### 2.2 为什么常用协程配合

`SendWebRequest()` 是异步的：在协程里写 `yield return request.SendWebRequest()`，可以在不阻塞主线程的情况下等待结束，然后在同一帧之后读取结果。

### 2.3 示例：GET 一段文本（例如服务器返回的 JSON 字符串）

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ExampleHttpGetText : MonoBehaviour
{
    [SerializeField] string apiUrl = "https://httpbin.org/get";

    IEnumerator Start()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(req.error);
                yield break;
            }

            string jsonOrText = req.downloadHandler.text;
            Debug.Log(jsonOrText.Substring(0, Mathf.Min(200, jsonOrText.Length)));
        }
    }
}
```

说明：

- `using` 确保请求对象被释放。  
- `UnityWebRequest.Result.Success` 在较新 Unity 版本中是推荐判断方式。  
- `httpbin.org` 是公网测试服务，仅作演示；正式环境换成你自己的后端地址。

### 2.4 示例：POST JSON

```csharp
IEnumerator PostJsonExample(string url, string jsonBody)
{
    using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
    {
        byte[] body = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(req.error);
            yield break;
        }

        Debug.Log(req.downloadHandler.text);
    }
}
```

### 2.5 示例：下载为 `Texture2D`（远程 PNG/JPG）

```csharp
IEnumerator DownloadImageAsTexture(string imageUrl)
{
    using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(imageUrl))
    {
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(req.error);
            yield break;
        }

        Texture2D tex = DownloadHandlerTexture.GetContent(req);
        // 再转成 Sprite、赋给 UI 或 SpriteRenderer 等，由展示层负责
    }
}
```

### 2.6 常用配套：`DownloadHandler` / `UploadHandler` 选型

| 处理器 | 适用场景 |
|--------|----------|
| `DownloadHandlerBuffer` | 原始字节、通用 |
| `DownloadHandlerText` | 明确当字符串用 |
| `DownloadHandlerTexture` | 图片 → `Texture2D` |
| `DownloadHandlerFile` | 大文件直写磁盘路径 |
| `UploadHandlerRaw` | 上传字节体（如 JSON、Protobuf） |

### 2.7 `HttpClient` 何时考虑

`System.Net.Http.HttpClient` 是标准 .NET 写法，适合复杂 HTTP、连接复用。在 Unity 中要额外注意 **目标平台**（例如 WebGL、部分 IL2CPP 限制）。若团队已统一用 `UnityWebRequest` 且需求不复杂，可不必引入 `HttpClient`。

---

## 3. 多人游戏与实时同步

### 3.1 与 HTTP 的本质区别

多人玩法要解决的是：**谁在什么时间以什么规则更新世界状态**。这涉及同步频率、预测、插值、服务器权威等，**不是**「多调几次 `Get` 就能解决」。

### 3.2 常见技术选型（概念层面）

- **官方路线**：如 **Netcode for GameObjects** 等（随 Unity 版本与文档更新），常与 Relay、Lobby 等按产品组合使用。  
- **第三方 / 商业**：如 Photon、Mirror、Fish-Net 等，差异在托管方式、收费模型、是否自建服务器。

### 3.3 设计时要问的问题（举例）

- 移动与射击：**服务器**是否最终裁定命中？  
- 延迟高时：是否做**客户端预测**、回放纠正？  
- 作弊：哪些状态**绝不能**只信客户端？

这些问题的答案决定架构，而不是某个 HTTP API。

---

## 4. 长连接：WebSocket 等

### 4.1 典型用途

- 聊天、通知、与网关保持会话  
- 服务端主动推送（避免客户端高频轮询）

### 4.2 实现途径（概念）

- .NET 的 `ClientWebSocket`（需确认 **构建目标** 是否支持）  
- 第三方库（带重连、心跳封装）  
- 原生插件（特定平台优化）

### 4.3 与 HTTP 的对比（直觉）

| | HTTP 请求 | WebSocket |
|--|-----------|-----------|
| 连接 | 通常短请求 | 长连接 |
| 推送 | 主要靠客户端拉 | 服务端可主动发 |
| 适用 | REST、下载 | 聊天、实时信令 |

---

## 5. 客户端架构与设计思路

### 5.1 分层（推荐思路）

用一个**贴近业务**的分层，避免把 URL、JSON 解析、按钮点击全写在一个 `MonoBehaviour` 里。

| 层 | 职责 | 举例 |
|----|------|------|
| **网络层 / Service** | 只负责发请求、解析 HTTP 结果、错误与超时 | `FetchUserProfileAsync` 返回数据或错误码 |
| **数据模型** | 配置、DTO、统一「成功/失败」结果对象 | 可序列化的配置资源、纯 C# 类 |
| **流程 / 用例** | 组合多次请求、决定重试与缓存策略 | 「登录 → 拉角色列表」 |
| **表现层** | UI、场景物体，只订阅「数据就绪」 | 收到回调后再刷新列表 |

好处：**换接口地址、加重试、换 UI**，多数时候只动一两层。

### 5.2 异步写法：协程 vs `async/await`

- **协程**：和 `MonoBehaviour` 生命周期天然合拍；注意对象销毁时停止协程，避免回调里访问已销毁物体。  
- **`async/await`**：逻辑更像「顺序代码」；在 Unity 中更新 UI、实例化物体仍须在**主线程**执行，常通过调度器或 `UniTask` 等库约束。

### 5.3 配置与代码分离

把环境相关的量放到**可编辑资源**或远程配置里，例如：

- 正式服 / 测试服 Base URL  
- 请求超时秒数  
- 功能开关  

这样打不同包或热更配置时不必改脚本。

### 5.4 示例：分层后的「伪代码」形状（无具体工程名）

```csharp
// 网络层：只返回「结果对象」，不碰 UI
public static class UserApiClient
{
    public static IEnumerator FetchDisplayName(string userId, System.Action<string> onOk, System.Action<string> onFail)
    {
        string url = $"https://api.example.com/users/{userId}";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onFail?.Invoke(req.error);
                yield break;
            }
            onOk?.Invoke(req.downloadHandler.text);
        }
    }
}

// 表现层：挂在一个物体上，负责按钮与显示
public class ProfilePanel : MonoBehaviour
{
    public void OnRefreshClicked()
    {
        StartCoroutine(UserApiClient.FetchDisplayName(
            "123",
            text => { /* 更新 Text */ },
            err => { Debug.LogError(err); }));
    }
}
```

真实项目里会把 URL、错误码枚举、JSON 反序列化再细化；这里只展示**职责边界**。

---

## 6. 应用场景速查表

| 你想做…… | 优先考虑 |
|----------|----------|
| 拉一段 JSON / XML | `UnityWebRequest` + `DownloadHandlerText` / Buffer |
| 下载图片显示在 UI | `UnityWebRequestTexture` → `Texture2D` → `Sprite`（展示层决定 PPU） |
| 下载 AssetBundle | `UnityWebRequest` 拿字节或 `GetAssetBundle`，再 `AssetBundle.LoadAsset` |
| 用户登录、提交分数 | HTTPS + POST + Token Header |
| 同屏多人 | 专用同步框架（Netcode / 第三方） |
| 聊天、推送 | WebSocket 或 IM SDK |
| 极简单局域网探测 | 少数情况 `UdpClient` 广播（仍建议封装好协议与错误处理） |

---

## 7. 调试与常见坑

### 7.1 Player 设置

- **Internet Access**：需要访问公网时，在 Player 设置中设为 **Require**（具体路径随 Unity 版本在 Project Settings → Player）。  
- **Android**：注意网络权限与网络安全配置（明文 HTTP 限制等）。  
- **WebGL**：浏览器环境限制多，CORS、HTTPS、部分 API 不可用或行为不同，需查当前版本文档。

### 7.2 HTTPS 与证书

证书错误、自签名证书在真机上容易失败；开发机与生产环境要区分处理策略（仅调试环境放宽等）。

### 7.3 线程与主线程

`UnityWebRequest` 的回调与协程续体一般在 Unity 主线程继续；若使用线程池或 `async` 库，**不要**在后台线程直接改 `Transform`、UI。

### 7.4 对象生命周期

界面关闭或场景卸载后，取消未完成的请求或忽略迟到的回调，避免空引用。

### 7.5 超时与弱网

为 `UnityWebRequest` 设置合理的 `timeout`；必要时做重试与退避，避免无限重试打爆服务器或耗电。

---

## 结语

- **HTTP / 资源** → 以 `UnityWebRequest` 为核心，配好分层与配置。  
- **多人实时** → 选专用网络栈，从「权威与同步模型」入手。  
- **长连接** → WebSocket 或网关 SDK，单独设计心跳与重连。

文档随 Unity 版本会略有差异，编写与发布前请以 **当前 Editor 版本对应的 Unity Manual** 为准。
