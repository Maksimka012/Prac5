using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        static List<ClientSession> clients = new List<ClientSession>();
        static List<string> blacklist = new List<string>();
        static int maxClient;
        static int tokenLifetimeSec;

        static void Main(string[] args)
        {
            Console.Title = "Сервер менеджера лицензий";
            Console.Write("Введите IP адрес сервера (Enter для 127.0.0.1): ");
            string ip = Console.ReadLine();
            if (string.IsNullOrEmpty(ip))
            {
                ip = "127.0.0.1";
            }
            Console.Write("Введите порт сервера: ");
            int port = int.Parse(Console.ReadLine());

            Console.Write("Кол-во доступных лицензий: ");
            maxClient = int.Parse(Console.ReadLine());

            Console.Write("Время обновления уникального кода в сек.: ");
            tokenLifetimeSec = int.Parse(Console.ReadLine());

            Console.Write("Время подтверждения кода в сек.: ");
            int confirmTime = int.Parse(Console.ReadLine());

            TcpListener server = new TcpListener(IPAddress.Parse(ip), port);
            server.Start();
            new Thread(() => ListenClients(server)) { IsBackground = true }.Start();

            Console.WriteLine($"\nСервер запущен на {ip}:{port}");
            Console.WriteLine("Команды: list - список клиентов | kick <код> - отключить  | ban <код> - ЧС");

            while (true)
            {
                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }
                string[] cmd = input.Split(' ');
                if (cmd[0] == "list")
                {
                    var active = clients.Where(x => !x.isKicked).ToList();
                    Console.WriteLine($"Подключенно клиентов: {active.Count} из {maxClient}");

                    foreach (var client in active)
                    {
                        TimeSpan duration = DateTime.Now - client.ConnectTime;
                        string connectStr = client.ConnectTime.ToString("dd.MM.yyyy HH:mm:ss");
                        string durationStr = duration.ToString(@"hh\:mm\:ss");

                        Console.WriteLine($"Код: {client.Token} | IP: {client.Ip}:{client.Port} | Вход: {connectStr} | Длительность: {durationStr}");
                    }
                }
                else if (cmd[0] == "kick" && cmd.Length > 1)
                {
                    var client = clients.FirstOrDefault(c => c.Token == cmd[1]);
                    if (client != null)
                    {
                        client.isKicked = true;
                        Console.WriteLine("Клиент отключен");
                    }
                }
                else if (cmd[0] == "ban" && cmd.Length > 1)
                {
                    var client = clients.FirstOrDefault(c => c.Token == cmd[1]);
                    if (client != null)
                    {
                        blacklist.Add(client.Token);
                        blacklist.Add(client.Ip);
                        client.isKicked = true;
                        Console.WriteLine($"Клиент {client.Ip} (код: {client.Token}) добавлен в черный список!");
                    }
                }
            }
        }
        static void ListenClients(TcpListener server)
        {
            while (true)
            {


                using (TcpClient client = server.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    IPEndPoint ep = (IPEndPoint)client.Client.RemoteEndPoint;
                    byte[] buf = new byte[1024];
                    int len = stream.Read(buf, 0, buf.Length);
                    string req = Encoding.UTF8.GetString(buf, 0, len).Trim();

                    string resp = ProcessRequest(req, ep.Address.ToString(), ep.Port);
                    byte[] data = Encoding.UTF8.GetBytes(resp);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        static string ProcessRequest(string req, string ip, int port)
        {
            string[] parts = req.Split('|');
            string cmd = parts[0];

            if (blacklist.Contains(ip)) return "BLACKLISTED";

            if (cmd == "CONNECT")
            {
                if (clients.Count(x => !x.isKicked) >= maxClient)
                    return "NO_SLOTS";

                string token = Guid.NewGuid().ToString().Substring(0, 8);
                clients.Add(new ClientSession
                {
                    Token = token,
                    Ip = ip,
                    Port = port,
                    ConnectTime = DateTime.Now,
                    TokenCreatedTime = DateTime.Now
                });
                Console.WriteLine($"[+] Подключился клиент {ip}:{port} -> Код: {token}");
                return "OK|" + token;
            }
            else if (cmd == "CHECK" && parts.Length > 1)
            {
                string token = parts[1];
                if (blacklist.Contains(token)) return "BLACKLISTED";

                var c = clients.FirstOrDefault(x => x.Token == token);
                if (c == null || c.isKicked) return "KICKED";

                if ((DateTime.Now - c.TokenCreatedTime).TotalSeconds >= tokenLifetimeSec)
                    return "EXPIRED";

                return "ACTIVE";
            }
            else if (cmd == "RENEW" && parts.Length > 1)
            {
                var c = clients.FirstOrDefault(x => x.Token == parts[1] && !x.isKicked);
                if (c == null) return "KICKED";

                c.Token = Guid.NewGuid().ToString().Substring(0, 8);
                c.TokenCreatedTime = DateTime.Now;
                Console.WriteLine($"[~] Обновлен код для {c.Ip}:{c.Port} -> Новый код: {c.Token}");
                return "NEW_TOKEN|" + c.Token;
            }

            return "ERROR";
        }
    }
}