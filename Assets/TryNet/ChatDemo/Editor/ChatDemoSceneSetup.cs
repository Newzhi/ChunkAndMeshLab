#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TryNet.ChatDemo;

namespace TryNet.ChatDemo.Editor
{
    /// <summary>
    /// 编辑器菜单：一键在当前场景生成 ChatDemo UI。
    /// 步骤拆分为独立方法，便于后续换布局、加变体菜单或从其他 Editor 工具复用。
    /// </summary>
    public static class ChatDemoSceneSetup
    {
        public const string MenuPath = "TryNet/ChatDemo/生成场景 UI（当前场景）";

        /// <summary>
        /// 布局常量集中一处，改尺寸/位置或做「高密度/横屏」变体时可复制此类再换数值。
        /// </summary>
        static class Layout
        {
            public static readonly Vector2 HostIpPos = new Vector2(0, -40);
            public static readonly Vector2 HostIpSize = new Vector2(400, 40);
            public static readonly Vector2 NicknamePos = new Vector2(0, -100);
            public static readonly Vector2 NicknameSize = new Vector2(200, 40);
            public static readonly Vector2 MessagePos = new Vector2(0, -420);
            public static readonly Vector2 MessageSize = new Vector2(520, 44);
            public static readonly Vector2 BtnHostPos = new Vector2(-200, -160);
            public static readonly Vector2 BtnClientPos = new Vector2(80, -160);
            public static readonly Vector2 BtnSendPos = new Vector2(300, -420);
            public static readonly Vector2 LogScrollPos = new Vector2(0, -220);
            public static readonly Vector2 LogScrollSize = new Vector2(640, 280);
        }

        [MenuItem(MenuPath)]
        static void CreateChatDemoUi()
        {
            EnsureEventSystemExists();
            Canvas canvas = EnsureCanvasExists();
            GetOrCreateChatRoot(out ChatDemoTransport transport, out ChatDemoUI ui);

            WireTransportToUi(ui, transport);
            BuildUiUnderCanvas(canvas.transform, ui);
            MarkActiveSceneDirty();
            Debug.Log("ChatDemo：已在当前场景生成 Canvas / ChatRoot / UI，保存场景后即可运行。");
        }

        static void EnsureEventSystemExists()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        static Canvas EnsureCanvasExists()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null)
                return canvas;

            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return canvas;
        }

        static void GetOrCreateChatRoot(out ChatDemoTransport transport, out ChatDemoUI ui)
        {
            var root = GameObject.Find("ChatRoot");
            if (root == null)
            {
                root = new GameObject("ChatRoot");
                Undo.RegisterCreatedObjectUndo(root, "Create ChatRoot");
            }

            transport = root.GetComponent<ChatDemoTransport>();
            if (transport == null)
                transport = Undo.AddComponent<ChatDemoTransport>(root);

            ui = root.GetComponent<ChatDemoUI>();
            if (ui == null)
                ui = Undo.AddComponent<ChatDemoUI>(root);
        }

        static void WireTransportToUi(ChatDemoUI ui, ChatDemoTransport transport)
        {
            var serial = new SerializedObject(ui);
            serial.FindProperty("transport").objectReferenceValue = transport;
            serial.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildUiUnderCanvas(Transform canvasTransform, ChatDemoUI ui)
        {
            Transform ct = canvasTransform;

            var hostIp = CreateInputField(ct, "HostIpInput", "主机 IP（手机填电脑 IPv4）", Layout.HostIpPos, Layout.HostIpSize);
            var nick = CreateInputField(ct, "NicknameInput", "昵称", Layout.NicknamePos, Layout.NicknameSize);

            var btnHost = CreateButton(ct, "BtnHost", "当主机", Layout.BtnHostPos);
            var btnClient = CreateButton(ct, "BtnClient", "连接主机", Layout.BtnClientPos);

            // 日志区必须在「消息输入 / 发送」之前加入层级，否则 ScrollRect 会盖住下方控件，导致无法聚焦输入、按钮偶发无响应。
            var logText = CreateLogArea(ct, "LogScroll", Layout.LogScrollPos, Layout.LogScrollSize);

            var msg = CreateInputField(ct, "MessageInput", "消息…", Layout.MessagePos, Layout.MessageSize);
            var btnSend = CreateButton(ct, "BtnSend", "发送", Layout.BtnSendPos);

            var serial = new SerializedObject(ui);
            serial.FindProperty("hostAddressInput").objectReferenceValue = hostIp;
            serial.FindProperty("nicknameInput").objectReferenceValue = nick;
            serial.FindProperty("messageInput").objectReferenceValue = msg;
            serial.FindProperty("hostButton").objectReferenceValue = btnHost.GetComponent<Button>();
            serial.FindProperty("clientConnectButton").objectReferenceValue = btnClient.GetComponent<Button>();
            serial.FindProperty("sendButton").objectReferenceValue = btnSend.GetComponent<Button>();
            serial.FindProperty("logText").objectReferenceValue = logText;
            serial.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MarkActiveSceneDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.15f);

            var input = go.AddComponent<InputField>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 6);
            textRt.offsetMax = new Vector2(-10, -6);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = Color.white;
            text.supportRichText = false;
            input.textComponent = text;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10, 6);
            phRt.offsetMax = new Vector2(-10, -6);
            var ph = phGo.AddComponent<Text>();
            ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ph.fontSize = 22;
            ph.color = new Color(1, 1, 1, 0.45f);
            ph.text = placeholder;
            input.placeholder = ph;

            Undo.RegisterCreatedObjectUndo(go, "InputField " + name);
            return input;
        }

        static GameObject CreateButton(Transform parent, string name, string label, Vector2 pos)
        {
            var existing = parent.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            Undo.RegisterCreatedObjectUndo(go, "Button " + name);
            return go;
        }

        static Text CreateLogArea(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var existing = parent.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var scrollGo = new GameObject(name);
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.5f, 1f);
            scrollRt.anchorMax = new Vector2(0.5f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 1f);
            scrollRt.anchoredPosition = pos;
            scrollRt.sizeDelta = size;

            var scroll = scrollGo.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var ctRt = content.AddComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0, 1);
            ctRt.anchorMax = new Vector2(1, 1);
            ctRt.pivot = new Vector2(0.5f, 1);
            ctRt.anchoredPosition = Vector2.zero;
            ctRt.sizeDelta = new Vector2(0, 0);

            var logText = content.AddComponent<Text>();
            logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            logText.fontSize = 20;
            logText.color = Color.white;
            logText.alignment = TextAnchor.UpperLeft;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = vpRt;
            scroll.content = ctRt;
            scroll.horizontal = false;
            scroll.vertical = true;

            Undo.RegisterCreatedObjectUndo(scrollGo, "Log Scroll");
            return logText;
        }
    }
}
#endif
