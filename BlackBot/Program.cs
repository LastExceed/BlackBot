using System.IO;
using System;

namespace ETbot {
    class Program {
        static void Main(string[] args) {
            Console.Write("enter pw: ");
            string pw = Console.ReadLine();
            Console.Clear();
            while (true) {
                try {
                    BlackBot.Connect("localhost", 12345, pw);
                }
                catch(StackOverflowException) {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("crash");
                }
            }
        }
    }
}
