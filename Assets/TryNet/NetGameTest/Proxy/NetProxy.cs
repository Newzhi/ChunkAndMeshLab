using System;
using System.Collections;
using System.IO;
using NetGameTest.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace NetGameTest.Proxy
{
    /// <summary>
    /// 网络代理：仅负责请求与结果封装，不依赖场景对象。
    /// 由 <see cref="NetRoot"/>、<see cref="NetVideoRoot"/> 等 <c>StartCoroutine</c> 驱动。
    /// </summary>
    public static class NetProxy
    {
        /// <summary>
        /// 将 URL 内容下载到本地文件（适合视频等大文件，避免整文件进内存）。
        /// </summary>
        public static IEnumerator DownloadToFile(string url, string absolutePath, Action<NetFileDownloadResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                onComplete?.Invoke(NetFileDownloadResult.Fail("URL 为空"));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                onComplete?.Invoke(NetFileDownloadResult.Fail("保存路径为空"));
                yield break;
            }

            // 混用 / 与 \ 时 DownloadHandlerFile 在 Windows 上可能 ArgumentException；必须规范为完整本地路径
            absolutePath = Path.GetFullPath(absolutePath.Trim());
            string dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(absolutePath))
            {
                try
                {
                    File.Delete(absolutePath);
                }
                catch (Exception e)
                {
                    onComplete?.Invoke(NetFileDownloadResult.Fail($"无法覆盖已有文件: {e.Message}"));
                    yield break;
                }
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(absolutePath) { removeFileOnAbort = true };
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(NetFileDownloadResult.Fail(
                        $"{request.error} (HTTP {request.responseCode})",
                        request.responseCode));
                    yield break;
                }

                onComplete?.Invoke(NetFileDownloadResult.Ok(absolutePath));
            }
        }

        public static IEnumerator DownloadTexture(string url, Action<NetImageFetchResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                onComplete?.Invoke(NetImageFetchResult.Fail("URL 为空"));
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(NetImageFetchResult.Fail(
                        $"{request.error} (HTTP {request.responseCode})",
                        request.responseCode));
                    yield break;
                }

                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                if (tex == null)
                {
                    onComplete?.Invoke(NetImageFetchResult.Fail("响应体无法解析为纹理", request.responseCode));
                    yield break;
                }

                onComplete?.Invoke(NetImageFetchResult.Ok(tex));
            }
        }

        public static Sprite TextureToSprite(Texture2D texture, float pixelsPerUnit = 100f)
        {
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }
    }
}
