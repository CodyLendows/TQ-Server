using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    class Program
    {
        public static Server server = new Server();
        private static bool halted = false;

        private static ConsoleColor bgCol = ConsoleColor.Black;
        private static ConsoleColor fgCol = ConsoleColor.Gray;

        private static string ID; // use for directed commands (control flags)
        private static bool IsServerReady = true; // Tracks if the server sent #Available#{ID}, i.e. we're allowed to transmit

        static void Main(string[] args)
        {
            Console.BackgroundColor = bgCol;
            Console.ForegroundColor = fgCol;

            Task serverTask = Task.Run(() => server.Start());

            // If interactive, start our client handler.
            if (Config.Interactive)
            {
                TcpClient tcpClient = new TcpClient();
                tcpClient.Connect("localhost", Config.Port);

                // Get network stream 
                NetworkStream stream = tcpClient.GetStream();

                // For reading from the network stream
                Task.Run(() => ReceiveData(stream));

                // Initial handshake
                Transmit(stream, "af3hin"/* magic number! except there's only 1 number in it. */);

                // Inappropriately named, this is actually the main loop for sending commands.
                SendData(stream);

                tcpClient.Close();
            }
            else
            {
                // Non-interactive server. Listen to Server logs instead.
                while (true)
                {
                    Console.ReadKey();
                }
            }
        }

        static bool firstHandshake = true;
        static void ReceiveData(NetworkStream stream)
        {
            try
            {
                // Create a buffer to read data
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Convert received bytes to string and print it
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    #region Special Cases
                    if (firstHandshake)
                    {
                        try
                        {
                            ID = message.Split('\n')[4].Split(" ")[1];
                            firstHandshake = false;
                        }
                        catch { }

                        try
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                Console.WriteLine(message.Split('\n')[i]);
                            }
                        }
                        catch { Console.WriteLine("Internal error occurred during integration."); }

                        continue;
                    }

                    if (message.Contains("#Available#" + ID))
                    {
                        IsServerReady = true; // Server is ready for the next command
                        message = message.Replace("#Available#" + ID, "");
                    }

                    if (message.Contains($"#RaiseFatal#{ID}"))
                    {
                        string fatalMessage = message.Remove(0, $"#RaiseFatal#{ID}".Length);
                        DisplayFatalMessage(fatalMessage);
                        break;
                    }

                    if (message.Contains($"#PassWdFollows#{ID}"))
                    {
                        ExpectSecure = true;
                        message = message.Replace($"#PassWdFollows#{ID}", "");
                    }

                    #endregion

                    Console.Write(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error receiving data: " + ex.Message);
            }
        }

        static void DisplayFatalMessage(string message)
        {
            try
            {
                int originalTop = Console.CursorTop;
                int originalLeft = Console.CursorLeft;

                Console.SetCursorPosition(0, 0);

                int messageLength = message.Length;
                int screenWidth = Console.WindowWidth;
                int paddingLeft = (screenWidth - messageLength) / 2;

                while (true)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.SetCursorPosition(paddingLeft, 0);
                    Console.Write(message);
                    Task.Delay(1000).Wait();

                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.SetCursorPosition(paddingLeft, 0);
                    Console.Write(message);
                    Task.Delay(1000).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying fatal message: " + ex.Message);
            }
        }

        static bool ExpectSecure = false;
        static void SendData(NetworkStream stream)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine();
                Transmit(stream, $"#ready#{ID}");
            };

            try
            {
                while (true)
                {
                    if (!IsServerReady)
                    {
                        Task.Delay(100).Wait(); // Wait for the server to indicate readiness
                        continue;
                    }

                    string message = string.Empty;

                    if (!ExpectSecure)
                    {
                        message = Console.ReadLine();
                    }
                    else
                    {
                        ExpectSecure = false;
                        message = ReadPassword('*').ToString();
                    }

                    if (message == null) { message = ""; }

                    /*if (message.ToLower() == "exit")
                    {
                        Console.WriteLine("!! Shutting down internal server !!");
                        server.Stop();
                        break;
                    }
                    else */
                    if (message.ToLower() == "clear")
                    {
                        Console.BackgroundColor = bgCol;
                        Console.ForegroundColor = fgCol;
                        Console.Clear();
                        Transmit(stream, $"#ready#{ID}");
                        continue;
                    }

                    IsServerReady = false; // Reset readiness until the server signals again
                    Transmit(stream, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending data: " + ex.Message);
            }
        }

        static void Transmit(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }

        public static string ReadPassword(char mask)
        {
            const int ENTER = 13, BACKSP = 8, CTRLBACKSP = 127;
            int[] FILTERED = { 0, 27, 9, 10 };

            var pass = new Stack<char>();
            char chr = (char)0;

            while ((chr = System.Console.ReadKey(true).KeyChar) != ENTER)
            {
                if (chr == BACKSP)
                {
                    if (pass.Count > 0)
                    {
                        System.Console.Write("\b \b");
                        pass.Pop();
                    }
                }
                else if (chr == CTRLBACKSP)
                {
                    while (pass.Count > 0)
                    {
                        System.Console.Write("\b \b");
                        pass.Pop();
                    }
                }
                else if (FILTERED.Count(x => chr == x) > 0) { }
                else
                {
                    pass.Push((char)chr);
                    System.Console.Write(mask);
                }
            }

            System.Console.WriteLine();

            return new string(pass.Reverse().ToArray());
        }
    }
}
