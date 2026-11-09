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

            // логин и пароль
            Console.Write("Введите логин (или нажмите Enter, если без БД): ");
            string login = Console.ReadLine();
            Console.Write("Введите пароль (или нажмите Enter, если без БД): ");
            string password = Console.ReadLine();

            // получение уникального кода
            Console.WriteLine("\nПодключение к серверу...");
            if (!RequestInitialToken(login, password))
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

        static bool RequestInitialToken(string login, string password)
        {
            string request = string.Format("CONNECT|{0}|{1}", login, password);
            string response = SendRequest(request);

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
            else if (status == "AUTH_FAIL")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Отказ сервера: Неверный логин или пароль.");
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
        
    }
}