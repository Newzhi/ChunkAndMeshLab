# ChatDemo：PC ↔ 手机 简易 TCP 聊天

同一 **WiFi** 下，**电脑端当主机（监听）**，**Android 当客户端** 连电脑的局域网 IP，双向发文字。协议为 **一行一条 UTF-8 消息**（`昵称: 内容`）。

## 原理

- **主机（PC / Editor）**：`TcpListener` 绑定 `0.0.0.0:端口`（默认 **47000**），接受多个客户端；收到任一端的一行，显示在本机并 **广播给所有已连接客户端**。
- **客户端（手机等）**：`TcpClient` 连 `主机IPv4:端口`；收发的都是 **换行分隔** 的文本。
- 不涉及 HTTP，一般 **无需** Android 明文 HTTP 配置；需 **网络权限**（Unity Android 默认通常已包含 `INTERNET`）。

## 场景搭建

### 快捷生成（推荐）

1. 新建空场景并保存（如 `ChatDemo.unity`）到本目录。
2. 菜单 **TryNet → ChatDemo → 生成场景 UI（当前场景）**。
3. 会自动创建 **Canvas、EventSystem、ChatRoot（含 Transport + UI 引用）、输入框与按钮**。保存场景后即可 Play。

### 手动搭建（约 5 分钟）

1. 新建场景 `ChatDemo`（或任意场景），保存到本目录。
2. 创建 **Canvas**（Screen Space - Overlay），加 **EventSystem**（右键 UI → Event System）。
3. 空物体 **`ChatRoot`**，挂 **`ChatDemoTransport`**，**Port** 保持 `47000`（与防火墙放行一致）。
4. 同一物体或子物体挂 **`ChatDemoUI`**，把 **`Transport`** 指向上面的组件。
5. 在 Canvas 下建 UI（均为 Unity 内置 **uGUI**）：
   - **InputField**「主机 IP」→ 绑定 `Host Address Input`（手机填电脑 `ipconfig` 里的 IPv4；本机双开测试可填 `127.0.0.1`）。
   - **InputField**「昵称」→ `Nickname Input`。
   - **Button**「当主机」→ `Host Button`。
   - **Button**「连接主机」→ `Client Connect Button`。
   - **InputField**「消息」→ `Message Input`。
   - **Button**「发送」→ `Send Button`。
   - **Text**（建议放在 **Scroll View** 的 Content 上）→ `Log Text`，锚点拉伸，**Horizontal Overflow / Vertical Overflow** 设为 Overflow 或配合 Content Size Fitter。

## 运行流程

### 电脑 + 手机（同一 WiFi）

1. **电脑**：`ipconfig` 查看 **无线局域网适配器** 的 **IPv4**（如 `192.168.1.5`）。
2. **电脑**：运行游戏或 Editor，点 **「当主机」**，看到日志里提示已监听。
3. **Windows 防火墙**：若手机连不上，放行 **入站 TCP 47000**（或临时关闭防火墙验证）。
4. **手机**：安装 APK，**主机 IP** 填上一步的 IPv4，点 **「连接主机」**，出现「已连接」后再发消息。
5. **电脑** 侧也可填同一 IP（若本机也当客户端自测可 `127.0.0.1`）；主机发消息会显示在主机日志并发给所有客户端。

### 仅本机双开测试

1. 先启动一个进程点 **「当主机」**。
2. 再启动第二个（第二个 Editor 实例或 Build），**主机 IP** 填 **`127.0.0.1`**，点 **「连接主机」**。

## 打包注意

- **Android**：**Build Settings** 勾选 **Internet Access: Require**（若项目未开）。
- **PC**：与手机需 **同一局域网**；若用笔记本开热点，手机连热点，主机 IP 用该热点的网关 IP（仍以 `ipconfig` 为准）。

## 脚本说明

| 脚本 | 作用 |
|------|------|
| `ChatDemoTransport.cs` | TCP 主机/客户端、线程读包、主线程通过队列刷新 UI 事件 |
| `ChatDemoUI.cs` | 按钮、输入框、日志文本绑定 |

## 扩展建议

- 生产环境请加重连、心跳、TLS、鉴权与消息长度限制；本示例仅作学习演示。
