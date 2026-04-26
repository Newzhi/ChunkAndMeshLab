# NetGameTest：网络下载演示（图片 + 视频）

- **图片**：按钮下载远程图 → 绑定物体上的 `SpriteRenderer` 显示；日志前缀 **`[NetGame]`**（`NetRoot`）。  
- **视频**：按钮下载 MP4 等到本地缓存 → `VideoPlayer` 播放；日志前缀 **`[NetVideo]`**（`NetVideoRoot`）。  

二者共用 **`NetGameConfig`**（图片 URL + 视频 URL）与 **`NetProxy`**（纹理 / 文件下载）。

---

## 一、配置说明（先读这段）

### 1. `NetGameConfig` 资源（推荐）

用于放**默认**下载地址和 Sprite 参数，可多场景复用。

| 步骤 | 操作 |
|------|------|
| 创建 | Project 窗口右键 → **Create → TryNet → Net Game Config** |
| 命名 | 例如 `NetGameConfig_Default` |
| **Default Image Url** | 填完整 HTTPS 图片地址（需可在浏览器直接打开） |
| **Sprite Pixels Per Unit** | 下载图转成 `Sprite` 时的 PPU，一般保持 `100`，图太大/太小再调 |
| **Default Video Url** | `NetVideoRoot` 用：可直链下载的 **MP4**（建议 H.264），默认使用公开示例片 |

将资源拖到 **`NetRoot` / `NetVideoRoot`** 各自的 **Config** 槽（可共用同一资源）。

### 2. `NetRoot` 上的 Model 字段

| 字段 | 含义 |
|------|------|
| **Config** | 可选。若赋值，则使用其中的默认 URL 与 PPU（PPU 在无 Config 时用下方 `Sprite Pixels Per Unit`）。 |
| **Url Override** | 可选。若**非空**，**优先**于 `NetGameConfig.Default Image Url`，用于临时改地址、调试。 |
| **Sprite Pixels Per Unit** | 仅当 **未挂 Config** 时，用作生成 Sprite 的 PPU；挂了 Config 则以 Config 为准。 |

**URL 生效优先级：** `Url Override`（非空）→ `NetGameConfig.Default Image Url` → 若都无效则报错。

### 3. `NetRoot` 上的 View / Controller（场景绑定）

| 字段 | 含义 |
|------|------|
| **Image Display Target** | **必填。** 拖入**另一个**负责显示图片的 **GameObject**（例如场景里的 `Target` 精灵）。脚本会在该物体上 `GetComponent<SpriteRenderer>`，没有则在其**子节点**上查找。 |
| **Download Button** | 可选。拖 UI **Button**；也可留空，在 Button 的 **OnClick** 里绑定 **`NetRoot.OnDownloadButtonClick`**。 |

**注意：** `Image Display Target` **不要**拖成挂有 **同一 `NetRoot`** 的物体；逻辑根与展示根应分离。

---

## 二、快速上手（验证功能）

### 1. 工程设置

1. **Edit → Project Settings → Player → Other Settings**  
2. **Internet Access** → **Require**（需访问公网时）

### 2. 场景最小步骤

1. 新建或使用一个空物体作为**逻辑根**（例如 `GameRoot`），挂载 **`NetRoot`**。  
2. 另有一个带 **`SpriteRenderer`** 的物体作为**显示用**（例如 `Target`），把该物体的 **GameObject** 拖到 **`Image Display Target`**。  
3. 配置 URL：要么拖入 **NetGameConfig**，要么只填 **Url Override**。  
4. 绑定 **Button**（或 OnClick 指向 `OnDownloadButtonClick`）。  
5. **Play**，点击按钮，看 **Target** 是否换图、Console 是否出现 `[NetGame]` 日志。

### 3. 示例 URL

- `https://www.baidu.com/img/flexible/logo/pc/result.png`  
  也可换成其它可直接 **GET** 的图片 HTTPS 地址。

### 4. 常见问题

| 现象 | 可检查项 |
|------|----------|
| 提示 View 未就绪 / 找不到 SpriteRenderer | **Image Display Target** 是否指向带 `SpriteRenderer` 的物体（或子节点有） |
| 提示不应与 NetRoot 同物体 | 显示对象与挂 `NetRoot` 的对象分开 |
| 无网络 / 下载失败 | Internet Access、URL 能否在浏览器打开 |
| 有日志但看不见图 | 摄像机是否照到 **显示物体**、层级是否被挡 |

