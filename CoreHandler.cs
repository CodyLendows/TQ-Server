using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TQServer
{
    static class CoreHandler
    {
        private static readonly Dictionary<string, CommandHandler> CommandRegistry = new();

        static CoreHandler()
        {
            RegisterCommand("echo", HandleEcho, "Any");

            RegisterCommand("cd", HandleCd, "Traverse");

            RegisterCommand("ls", HandleLs, "Traverse");
            RegisterCommand("dir", HandleLs, "Traverse");

            RegisterCommand("pwd", HandlePwd, "Traverse");

            RegisterCommand("cat", HandleCat, "Read");

            RegisterCommand("mkdir", HandleMkdir, "Write");
            RegisterCommand("md", HandleMkdir, "Write");

            RegisterCommand("touch", HandleTouch, "Write");
            RegisterCommand("mv", HandleMove, "Write");
            RegisterCommand("cp", HandleCopy, "Write");
            RegisterCommand("rm", HandleDelete, "Write");

            RegisterCommand("exit", HandleDisconnect, "LiterallyAny");

            RegisterCommand("logs", HandleLogs, "Admin");

            RegisterCommand("curl", HandleCurl, "Net");

            RegisterCommand("testfatal", HandleTestFatal, "Any");
            RegisterCommand("dumpconfig", HandleDumpConfig, "Admin");

            RegisterCommand("ready", HandleNone, "Any");
            RegisterCommand("users", HandleListUsers, "Any");
            RegisterCommand("argdbg", HandleDbg, "Any");
            RegisterCommand("quirks", HandleQuirks, "Any");
            RegisterCommand("whichdir", HandleDirtest, "Any");

            RegisterCommand("exec", HandleProgExec, "Exec");

            RegisterCommand("man", HandleMan, "Any");
            RegisterCommand("help", HandleHelp, "Any");

            RegisterCommand("su", HandleSwitchUser, "LiterallyAny");
            RegisterCommand("login", HandleSwitchUser, "LiterallyAny");
            RegisterCommand("logout", HandleLogout, "Any");

        }

        public static string PerformClientCommand(Server server, Client client, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "?";

            // Check for piping (e.g., "command args > pipeMethod")
            var pipeIndex = message.IndexOf('>');
            string pipeTarget = null;
            if (pipeIndex > -1)
            {
                pipeTarget = message[(pipeIndex + 1)..].Trim();
                message = message[..pipeIndex].Trim();

                if (string.IsNullOrWhiteSpace(pipeTarget))
                {
                    return "Invalid pipe target.";
                }
            }

            var (command, args, flags) = ParseCommand(message);

            if (!CommandRegistry.TryGetValue(command, out var handler))
            {
                // No command matches. Check if it ends in .exe and in that case try executing it...
                try 
                { 
                    flags.Add("#STX#", command);
                    return HandleProgExec(server, client, args, flags); 
                }
                catch
                {
                    // okay, it's not even an exe file, let's just go home.
                    return "command not found: " + command;
                }
            }

            if (!CheckClientScope(client, handler.RequiredScope)) return "";

            try
            {
                var result = handler.Invoke(server, client, args, flags);

                // If piping, redirect output to the specified method
                if (!string.IsNullOrEmpty(pipeTarget))
                {
                    try
                    {
                        return RedirectToPipe(pipeTarget, result, client);
                    }
                    catch (Exception ex)
                    {
                        return $"Failed to redirect output to {pipeTarget}: {ex.Message}";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"{command}: {ex.Message}";
            }
        }

        private static string RedirectToPipe(string pipeTarget, string output, Client client)
        {
            if (!CheckClientScope(client, "Write")) return "";

            if (pipeTarget.StartsWith('>'))
            {
                pipeTarget = pipeTarget.Substring(1).Trim();
                if (pipeTarget.Contains('>')) return "Pipe syntax error.";

                DirHandler.EffectiveAppendResult result = DirHandler.AppendFile(pipeTarget, output, client.RealDirectory);

                if (result == DirHandler.EffectiveAppendResult.OK) return "";
                if (result == DirHandler.EffectiveAppendResult.FAIL) return "Pipe failure.";
                if (result == DirHandler.EffectiveAppendResult.DNE) ; /* I mean... I guess just flow over into the functionality below. lol. */
            }

            DirHandler.WriteFileIndiscriminate(pipeTarget, output, client.RealDirectory);
            return "";
        }



        private static void RegisterCommand(string name, Func<Server, Client, List<string>, Dictionary<string, string>, string> handler, string requiredScope)
        {
            CommandRegistry[name] = new CommandHandler(handler, requiredScope);
        }

        private static (string command, List<string> args, Dictionary<string, string> flags) ParseCommand(string message)
        {
            var tokens = Regex.Matches(message, @"(?:[^\s""']+|""[^""]*""|'[^']*')+")
                              .Cast<Match>()
                              .Select(m => m.Value.Trim('\"', '\''))
                              .ToList();
            /*
               TL;DR for the demented regex above: 
                   -X=GET : GET is treated as a flag for X.
                   -X GET : Flag X is True but GET is a separate argument
             */

            if (tokens.Count == 0) return ("", new List<string>(), new Dictionary<string, string>());

            var command = tokens[0].ToLower();
            var args = new List<string>();
            var flags = new Dictionary<string, string>(StringComparer.Ordinal); // Case-sensitive keys

            foreach (var token in tokens.Skip(1))
            {
                if (token.StartsWith("--"))
                {
                    var flagParts = token[2..].Split('=', 2);
                    flags[flagParts[0]] = flagParts.Length > 1 ? flagParts[1] : "true";
                }
                else if (token.StartsWith("-"))
                {
                    var flagParts = token[1..].Split('=', 2);
                    flags[flagParts[0]] = flagParts.Length > 1 ? flagParts[1] : "true";
                }
                else
                {
                    args.Add(token);
                }
            }

            return (command, args, flags);
        }


        public static bool CheckClientScope(Client client, string permission)
        {
            if (permission == "LiterallyAny") return true;

            if (client.User.Scopes.Count < 1)
            {
                client.Send("Your client does not have any scopes associated with it. Please log in.");
                return false;
            }

            if (client.User.HasPermission(permission) || permission == "Any") return true;

            string scopeNames = string.Join(" & ", client.User.Scopes.Select(scope => scope.Name));
            client.Send($"Sorry, ({scopeNames}) does not have permission ({permission}) to perform this command on {Config.ServerName}.");
            return false;
        }

        private static string HandleNone(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            return "";
        }

        private static string HandleSwitchUser(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            string requestedUsername = string.Empty;

            // Check if username was specified
            if (args.Count == 0)
            {
                client.Send("Switching users.\nUsername:", true);
                requestedUsername = client.LivePrompt(server);
            }
            else
            {
                requestedUsername = args[0];
            }

            AntiEnumerationWait();

            if (requestedUsername == "" || requestedUsername == null) { throw new ArgumentException("A username is required."); }

            User requestedUser = UserTree.GetUserByName(requestedUsername);
            if (requestedUser == null) return $"{requestedUsername}@{Config.ServerName} does not exist or is unavailable.";

            string presuppliedPassword = flags.ContainsKey("p") ? flags["p"] : null;

            bool correct = false;

            if (presuppliedPassword == null)
            {
                for (int i = 0; i < 3; i++)
                {
                    client.Send("Password:", true);
                    presuppliedPassword = client.LivePasswordPrompt(server);

                    AntiEnumerationWait();

                    if (requestedUser.PasswdPath == presuppliedPassword) { client.User = requestedUser; correct = true; break; }
                    else { client.Send("Sorry, try again."); }
                }

                if (!correct) { throw new UnauthorizedAccessException("3 incorrect password attempts."); }
            }

            if (requestedUser.PasswdPath == presuppliedPassword) { client.User = requestedUser; return ""; }
            else throw new UnauthorizedAccessException("incorrect password");
        }
        private static string HandleProgExec(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            string STX_COMMAND = null;
            if (flags.ContainsKey("#STX#")) { STX_COMMAND = flags["#STX#"]; flags.Remove("#STX#"); }

            // i mean, provided they have perms.
            if (!CheckClientScope(client, "Exec")) throw new UnauthorizedAccessException();

            if (STX_COMMAND != null)
            {
                // method was invoked via unknown command fallback and, if correct,
                // is in the format "explorer.exe /H ANY"
                if (!STX_COMMAND.EndsWith(".exe")) throw new Exception();

                try
                {
                    List<string> concArgs = new List<string>();
                    concArgs.AddRange(args);
                    foreach(string flag in flags.Keys)
                    {
                        concArgs.Add(flag + ((flags[flag] == "true" || flags[flag] == "false") ? "" : " " + flags[flag]));
                    }

                    System.Diagnostics.Process.Start(STX_COMMAND, concArgs);
                    return "";
                }
                catch { return "exec: no such program found"; } // unknown command 3 revengeance 
            }
            else
            {
                // method was invoked via exec command and is in format
                // "exec explorer /H ANY"
                try
                {
                    List<string> concArgs = new List<string>();
                    concArgs.AddRange(args);
                    foreach (string flag in flags.Keys)
                    {
                        concArgs.Add("-"+flag + ((flags[flag] == "true" || flags[flag] == "false") ? "" : " " + flags[flag]));
                    }

                    // Build the command and its arguments
                    string command = args[0];
                    string commandArgs = string.Join(" ", concArgs.Skip(1));

                    // Start the process
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = commandArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        if (process == null) throw new Exception("Failed to start process");

                        // Read output and error
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        process.WaitForExit();

                        // Return output or error
                        if (!string.IsNullOrEmpty(output)) return output.Trim();
                        if (!string.IsNullOrEmpty(error)) return error.Trim();

                        return ""; // No output or error
                    }
                }
                catch (Exception ex)
                {
                    return $"exec: no such program found";
                }
            }
        }

        private static string HandleEcho(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            return string.Join(" ", args);
        }

        private static string HandleListUsers(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            foreach (User user in UserTree.Users)
            {
                string scoeptas = string.Join(" ", user.Scopes);
                client.Send(user.Username + ": " + scoeptas);
            }
            return "";
        }

        private static string HandleLogout(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            client.User = new User();
            return "Logging you out!";
        }

        private static string HandleCd(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0) throw new ArgumentException("no directory supplied");
            if (args.Count > 1) throw new ArgumentException("too many arguments");

            var edcr = client.SetDirectory(args[0]);
            return edcr switch
            {
                Client.EffectiveDirectoryChangeResult.OK => "",
                Client.EffectiveDirectoryChangeResult.DNE => $"cd: {args[0]}: directory does not exist",
                _ => "cd: internal error",
            };
        }

        private static string HandleDelete(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0) throw new ArgumentException("expected path");
            if (args.Count > 1) throw new ArgumentException("too many arguments");

            if (flags.ContainsKey("r"))
            {
                // recursive delete (directory)
                // check if even exists
                string target = DirHandler.ResolvePath(args[0], client.RealDirectory);
                if (!Directory.Exists(target)) throw new FileNotFoundException("no such directory: " + DirHandler.GetPerceivedPath(target, Config.ServerRootDirectory));

                // dangerous so ask them for confirmation
                if (!flags.ContainsKey("f"))
                {
                    client.Send($"All files and subdirectories in {DirHandler.GetPerceivedPath(target, Config.ServerRootDirectory)} will be deleted, are you sure? (y/n)", true);
                    string reply = client.LivePrompt(server);

                    if (reply.ToLower() != "y") throw new Exception("aborting");
                }

                DirHandler.EffectiveMoveResult result = DirHandler.DeleteDirectory(args[0], client.RealDirectory);

                if (result == DirHandler.EffectiveMoveResult.OK) return "";
                throw new Exception("unknown failure");
            }
            else
            {
                // deleting single file
                DirHandler.EffectiveMoveResult result = DirHandler.DeleteFile(args[0], client.RealDirectory);

                if (result == DirHandler.EffectiveMoveResult.OK) return "";
                if (result == DirHandler.EffectiveMoveResult.NO_SRC) throw new ArgumentException($"no such file: {args[0]}");
                throw new Exception("unknown failure");
            }
        }
        private static string HandleDirtest(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0) throw new ArgumentException("specify a directory");
            if (args.Count > 1) throw new ArgumentException("too many arguments");
            string str = DirHandler.ResolvePath(args[0], client.RealDirectory);
            return "Real: " + str + " | Exists: " + Directory.Exists(str) + " | Perceived: " + DirHandler.GetPerceivedPath(str, Config.ServerRootDirectory);
        }

        private static string HandleDumpConfig(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if(args.Count < 1)
            {
                string builder = "";
                builder += "AllowAnonymous "             + Config.AllowAnonymous + "\n";
                builder += "AnonymousScope.Permissions " + string.Join(", ", Config.AnonymousScope.Permissions) + "\n";
                builder += "InternalOnlyMode "           + Config.InternalOnlyMode + "\n";
                builder += "ServerVersion"               + Config.ServerVersion + "\n";
                builder += "PerformConfigAuditOnStart"   + Config.PerformConfigAuditOnStart + "\n";
                builder += "Port"                        + Config.Port + "\n";
                builder += "ServerName"                  + Config.ServerName + "\n";
                builder += "ServerRootDirectory"         + Config.ServerRootDirectory + "\n";
                return builder;
            }
            throw new ArgumentException("too many arguments");
        }

        private static string HandleLs(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (!args.Any()) { args = new List<string>(); args.Add("."); }
            return DirHandler.InvokeFileListing(args, flags, client.RealDirectory);
        }

        private static string HandlePwd(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (flags.ContainsKey("r"))
            {
                if (!CheckClientScope(client, "Admin")) throw new UnauthorizedAccessException("access denied");
                return client.RealDirectory;
            }
            return "/" + client.CDirectory;
        }

        private static string HandleCat(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0) throw new ArgumentException("no file supplied");

            if (args.Count > 1)
            {
                var results = args.Select(path => $"* {path}: \n{DirHandler.ReadFile(path, client.RealDirectory)}\n").ToList();
                return string.Join("\n", results).TrimEnd();
            }
            else
            {
                var results = args.Select(path => $"{DirHandler.ReadFile(path, client.RealDirectory)}\n").ToList();
                return string.Join("\n", results).TrimEnd();
            }
        }

        private static string HandleQuirks(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            return client.Quirks;
        }

        private static string HandleDisconnect(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            client.Send("Bye!");
            server.DisposeClient(client);
            return "";
        }

        private static string HandleLogs(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            return server.GetLogs();
        }

        private static string HandleMan(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0)
            {
                return "man: Please specify a command. Example: man echo";
            }

            var command = args[0];
            return Manual.GetManual(command);
        }

        private static string HandleHelp(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            string retStr = $"TQ Shell Syntax Examples:\n";
            retStr += "command argument argument2 -flag=flagvalue\n";
            retStr += "command \"single argument with whitespace\" -a=1 -b=2 -c\n\n";
            retStr += "Flags with no assigned values default to true. For additional help with arguments and flags, use argdbg.\n\n";
            retStr += "This shell also supports output redirection. Use > to direct program output to a file, or >> to append.\n\n";
            retStr += $"Available commands on {Config.ServerName}:\n";
            retStr += string.Join(", ", CommandRegistry.Keys);

            return retStr;
        }

        private static string HandleCurl(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count == 0) throw new ArgumentException("not enough arguments");

            try
            {
                var uri = NetTools.ConvertToUri(args[0]);
                string method = flags.ContainsKey("X") ? flags["X"].ToUpper() : "GET";
                HttpMethod httpMethod = method switch
                {
                    "GET" => HttpMethod.Get,
                    "POST" => HttpMethod.Post,
                    "PUT" => HttpMethod.Put,
                    "DELETE" => HttpMethod.Delete,
                    "HEAD" => HttpMethod.Head,
                    "OPTIONS" => HttpMethod.Options,
                    _ => HttpMethod.Trace
                };

                if(httpMethod == HttpMethod.Trace) { throw new MissingMethodException("invalid or unsupported method"); }

                var byteReply = NetTools.InvokeWebRequest(uri, httpMethod);
                return Encoding.UTF8.GetString(byteReply);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static string HandleMkdir(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count < 1) throw new ArgumentException("expected path");
            DirHandler.EffectiveMkdirResult result = DirHandler.CreateDirectory(args[0], client.RealDirectory);
            
            if (result == DirHandler.EffectiveMkdirResult.OK) return "";
            if (result == DirHandler.EffectiveMkdirResult.EXISTS) throw new InvalidOperationException("directory exists");
            if (result == DirHandler.EffectiveMkdirResult.FAIL) throw new FileLoadException("could not create directory");

            throw new Exception("internal error");
        }

        private static string HandleTouch(Server server, Client client, List<string> args, Dictionary<string, string> flags) 
        {
            if (args.Count < 1) throw new ArgumentException("expected file path");
            DirHandler.EffectiveTouchResult result = DirHandler.CreateFile(args[0], client.RealDirectory);

            if (result == DirHandler.EffectiveTouchResult.FAIL) throw new Exception("could not create file");
            return "";
        }

        private static string HandleMove(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count != 2) throw new ArgumentException("usage: mv [-f force] <source> <destination>");
            DirHandler.EffectiveMoveResult result = DirHandler.MoveFile(args[0], args[1], client.RealDirectory, false, flags.ContainsKey("f") || flags.ContainsKey("force"));

            if (result == DirHandler.EffectiveMoveResult.FAIL) throw new Exception ("failed to move file");
            if (result == DirHandler.EffectiveMoveResult.SAME) throw new UnauthorizedAccessException("source and destination paths are the same");
            if (result == DirHandler.EffectiveMoveResult.NO_SRC) throw new FileNotFoundException($"no such file: {args[0]}");
            if (result == DirHandler.EffectiveMoveResult.DEST_EXISTS) throw new UnauthorizedAccessException($"destination exists, specify -f to overwrite");
            return "";
        }

        private static string HandleCopy(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            if (args.Count != 2) return "cp: usage: cp [-f force] <source> <destination>";
            DirHandler.EffectiveMoveResult result = DirHandler.MoveFile(args[0], args[1], client.RealDirectory, true, flags.ContainsKey("f") || flags.ContainsKey("force"));

            if (result == DirHandler.EffectiveMoveResult.FAIL) throw new Exception("failed to move file");
            if (result == DirHandler.EffectiveMoveResult.SAME) throw new UnauthorizedAccessException("source and destination paths are the same");
            if (result == DirHandler.EffectiveMoveResult.NO_SRC) throw new FileNotFoundException($"no such file: {args[0]}");
            if (result == DirHandler.EffectiveMoveResult.DEST_EXISTS) throw new UnauthorizedAccessException($"destination exists, specify -f to overwrite");

            return "";
        }

        private static string HandleDbg(Server server, Client client, List<string> args,  Dictionary<string, string> flags)
        {
            string dump = "";
            dump += "RECEIVED ARGUMENTS\n";
            foreach(string arg in args) { dump += arg + "\n"; }
            dump += "\r\n";
            dump += "RECEIVED FLAGS\n";
            foreach(string arg in flags.Keys) dump += arg + "\n";
            dump += "\r\n";
            dump += "RECEIVED FLAG VALUES" +
                "\n";
            foreach (string arg in flags.Values) dump += arg + "\n";

            return dump;
        }

        private static string HandleTestFatal(Server server, Client client, List<string> args, Dictionary<string, string> flags)
        {
            return args.Count < 1 ? $"#RaiseFatal#{client.ID}Test error raised." : $"#RaiseFatal#{client.ID}" + string.Join(" ", args);
        }

        static Random random = new Random();
        private static void AntiEnumerationWait()
        {
            int min = 250;
            int max = 2250;
            Thread.Sleep(random.Next(min, max));
        }

        private class CommandHandler
        {
            public Func<Server, Client, List<string>, Dictionary<string, string>, string> Invoke { get; }
            public string RequiredScope { get; }

            public CommandHandler(Func<Server, Client, List<string>, Dictionary<string, string>, string> invoke, string requiredScope)
            {
                Invoke = invoke;
                RequiredScope = requiredScope;
            }
        }
    }
}
