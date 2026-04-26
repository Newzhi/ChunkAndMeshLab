namespace NetGameTest.Data
{
    /// <summary>
    /// 文件下载结果（本地路径或错误信息）。
    /// </summary>
    public sealed class NetFileDownloadResult
    {
        public bool IsSuccess { get; private set; }
        public string LocalPath { get; private set; }
        public string Error { get; private set; }
        public long HttpStatus { get; private set; }

        public static NetFileDownloadResult Ok(string localPath) =>
            new NetFileDownloadResult { IsSuccess = true, LocalPath = localPath };

        public static NetFileDownloadResult Fail(string message, long httpStatus = 0) =>
            new NetFileDownloadResult { IsSuccess = false, Error = message, HttpStatus = httpStatus };
    }
}
