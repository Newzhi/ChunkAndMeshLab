using System.Collections;
using System.IO;
using NetGameTest.Config;
using NetGameTest.Data;
using NetGameTest.Proxy;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace NetGameTest
{
    /// <summary>
    /// 视频演示 Controller：下载到本地缓存后，在绑定物体的 <see cref="VideoPlayer"/> 上播放。
    /// </summary>
    public class NetVideoRoot : MonoBehaviour
    {
        private const string LogTag = "[NetVideo]";

        [Header("Model · 可配置数据")]
        [SerializeField]
        private NetGameConfig config;

        [Tooltip("若填写则覆盖 Config 中的 Default Video Url")]
        [SerializeField]
        private string videoUrlOverride;

        [Tooltip("仅文件名，例如 demo_clip.mp4；实际目录为项目 Assets/TryNet/NetGameTest/VideoDownloads/（编辑器）或包内 TryNet/NetGameTest/VideoDownloads/（打包后）")]
        [SerializeField]
        private string localFileName = "demo_clip.mp4";

        [Header("View · 播放目标（其它游戏对象）")]
        [Tooltip("拖入带 VideoPlayer 的物体（或子节点上有）；不要拖挂有本 NetVideoRoot 的同一物体。")]
        [SerializeField]
        private GameObject videoPlayTarget;

        [Header("Controller · 输入")]
        [SerializeField]
        private Button downloadPlayButton;

        private VideoPlayer _videoPlayer;

        /// <summary>由 <see cref="VideoPlayer.errorReceived"/> 置位，部分 Unity 版本无 <c>VideoPlayer.error</c> 属性。</summary>
        private bool _videoPlayerLastError;

        private void Awake()
        {
            if (downloadPlayButton != null)
            {
                downloadPlayButton.onClick.AddListener(OnDownloadAndPlayClicked);
                Debug.Log($"{LogTag} 已绑定下载并播放按钮。");
            }
            else
                Debug.LogWarning($"{LogTag} 未指定 Button，请把 OnDownloadAndPlayClicked 绑到 UI 按钮。");
        }

        private void OnDestroy()
        {
            if (downloadPlayButton != null)
                downloadPlayButton.onClick.RemoveListener(OnDownloadAndPlayClicked);

            if (_videoPlayer != null)
                _videoPlayer.errorReceived -= OnVideoError;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            _videoPlayerLastError = true;
            Debug.LogError($"{LogTag} VideoPlayer 错误: {message}");
        }

        public void OnDownloadAndPlayClicked()
        {
            Debug.Log($"{LogTag} --- 点击：下载并播放 ---");
            StopAllCoroutines();
            StartCoroutine(DownloadAndPlayFlow());
        }

        private IEnumerator DownloadAndPlayFlow()
        {
            if (!TryResolveVideoPlayer(out VideoPlayer vp))
                yield break;

            // 释放上一段播放对本地 MP4 的占用，否则覆盖下载会报「文件被另一进程使用」
            yield return ReleaseLocalVideoFile(vp);

            string url = ResolveVideoUrl(out string urlSource);
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogError($"{LogTag} 无有效视频 URL：填写 Video Url Override 或在 NetGameConfig 中设置 Default Video Url。");
                yield break;
            }

            string savePath = BuildVideoSavePath();
            Debug.Log($"{LogTag} 使用地址 ({urlSource}): {url}");
            Debug.Log($"{LogTag} 保存到: {savePath}");

            Debug.Log($"{LogTag} 开始下载文件（Proxy）…");
            NetFileDownloadResult fileResult = null;
            yield return StartCoroutine(NetProxy.DownloadToFile(url, savePath, r => fileResult = r));

            if (fileResult == null || !fileResult.IsSuccess)
            {
                Debug.LogError($"{LogTag} 下载失败: {fileResult?.Error ?? "无结果"}");
                yield break;
            }

            Debug.Log($"{LogTag} 下载完成，文件大小约 {new FileInfo(savePath).Length / 1024} KB");

            string playUrl = ToVideoPlayerUrl(savePath);
            Debug.Log($"{LogTag} 准备播放 URL: {playUrl}");

            vp.Stop();
            vp.source = VideoSource.Url;
            vp.url = playUrl;
            vp.errorReceived -= OnVideoError;
            vp.errorReceived += OnVideoError;

            _videoPlayerLastError = false;
            vp.Prepare();
            const float prepareTimeoutSec = 30f;
            float deadline = Time.realtimeSinceStartup + prepareTimeoutSec;
            while (!vp.isPrepared && !_videoPlayerLastError && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (_videoPlayerLastError)
                yield break;

            if (!vp.isPrepared)
            {
                Debug.LogError($"{LogTag} 准备超时（{prepareTimeoutSec}s）或解码不支持，请换 H.264 MP4 测试。");
                yield break;
            }

            vp.Play();
            Debug.Log($"{LogTag} 已开始播放（对象「{videoPlayTarget.name}」），流程结束。");
        }

        private bool TryResolveVideoPlayer(out VideoPlayer vp)
        {
            vp = null;

            if (videoPlayTarget == null)
            {
                Debug.LogError($"{LogTag} 未指定 Video Play Target。");
                return false;
            }

            if (videoPlayTarget == gameObject)
            {
                Debug.LogError($"{LogTag} Video Play Target 不应为挂有本 NetVideoRoot 的同一物体。");
                return false;
            }

            vp = videoPlayTarget.GetComponent<VideoPlayer>()
                 ?? videoPlayTarget.GetComponentInChildren<VideoPlayer>(true);

            if (vp == null)
            {
                Debug.LogError($"{LogTag} 在「{videoPlayTarget.name}」上未找到 VideoPlayer。");
                return false;
            }

            _videoPlayer = vp;
            Debug.Log($"{LogTag} 已解析到 VideoPlayer（「{vp.gameObject.name}」）。");
            return true;
        }

        /// <summary>
        /// 编辑器：<c>Assets/TryNet/NetGameTest/VideoDownloads/</c>；运行时 dataPath 为 Assets 或 *_Data。
        /// </summary>
        private string BuildVideoSavePath()
        {
            string safeName = Path.GetFileName(localFileName.Trim());
            if (string.IsNullOrEmpty(safeName))
                safeName = "demo_clip.mp4";

            return Path.Combine(Application.dataPath, "TryNet", "NetGameTest", "VideoDownloads", safeName);
        }

        private static IEnumerator ReleaseLocalVideoFile(VideoPlayer vp)
        {
            if (vp == null)
                yield break;

            vp.Stop();
            vp.url = string.Empty;
            vp.source = VideoSource.Url;
            yield return null;
            yield return null;
        }

        private string ResolveVideoUrl(out string source)
        {
            if (!string.IsNullOrWhiteSpace(videoUrlOverride))
            {
                source = "Inspector Video Url Override";
                return videoUrlOverride.Trim();
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.DefaultVideoUrl))
            {
                source = "NetGameConfig.DefaultVideoUrl";
                return config.DefaultVideoUrl.Trim();
            }

            source = "无";
            return string.Empty;
        }

        /// <summary>本地绝对路径转为 VideoPlayer 可用的 file:// URI。</summary>
        private static string ToVideoPlayerUrl(string absolutePath)
        {
            absolutePath = Path.GetFullPath(absolutePath);
            return new System.Uri(absolutePath).AbsoluteUri;
        }
    }
}
