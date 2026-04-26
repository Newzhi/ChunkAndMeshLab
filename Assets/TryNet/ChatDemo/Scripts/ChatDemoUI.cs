using UnityEngine;
using UnityEngine.UI;

namespace TryNet.ChatDemo
{
    /// <summary>
    /// 聊天 UI：主机 / 客户端、昵称、主机 IP、发送框。
    /// </summary>
    public class ChatDemoUI : MonoBehaviour
    {
        [SerializeField] ChatDemoTransport transport;

        [Header("UI")]
        [SerializeField] InputField hostAddressInput;
        [SerializeField] InputField nicknameInput;
        [SerializeField] InputField messageInput;
        [SerializeField] Button hostButton;
        [SerializeField] Button clientConnectButton;
        [SerializeField] Button sendButton;
        [SerializeField] Text logText;
        [SerializeField] int maxLogChars = 8000;

        [Header("默认")]
        [SerializeField] string defaultNicknamePc = "PC";
        [SerializeField] string defaultNicknameMobile = "手机";
        [SerializeField] string defaultHostIp = "127.0.0.1";

        void Awake()
        {
            if (transport == null)
                transport = FindObjectOfType<ChatDemoTransport>();

            if (nicknameInput != null && string.IsNullOrEmpty(nicknameInput.text))
                nicknameInput.text = Application.isMobilePlatform ? defaultNicknameMobile : defaultNicknamePc;

            if (hostAddressInput != null && string.IsNullOrEmpty(hostAddressInput.text))
                hostAddressInput.text = defaultHostIp;

            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);
            if (clientConnectButton != null)
                clientConnectButton.onClick.AddListener(OnClientClicked);
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);

            if (transport != null)
                transport.OnRemoteLine += AppendLog;
        }

        void OnDestroy()
        {
            if (transport != null)
                transport.OnRemoteLine -= AppendLog;
        }

        void OnHostClicked()
        {
            transport?.StartHost();
        }

        void OnClientClicked()
        {
            string ip = hostAddressInput != null ? hostAddressInput.text : "";
            transport?.StartClient(ip);
        }

        void OnSendClicked()
        {
            string nick = nicknameInput != null ? nicknameInput.text.Trim() : "User";
            if (string.IsNullOrEmpty(nick))
                nick = "User";
            string msg = messageInput != null ? messageInput.text.Trim() : "";
            if (string.IsNullOrEmpty(msg))
            {
                AppendLog("[提示] 请先输入要发送的内容");
                return;
            }

            transport?.SendLine($"{nick}: {msg}");
            if (messageInput != null)
                messageInput.text = "";
        }

        void AppendLog(string line)
        {
            if (logText == null)
                return;

            logText.text += line + "\n";
            if (logText.text.Length > maxLogChars)
                logText.text = logText.text.Substring(logText.text.Length - maxLogChars);
        }
    }
}
