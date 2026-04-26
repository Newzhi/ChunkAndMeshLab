using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TryNet.ChatDemo
{
    /// <summary>
    /// 简易 TCP 聊天传输：Host 在本机监听，Client 连到 Host IP。
    /// 适合 PC（主机）+ Android（客户端）同一 WiFi；Editor 也可当 Host 或 Client 自测。
    /// </summary>
    public class ChatDemoTransport : MonoBehaviour
    {
        [SerializeField] int port = 47000;

        readonly ConcurrentQueue<string> _incomingLines = new();
        readonly List<TcpClient> _remoteClients = new();
        readonly object _clientsLock = new();

        TcpListener _listener;
        Thread _acceptThread;
        TcpClient _clientConnection;
        Thread _clientReadThread;

        public event Action<string> OnRemoteLine;

        public int Port => port;

        void Update()
        {
            while (_incomingLines.TryDequeue(out string line))
                OnRemoteLine?.Invoke(line);
        }

        /// <summary>本机作主机：监听所有网卡，手机填电脑的局域网 IP 连接。</summary>
        public void StartHost()
        {
            StopTransport();
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                PushLine($"[系统] 主机已监听 0.0.0.0:{port}，请客户端连接本机局域网 IP。");
                _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
                _acceptThread.Start();
            }
            catch (Exception e)
            {
                PushLine($"[错误] 无法监听: {e.Message}");
            }
        }

        void AcceptLoop()
        {
            try
            {
                while (_listener != null)
                {
                    var tcp = _listener.AcceptTcpClient();
                    lock (_clientsLock)
                        _remoteClients.Add(tcp);
                    PushLine($"[系统] 新连接: {tcp.Client.RemoteEndPoint}");
                    var t = new Thread(() => HostReadLoop(tcp)) { IsBackground = true };
                    t.Start();
                }
            }
            catch (SocketException)
            {
                /* StopHost */
            }
            catch (Exception e)
            {
                PushLine($"[错误] Accept: {e.Message}");
            }
        }

        void HostReadLoop(TcpClient fromClient)
        {
            try
            {
                using var stream = fromClient.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    PushLine(line);
                    // 含发送方，便于手机端也显示自己发的内容
                    HostBroadcastRawLine(line, except: null);
                }
            }
            catch
            {
                /* disconnect */
            }
            finally
            {
                lock (_clientsLock)
                {
                    _remoteClients.Remove(fromClient);
                    try
                    {
                        fromClient.Close();
                    }
                    catch
                    {
                        /* ignore */
                    }
                }

                PushLine("[系统] 一方已断开");
            }
        }

        void HostBroadcastRawLine(string line, TcpClient except)
        {
            byte[] data = Encoding.UTF8.GetBytes(line + "\n");
            List<TcpClient> snapshot;
            lock (_clientsLock)
                snapshot = _remoteClients.ToList();

            foreach (var c in snapshot)
            {
                if (except != null && c == except)
                    continue;
                if (!c.Connected)
                    continue;
                try
                {
                    c.GetStream().Write(data, 0, data.Length);
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        /// <summary>连接到主机（手机填电脑 WiFi 的 IPv4）。</summary>
        public void StartClient(string hostAddress)
        {
            StopTransport();
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                PushLine("[错误] 主机地址为空");
                return;
            }

            string host = hostAddress.Trim();
            var t = new Thread(() => ClientConnectThread(host)) { IsBackground = true };
            t.Start();
        }

        void ClientConnectThread(string host)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.Connect(host, port);
                _clientConnection = client;
                PushLine($"[系统] 已连接 {host}:{port}");
                ClientReadLoop();
            }
            catch (Exception e)
            {
                PushLine($"[错误] 连接失败: {e.Message}");
                try
                {
                    client?.Close();
                }
                catch
                {
                    /* ignore */
                }

                _clientConnection = null;
            }
        }

        void ClientReadLoop()
        {
            var client = _clientConnection;
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                    PushLine(line);
            }
            catch
            {
                /* disconnect */
            }
            finally
            {
                PushLine("[系统] 与主机断开");
                try
                {
                    client?.Close();
                }
                catch
                {
                    /* ignore */
                }

                if (ReferenceEquals(_clientConnection, client))
                    _clientConnection = null;
            }
        }

        /// <summary>发送一行 UTF-8 文本（末尾自动换行）。主机广播给所有客户端；客户端发给主机。</summary>
        public void SendLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            byte[] data = Encoding.UTF8.GetBytes(line + "\n");

            if (_listener != null)
            {
                PushLine(line);
                List<TcpClient> snapshot;
                lock (_clientsLock)
                    snapshot = _remoteClients.ToList();

                foreach (var c in snapshot)
                {
                    if (!c.Connected)
                        continue;
                    try
                    {
                        c.GetStream().Write(data, 0, data.Length);
                    }
                    catch
                    {
                        /* ignore */
                    }
                }
            }
            else if (_clientConnection != null && _clientConnection.Connected)
            {
                try
                {
                    _clientConnection.GetStream().Write(data, 0, data.Length);
                }
                catch (Exception e)
                {
                    PushLine($"[错误] 发送失败: {e.Message}");
                }
            }
            else
                PushLine("[错误] 未连接：请先启动主机或连接主机");
        }

        void PushLine(string line) => _incomingLines.Enqueue(line);

        public void StopTransport()
        {
            try
            {
                _listener?.Stop();
            }
            catch
            {
                /* ignore */
            }

            _listener = null;

            lock (_clientsLock)
            {
                foreach (var c in _remoteClients)
                {
                    try
                    {
                        c.Close();
                    }
                    catch
                    {
                        /* ignore */
                    }
                }

                _remoteClients.Clear();
            }

            try
            {
                _clientConnection?.Close();
            }
            catch
            {
                /* ignore */
            }

            _clientConnection = null;
        }

        void OnDestroy() => StopTransport();

        public bool IsHost => _listener != null;
        public bool IsClientConnected => _clientConnection != null && _clientConnection.Connected;
    }
}
