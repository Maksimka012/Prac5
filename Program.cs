using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Man_License
{
    class Program
    {
        static string serverIp;
        static int serverPort;
        static string uniqueToken = string.Empty;
        static DateTime connectionStartTime;
        static string localIp = string.Empty;
        static int localPort = 0;
        static bool isRunning = true;

        static void Main(string[] args)
        {
            Console.Title = "Клиент менеджера лицензий";
            Console.WriteLine("=== Настройка подключения к серверу лицензий ===");

            Console.Write("Введите IP-адрес сервера (например, 127.0.0.1): ");
            serverIp = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(serverIp)) serverIp = "127.0.0.1";

            Console.Write("Введите порт сервера (например, 8888): ");
            while (!int.TryParse(Console.ReadLine(), out serverPort))
            {
                Console.Write("Некорректный ввод. Введите числовой порт: ");
            }

            Console.WriteLine("\nПодключение к серверу...");
            if (!RequestInitialToken())
            {
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                return;
            }

            Thread workerThread = new Thread(PeriodicServerCheck);
            workerThread.IsBackground = true;
            workerThread.Start();

            while (isRunning)
            {
                PrintClientInfo();
                Thread.Sleep(1000);
            }

            Console.WriteLine("\nСессия завершена. Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        static bool RequestInitialToken()
        {
            string response = SendRequest("CONNECT");

            string[] parts = response.Split('|');
            string status = parts[0];

            if (status == "OK" && parts.Length > 1)
            {
                uniqueToken = parts[1];
                connectionStartTime = DateTime.Now;
                return true;
            }
            else if (status == "NO_SLOTS")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Отказ сервера: Нет свободных мест для лицензирования.");
                Console.ResetColor();
            }
            else if (status == "BLACKLISTED")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Отказ сервера: Ваш клиент находится в чёрном списке!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка подключения к серверу: " + response);


                Console.ResetColor();
            }

            return false;
        }

        static void PeriodicServerCheck()
        {
            int secondsCounter = 0;

            while (isRunning)
            {
                Thread.Sleep(5000);
                secondsCounter += 5;

                string checkResponse = SendRequest("CHECK|" + uniqueToken);
                string[] parts = checkResponse.Split('|');

                if (parts[0] == "BLOCKED" || parts[0] == "BLACKLISTED")
                {
                    isRunning = false;
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ВНИМАНИЕ: Клиент заблокирован сервером (добавлен в чёрный список)!");
                    Console.ResetColor();
                    break;
                }
                else if (parts[0] == "KICKED")
                {
                    isRunning = false;
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("ВНИМАНИЕ: Сервер разорвал подключение.");
                    Console.ResetColor();
                    break;
                }
                else if (parts[0] == "EXPIRED" || secondsCounter >= 20)
                {
                    secondsCounter = 0;
                    string renewResponse = SendRequest("RENEW|" + uniqueToken);
                    string[] renewParts = renewResponse.Split('|');

                    if (renewParts[0] == "NEW_TOKEN" && renewParts.Length > 1)
                    {
                        uniqueToken = renewParts[1];
                    }
                    else
                    {
                        isRunning = false;
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Не удалось обновить уникальный код. Ответ сервера: " + renewResponse);
                        Console.ResetColor();
                        break;
                    }
                }
            }
        }

        static void PrintClientInfo()
        {
            if (!isRunning) return;

            TimeSpan duration = DateTime.Now - connectionStartTime;

            Console.Clear();
            Console.WriteLine("=== КЛИЕНТ МЕНЕДЖЕРА ЛИЦЕНЗИЙ (АКТИВЕН) ===");
            Console.WriteLine("Адрес сервера:               {0}:{1}", serverIp, serverPort);
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("Дата и время подключения:    {0:dd.MM.yyyy HH:mm:ss}", connectionStartTime);
            Console.WriteLine("Продолжительность сессии:    {0:hh\\:mm\\:ss}", duration);
            Console.WriteLine("IP-адрес клиента:            {0}", localIp);
            Console.WriteLine("Порт клиента:                {0}", localPort);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Уникальный код подключения:  {0}", uniqueToken);
            Console.ResetColor();
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine("Статус: Подключено. Автоматическая проверка активна...");
        }

        static string SendRequest(string message)
        {
            try
            {
                using (TcpClient client =

                    new TcpClient())
                {
                    client.Connect(serverIp, serverPort);

                    IPEndPoint localEndPoint = (IPEndPoint)client.Client.LocalEndPoint;
                    localIp = localEndPoint.Address.ToString();
                    if (localPort == 0)
                    {
                        localPort = localEndPoint.Port;
                    }

                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(message);
                        stream.Write(data, 0, data.Length);

                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERROR|" + ex.Message;
            }
        }
    }
}