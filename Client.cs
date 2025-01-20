using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace TQServer
{
    public class Client
    {
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public string ID { get; }

        public string Quirks = "NA";

        public string CDirectory { get; private set; }
        public string RealDirectory { get; private set; }

        public TDF_Collection Last_TDF;

        public User User;

        public Client(TcpClient client)
        {
            TcpClient = client;
            Stream = client.GetStream();
            ID = Guid.NewGuid().ToString(); // Assign a unique ID to the client

            RealDirectory = Config.ServerRootDirectory;
            CDirectory = "";
        }

        // RX/TX
        public void Send(string msg)
        {
            Send(msg+"\r\n", true);
        }

        public void Send(string msg, bool raw)
        {
            try
            {
                if (!raw) { Send(msg); }

                byte[] responseBytes = Encoding.UTF8.GetBytes(msg);
                Stream.Write(responseBytes, 0, responseBytes.Length);
            }
            catch {}
        }

        public void SendPrompt()
        {
            string uname = User.Username == null ? "" : User.Username;
            string prompt = $"{uname}@{Config.ServerName}:/{CDirectory}> ";

            Send(prompt, true);
        }

        public void Free()
        {
            if (Quirks != "NO") Send("", true);
            else Send("#Available#" + ID, true);
        }

        private string _response;
        private ManualResetEvent _waitHandle = new ManualResetEvent(false);

        public string LivePrompt(Server server)
        {
            server.ToLive = true;
            Send(" ", true); // Prompt (nothing for now)
            Free();
            server.Recv(Last_TDF); // real schizo shit

            // Wait until InvokeReplyToLivePrompt signals
            _waitHandle.WaitOne();

            server.ToLive = false;

            // Return the response that was set
            return _response;
        }

        public string LivePasswordPrompt(Server server)
        {
            Send(" ", true);
            Free(); // allow input
            server.ToLive = true;
            if (Quirks == "NO") { Send($"#PassWdFollows#{ID}", true); }
            server.Recv(Last_TDF); // real schizo shit

            // Wait until InvokeReplyToLivePrompt signals
            _waitHandle.WaitOne();

            server.ToLive = false;

            // Return the response that was set
            return _response;
        }

        public void InvokeReplyToLivePrompt(string command)
        {
            if (command == $"#ready#{ID}")
            {
                // ctrl+c was pressed, don't send command.
                _response = "";
                _waitHandle.Set();
                return;
            }

            // Set the response
            _response = command;

            // Signal that the response is ready
            _waitHandle.Set();
        }

        // SERVER TRAVERSAL
        public enum EffectiveDirectoryChangeResult { OK, DNE, FAIL, INVALID }

        public EffectiveDirectoryChangeResult SetDirectory(string path)
        {
            try
            {
                string resolvedPath = ResolvePath(path, RealDirectory);

                if (resolvedPath == null || !Directory.Exists(resolvedPath))
                {
                    return EffectiveDirectoryChangeResult.DNE;
                }

                CDirectory = Path.GetRelativePath(Config.ServerRootDirectory, resolvedPath).Replace("\\", "/");
                if (CDirectory == ".") CDirectory = "";
                RealDirectory = resolvedPath;

                return EffectiveDirectoryChangeResult.OK;
            }
            catch
            {
                return EffectiveDirectoryChangeResult.FAIL;
            }
        }

        public static string ResolvePath(string relativePath, string currentDirectory)
        {
            try
            {
                string resolvedPath = Path.GetFullPath(Path.Combine(currentDirectory, relativePath));

                // Stay within the GOD DAMNED root directory
                if (!resolvedPath.StartsWith(Config.ServerRootDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedPath = Config.ServerRootDirectory;
                }

                return resolvedPath;
            }
            catch
            {
                return null; // Indicates invalid path
            }
        }

    }
}
