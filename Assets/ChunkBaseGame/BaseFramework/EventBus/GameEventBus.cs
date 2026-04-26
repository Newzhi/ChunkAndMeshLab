using System;
using System.Collections.Generic;

namespace BaseFramework.EventBus
{
    public static class GameEventBus
    {
        //字典存储结构，list方便后续对重复绑定排查整理
        private static readonly Dictionary<Type, List<Delegate>> Handles = new();

        //订阅方法，一般应用可以写在对应的初始化周期上
        public static void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            var type = typeof(T);
            if (!Handles.TryGetValue(type, out var handlers))
            {
                handlers = new List<Delegate>();
                Handles[type] = handlers;
            }
            // 避免重复订阅导致重复回调
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        //一般写在析构或者销毁方法上
        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            var type = typeof(T);
            if (Handles.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    Handles.Remove(type);
                }
            }
        }

        //消息的发布，被调用者会执行对应的方法
        public static void Publish<T>(T eventArgs) where T : IGameEvent
        {
            var type = typeof(T);
            if (Handles.TryGetValue(type, out var handlers))
            {
                // 发布期间允许订阅/退订：复制一份，避免迭代时集合被修改抛异常。
                var snapshot = handlers.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    if (snapshot[i] is Action<T> action)
                    {
                        action.Invoke(eventArgs);
                    }
                }
            }
        }
    }
}