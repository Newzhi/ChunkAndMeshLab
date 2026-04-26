using UnityEngine;

namespace NetGameTest.Config
{
    /// <summary>
    /// 网络演示用可复用配置（默认 URL、Sprite 参数等）。
    /// 在 Project 窗口：Create → TryNet → Net Game Config。
    /// </summary>
    [CreateAssetMenu(fileName = "NetGameConfig", menuName = "TryNet/Net Game Config", order = 0)]
    public class NetGameConfig : ScriptableObject
    {
        [SerializeField, Tooltip("当 NetRoot 未填写 URL 覆盖时使用")]
        private string defaultImageUrl = "https://www.baidu.com/img/flexible/logo/pc/result.png";

        [SerializeField, Tooltip("下载纹理转为 Sprite 时的 Pixels Per Unit")]
        private float spritePixelsPerUnit = 100f;

        [SerializeField, Tooltip("NetVideoRoot：未覆盖时使用；国内可试火山 CDN 样片（西瓜播放器文档用）")]
        private string defaultVideoUrl =
            "https://sf1-cdn-tos.huoshanstatic.com/obj/media-fe/xgplayer_doc_video/mp4/xgplayer-demo-360p.mp4";

        public string DefaultImageUrl => defaultImageUrl;
        public float SpritePixelsPerUnit => spritePixelsPerUnit;
        public string DefaultVideoUrl => defaultVideoUrl;
    }
}
