using System.Threading;
using Cysharp.Threading.Tasks;

namespace BaseFramework.Async
{
    /// <summary>
    /// 轻量版异步 AutoResetEvent（UniTask 版本兼容）。
    /// 语义：Set() 唤醒一个等待者；若无人等待则记一次信号，下一次 WaitAsync 直接通过。
    /// </summary>
    public sealed class AsyncAutoResetEventLite
    {
        private UniTaskCompletionSource core;
        private int signaled; // 0/1

        public AsyncAutoResetEventLite(bool initialState = false)
        {
            signaled = initialState ? 1 : 0;
        }

        public UniTask WaitAsync()
        {
            // 若已有信号，消费掉并立即返回
            if (Interlocked.Exchange(ref signaled, 0) == 1)
            {
                return UniTask.CompletedTask;
            }

            // 否则挂起等待
            var c = core;
            if (c == null)
            {
                c = new UniTaskCompletionSource();
                Interlocked.CompareExchange(ref core, c, null);
                c = core;
            }

            return c.Task;
        }

        public void Set()
        {
            // 若有人在等，唤醒一个；否则记为已 signaled
            var c = Interlocked.Exchange(ref core, null);
            if (c != null)
            {
                c.TrySetResult();
                return;
            }

            Interlocked.Exchange(ref signaled, 1);
        }
    }
}

