using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace TQServer
{
    public class Server
    {
        private readonly List<Client> Clients = new List<Client>();
        private readonly object LockObj = new object();
        private static List<string> Logs = new List<string>();

        TcpListener server = new TcpListener(IPAddress.Any, Config.Port);

        public void Start()
        {
            if (Config.PerformConfigAuditOnStart)
            {
                Server.WriteLine("Auditing server configuration...");
                Config.AuditConfig();
            }

            Server.WriteLine("Starting server...");

            try
            {
                server.Start();
                Server.WriteLine($"Server started on port {Config.Port}");

                while (true)
                {
                    TcpClient tcpClient = server.AcceptTcpClient();
                    Server.WriteLine($"Client connected from {tcpClient.Client.RemoteEndPoint}");

                    Client client = new Client(tcpClient);

                    if (Config.AllowAnonymous) { client.User = UserTree.Anonymous; }
                    else { client.User = new User(); }

                    // For InternalOnly mode, block non-localhost connections
                    if (Config.InternalOnlyMode) 
                    { 
                        if (tcpClient.Client.RemoteEndPoint.ToString().Split(":")[0] != "localhost" && 
                            tcpClient.Client.RemoteEndPoint.ToString().Split(":")[0] != "127.0.0.1") 
                        { 
                            Server.WriteLine("Not authorized (internal only mode)"); 
                            client.Send("You are not whitelisted on this server."); 
                            client.TcpClient.Close(); 
                            break; 
                        } 
                    }

                    lock (LockObj)
                    {
                        Clients.Add(client);
                    }

                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(client);
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                server.Stop();
            }
        }

        public void Stop()
        {
            Server.WriteLine("Shutting down internal server!");
            foreach (Client c in Clients) { c.Send("The server is shutting down!"); c.TcpClient.Close(); }

            server.Stop();
        }

        private void HandleClient(object clientObj)
        {
            Client client = (Client)clientObj;
            NetworkStream stream = client.Stream;
            byte[] buffer = new byte[1024];

            TDF_Collection tdf = new TDF_Collection(stream, client, buffer);

            client.Send(GetServerID(client.ID) + "\n");

            client.Free();
            client.SendPrompt();

            try
            {
                while (true)
                {
                    client.Last_TDF = tdf;
                    Recv(tdf);
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine($"Error with client {client.ID}: {ex.Message}");
            }
            finally
            {
                lock (LockObj)
                {
                    Clients.Remove(client);
                }

                Server.WriteLine($"Client {client.ID} disconnected.");
                stream.Close();
                client.TcpClient.Close();
            }
        }

        public bool ToLive = false;
        public void Recv(TDF_Collection tdf)
        {
            Stream stream = tdf.stream;
            Client client = tdf.client;
            byte[] buffer = tdf.buffer;

            int bytesRead = stream.Read(buffer, 0, buffer.Length); ;

            string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            if (client.Quirks == "NA") { if (command != "af3hin") { client.Quirks = "YA"; } else { client.Quirks = "NO"; return; } }

            Server.WriteLine($"Received from {client.ID}@{client.TcpClient.Client.RemoteEndPoint}: {command}");

            // If live prompt command, invoke client.
            if (ToLive) { client.InvokeReplyToLivePrompt(command); return; }

            // Pass to core handler
            string result = string.Empty;

            if (!command.Contains($"#ready#{client.ID}") && (command.Length != 0)) result = CoreHandler.PerformClientCommand(this, client, command);

            // Send result to client if not empty
            if (result.Length > 0) { client.Send(result); }

            // Send prompt
            client.Free();
            client.SendPrompt();

        }

        public void DisposeClient(Client client)
        {
            // Remove from client list.
            foreach (Client c in Clients)
            {
                if (c == client) { Clients.RemoveAt(Clients.IndexOf(c)); }
                break;
            }

            // Close.
            client.TcpClient.Close();
        }

        private string GetServerID(string UUID)
        {
            return $"TQ Containerized Shell\n" +
                $"Version {Config.ServerVersion}\n" +
                $"InternalOnlyMode {Config.InternalOnlyMode}\n" +
                $"AllowAnonymous {Config.AllowAnonymous}\n" +
                $"UUID {UUID}"; // UUID will hopefully be yeeted into the void by any compatible clients, as the UUID is used to make sure control flags aren't injected. so, yknow, keep this safe i guess lol
        }

        public static void WriteLine(string str)
        {
            if (!Config.Interactive) { Console.WriteLine(str); }
            Logs.Add(str);
        }

        public string GetLogs()
        {
            return string.Join("\n", Logs);
        }
    }
}