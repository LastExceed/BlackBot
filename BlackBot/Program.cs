using System.IO;
using System;
using System.Threading;

namespace BlackBot {
    class Program {
        static void Main(string[] args) {
            new Thread(new ThreadStart(DiscordBot.Connect().GetAwaiter().GetResult));
            Console.Write("enter ip: ");
            string ip = Console.ReadLine();
            Console.Write("enter pw: ");
            string pw = Console.ReadLine();
            Console.Clear();
            while (true) {
                try {
                    BlackBot.Connect(ip, 12345, pw);
                }
                catch(Exception) {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("crash");
                }
            }
        }
    }
}