---

## 四、视频下载与播放（`NetVideoRoot`）

### 1. 场景布置

1. 新建**逻辑物体**，挂载 **`NetVideoRoot`**（不要和播放物体用同一个）。  
2. **播放目标**：另建一个 **GameObject**，添加 **`VideoPlayer`**，按需要设置：
   - **Render Mode**：例如 **Camera Far Plane**（须指定 **Target Camera**；预制体 **`Target.prefab`** 上为空时，由 **`NetVideoTargetBootstrap`** 在运行时填 **Camera.main**）。  
   - 需要声音时，在同一物体上加 **Audio Source**，并在 VideoPlayer 的 **Target Audio Sources** 里指向该组件（**`Target.prefab`** 已内置引用）。  
3. 把该播放物体拖到 **`Video Play Target`**。  
4. 拖 **Button** 到 **`Download Play Button`**，或 OnClick 绑定 **`NetVideoRoot.OnDownloadAndPlayClicked`**。  
5. **Config**：拖入与图片演示共用的 **`NetGameConfig`**（使用其中的 **Default Video Url**），或在 **`Video Url Override`** 里临时填地址。  
6. **Local File Name**：仅文件名（默认 `demo_clip.mp4`），文件落在 **`Assets/TryNet/NetGameTest/VideoDownloads/`**（编辑器下即项目里该文件夹）；打包后为 `应用*_Data/TryNet/NetGameTest/VideoDownloads/`。再次下载前脚本会 **Stop 并清空 VideoPlayer.url** 以释放占用，避免覆盖失败。

### 2. 运行与验证

进入 Play → 点击按钮 → Console 看 **`[NetVideo]`** 日志（下载路径、文件大小、`file://` 播放地址）→ 画面中应开始播放。

### 3. 注意

| 项 | 说明 |
|----|------|
| 格式 | 优先 **H.264 + MP4**；部分平台对编码挑剔，准备失败可换短样片测试。 |
| 体积 | 大文件下载耗时久；仅作演示时可换 Config 里的小体积样片 URL。 |
| 网络 | 仍需 **Internet Access: Require**；HTTPS 直链需服务器允许下载。 |
| 解码失败 | 看 Console 中 `VideoPlayer` 报错；可改用平台支持的编码或 **`VideoPlayer` 直接 Url 流式**（本 Demo 为「先落盘再播」）。 |

---

## 三、设计思路

### 职责划分

- **`NetRoot`（Controller）**  
  挂在**管理用**物体上，处理按钮、读取 **Config / Url Override**、驱动协程、打日志。  
  **不**假设贴图显示在自己身上。

- **展示（View）**  
  由 **`Image Display Target`** 指定**其它**游戏对象；运行时解析其 **`SpriteRenderer`** 再赋值。这样 UI/场景层级与网络逻辑解耦，换展示物体只改引用。

- **Model**  
  **`NetGameConfig`**：可复用的默认图片 URL、PPU、默认视频 URL。  
  **`NetImageFetchResult` / `NetFileDownloadResult`**：单次下载结果数据。  
  **`Url Override`**：场景级覆盖，方便调试。

- **`NetProxy`**  
  HTTP：`DownloadTexture`（图片）、`DownloadToFile`（视频等到本地）；无场景引用。  
- **`NetVideoRoot`**  
  与 `NetRoot` 并列：下载到 **`Application.dataPath/TryNet/NetGameTest/VideoDownloads/`**（见上），再驱动 **`VideoPlayer`**。

### 可配置

- 默认行为以 **ScriptableObject** 为主，场景用 **Url Override** 做临时覆盖，避免改资源文件也能试不同地址。

### 日志

- 图片：**`[NetGame]`**（按钮、`SpriteRenderer`、URL、贴图结果）。  
- 视频：**`[NetVideo]`**（按钮、`VideoPlayer`、本地路径、播放状态）。

### 目录结构

```
Assets/TryNet/NetGameTest/
  Config/     NetGameConfig.cs
  Data/       NetImageFetchResult.cs, NetFileDownloadResult.cs
  Proxy/      NetProxy.cs
  NetRoot/    NetRoot.cs
  NetVideo/   NetVideoRoot.cs
```

---

## 相关入口

- 图片逻辑：**`NetRoot.cs`**
- 视频逻辑：**`NetVideoRoot.cs`**
- 配置资源：**Create → TryNet → Net Game Config**
