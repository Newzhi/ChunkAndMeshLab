using UnityEngine;

namespace NetGameTest.Data
{
    /// <summary>
    /// 单次图片拉取结果（纯数据，便于在 Proxy 与表现层之间传递）。
    /// </summary>
    public sealed class NetImageFetchResult
    {
        public bool IsSuccess { get; private set; }
        public Texture2D Texture { get; private set; }
        public string Error { get; private set; }
        public long HttpStatus { get; private set; }

        public static NetImageFetchResult Ok(Texture2D texture) =>
            new NetImageFetchResult
            {
                IsSuccess = true,
                Texture = texture
            };

        public static NetImageFetchResult Fail(string message, long httpStatus = 0) =>
            new NetImageFetchResult
            {
                IsSuccess = false,
                Error = message,
                HttpStatus = httpStatus
            };
    }
}
