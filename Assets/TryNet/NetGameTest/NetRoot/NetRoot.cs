using System.Collections;
using NetGameTest.Config;
using NetGameTest.Data;
using NetGameTest.Proxy;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NetGameTest
{
    /// <summary>
    /// Controller：挂在场景中的「逻辑根」物体上（可与按钮、画布同层级）；不负责自身显示。
    /// 下载完成后，在<strong>另行绑定的显示物体</strong>上取 <see cref="SpriteRenderer"/> 并赋图。
    /// </summary>
    public class NetRoot : MonoBehaviour
    {
        private const string LogTag = "[NetGame]";

        [Header("Model · 可配置数据")]
        [SerializeField]
        private NetGameConfig config;

        [Tooltip("若填写则覆盖 Config 中的默认图片地址")]
        [SerializeField, FormerlySerializedAs("imageUrl")]
        private string urlOverride;

        [Header("View · 展示目标（其它游戏对象）")]
        [Tooltip("拖入<strong>负责显示图片</strong>的物体（须带 SpriteRenderer，或在子节点上）；不要拖挂有本 NetRoot 的同一物体。")]
        [SerializeField]
        private GameObject imageDisplayTarget;

        [Header("Controller · 输入")]
        [SerializeField]
        private Button downloadButton;

        [Tooltip("无 Config 时用于生成 Sprite 的 Pixels Per Unit")]
        [SerializeField]
        private float spritePixelsPerUnit = 100f;

        public Texture2D LastTexture { get; private set; }

        private Sprite _runtimeSprite;

        private void Awake()
        {
            if (downloadButton != null)
            {
                downloadButton.onClick.AddListener(OnDownloadButtonClick);
                Debug.Log($"{LogTag} 已绑定下载按钮。");
            }
            else
                Debug.LogWarning($"{LogTag} 未指定 Button，请在 Inspector 指定或手动把 OnDownloadButtonClick 绑到按钮上。");
        }

        private void OnDestroy()
        {
            if (downloadButton != null)
                downloadButton.onClick.RemoveListener(OnDownloadButtonClick);

            if (_runtimeSprite != null)
                Destroy(_runtimeSprite);
        }

        public void OnDownloadButtonClick()
        {
            Debug.Log($"{LogTag} --- 点击下载 ---");
            StopAllCoroutines();
            StartCoroutine(DownloadAndShowFlow());
        }

        private IEnumerator DownloadAndShowFlow()
        {
            if (!TryResolveViewSpriteRenderer(out SpriteRenderer viewSr))
                yield break;

            string url = ResolveImageUrl(out string urlSource);
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogError($"{LogTag} Model 无有效 URL：填写 Url Override 或在 Config 中设置默认地址。");
                yield break;
            }

            float ppu = config != null ? config.SpritePixelsPerUnit : spritePixelsPerUnit;
            Debug.Log($"{LogTag} 使用地址 ({urlSource}): {url}");
            Debug.Log($"{LogTag} Sprite PPU = {ppu}");

            Debug.Log($"{LogTag} 开始下载（Proxy）…");
            NetImageFetchResult result = null;
            yield return StartCoroutine(NetProxy.DownloadTexture(url, r => result = r));

            if (result == null || !result.IsSuccess)
            {
                Debug.LogError($"{LogTag} 下载失败: {result?.Error ?? "无结果"}");
                yield break;
            }

            Texture2D tex = result.Texture;
            LastTexture = tex;
            Debug.Log($"{LogTag} 下载完成 Model 就绪: 纹理 {tex.width}×{tex.height}");

            ReplaceViewSprite(viewSr, tex, ppu);
            Debug.Log($"{LogTag} View 已替换为网络图片（对象「{imageDisplayTarget.name}」），流程结束。");
        }

        /// <summary>在绑定物体上解析 SpriteRenderer：优先本节点，否则子节点。</summary>
        private bool TryResolveViewSpriteRenderer(out SpriteRenderer viewSr)
        {
            viewSr = null;

            if (imageDisplayTarget == null)
            {
                Debug.LogError($"{LogTag} View 未就绪：请在 Inspector 指定 Image Display Target（用于显示下载图的游戏对象，非本 NetRoot 所在物体）。");
                return false;
            }

            if (imageDisplayTarget == gameObject)
            {
                Debug.LogError($"{LogTag} View 配置不当：Image Display Target 不应为挂有 NetRoot 的同一物体，请指定单独的显示对象。");
                return false;
            }

            viewSr = imageDisplayTarget.GetComponent<SpriteRenderer>()
                     ?? imageDisplayTarget.GetComponentInChildren<SpriteRenderer>(true);

            if (viewSr == null)
            {
                Debug.LogError($"{LogTag} View：在「{imageDisplayTarget.name}」及其子节点上未找到 SpriteRenderer。");
                return false;
            }

            Debug.Log($"{LogTag} View：已从「{imageDisplayTarget.name}」解析到 SpriteRenderer（用于贴图）。");
            return true;
        }

        private string ResolveImageUrl(out string source)
        {
            if (!string.IsNullOrWhiteSpace(urlOverride))
            {
                source = "Inspector Url Override";
                return urlOverride.Trim();
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.DefaultImageUrl))
            {
                source = "NetGameConfig";
                return config.DefaultImageUrl.Trim();
            }

            source = "无";
            return string.Empty;
        }

        private void ReplaceViewSprite(SpriteRenderer viewSr, Texture2D texture, float pixelsPerUnit)
        {
            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
                Debug.Log($"{LogTag} View: 已销毁上一张运行时 Sprite。");
            }

            _runtimeSprite = NetProxy.TextureToSprite(texture, pixelsPerUnit);
            viewSr.sprite = _runtimeSprite;
            Debug.Log($"{LogTag} View: 已在「{viewSr.gameObject.name}」的 SpriteRenderer 上赋值 sprite。");
        }
    }
}
