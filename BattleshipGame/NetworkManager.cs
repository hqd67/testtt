using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BattleshipGame
{
    public class NetworkManager
    {
        private TcpListener server;
        private TcpClient client;
        private NetworkStream stream;

        public event Action<string> MessageReceived;

        public async Task StartServer(int port)
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            client = await server.AcceptTcpClientAsync();
            stream = client.GetStream();
            _ = Listen();
        }

        public async Task ConnectToServer(string ip, int port)
        {
            client = new TcpClient();
            await client.ConnectAsync(ip, port);
            stream = client.GetStream();
            _ = Listen();
        }

        public async Task SendMessage(object msg)
        {
            if (stream == null) return;
            string json = System.Text.Json.JsonSerializer.Serialize(msg);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task Listen()
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytes == 0) break;
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytes).Trim();
                    MessageReceived?.Invoke(msg);
                }
                catch { break; }
            }
        }
    }
}
