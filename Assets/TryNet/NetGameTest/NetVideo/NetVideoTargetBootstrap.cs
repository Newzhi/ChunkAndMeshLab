using UnityEngine;
using UnityEngine.Video;

namespace NetGameTest
{
    /// <summary>
    /// 挂在带 <see cref="VideoPlayer"/> 的载体上：预制体里无法引用场景摄像机时，
    /// 在运行时若 Target Camera 为空则使用 <see cref="Camera.main"/>。
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class NetVideoTargetBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var vp = GetComponent<VideoPlayer>();
            if (vp == null)
                return;

            if (vp.renderMode == VideoRenderMode.CameraFarPlane || vp.renderMode == VideoRenderMode.CameraNearPlane)
            {
                if (vp.targetCamera == null)
                    vp.targetCamera = Camera.main;
            }
        }
    }
}
