using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BattleshipGame
{
    // Простой асинхронный NetworkManager.
    // Каждый JSON-пакет отправляется как строка + '\n'.
    public class NetworkManager
    {
        private TcpListener listener;
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;

        public bool IsConnected => client?.Connected ?? false;
        public bool IsServer { get; private set; } = false;

        // Событие: пришёл JSON-текст сообщения
        public event Action<string> MessageReceived;

        // Запуск сервера и ожидание клиента
        public async Task StartServer(int port)
        {
            if (listener != null) throw new InvalidOperationException("Server already started.");
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            IsServer = true;
            client = await listener.AcceptTcpClientAsync();
            SetupStreams();
            _ = ReceiveLoop();
        }

        // Подключение к серверу
        public async Task ConnectToServer(string ip, int port)
        {
            if (client != null) throw new InvalidOperationException("Already connected.");
            client = new TcpClient();
            await client.ConnectAsync(ip, port);
            IsServer = false;
            SetupStreams();
            _ = ReceiveLoop();
        }

        private void SetupStreams()
        {
            var ns = client.GetStream();
            reader = new StreamReader(ns, Encoding.UTF8);
            writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };
        }

        // Асинхронная отправка объекта (сериализуется в JSON и завершается '\n')
        public async Task SendAsync(object obj)
        {
            if (writer == null) return;
            string json = JsonSerializer.Serialize(obj);
            try
            {
                await writer.WriteLineAsync(json);
            }
            catch
            {
                // ignore
            }
        }

        // Приём сообщений: читаем построчно
        private async Task ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null) break;
                    MessageReceived?.Invoke(line);
                }
            }
            catch
            {
                // connection closed
            }
            finally
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            try { reader?.Dispose(); } catch { }
            try { writer?.Dispose(); } catch { }
            try { client?.Close(); } catch { }
            try { listener?.Stop(); } catch { }
            reader = null;
            writer = null;
            client = null;
            listener = null;
            IsServer = false;
        }
    }
}
