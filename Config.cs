using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    public static class Config
    {
        static Config()
        {
            LoadConfig("acki.conf");
        }

        public static int    Port                      = 11037;
        public static bool   InternalOnlyMode          = true;
        public static bool   AllowAnonymous            = true;
        public static bool   Interactive               = true;
        public static bool   PerformConfigAuditOnStart = true;

        public static int    ServerVersion             = 01;

        public static readonly string ServerName       = System.Net.Dns.GetHostName();
        public static string ServerRootDirectory       = "C:\\";
        public static Scope AnonymousScope { get; private set; }

        public static void AuditConfig()
        {
            if (AllowAnonymous && AnonymousScope.ContainsAny(new List<string> { "Admin", "Write", "Net", "Exec" }))
            {
                Server.WriteLine("\n!!! WARNING !!!\n\nDangerous configuration detected! Anonymous Scope is considered broad. Remember that anyone connecting to the server can perform these actions!\n");
            }
        }

        public static void LoadConfig(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {configFilePath}");
            }

            var globalSection = new Dictionary<string, string>();
            var scopeSections = new Dictionary<string, Scope>();

            string currentSection = null;
            string currentScopeName = null;

            bool inUsersSection = false;
            User currentUser = null;

            foreach (var line in File.ReadLines(configFilePath))
            {
                var trimmedLine = line.Trim();

                // Skip whitespace†/comments
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Handle section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    // Before changing sections, add the current user (if any) to the UserTree
                    if (inUsersSection && currentUser != null)
                    {
                        UserTree.AddUser(currentUser);
                        currentUser = null;
                    }

                    currentSection = trimmedLine.Trim('[', ']');
                    currentScopeName = null; // Reset scope context when a new section starts
                    inUsersSection = currentSection == "USERS";
                    continue;
                }

                if (currentSection == "SCOPES" && trimmedLine.StartsWith("<") && trimmedLine.EndsWith(">"))
                {
                    currentScopeName = trimmedLine.Trim('<', '>');
                    if (!scopeSections.ContainsKey(currentScopeName))
                    {
                        scopeSections[currentScopeName] = new Scope(currentScopeName);
                    }
                    continue;
                }

                if (currentSection == "GLOBAL")
                {
                    var parts = trimmedLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        globalSection[parts[0]] = parts[1].Trim('"');
                    }
                    continue;
                }

                // Parse permissions under a scope
                if (currentScopeName != null && currentSection == "SCOPES")
                {
                    var parts = trimmedLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && parts[0].Equals("Permission", StringComparison.OrdinalIgnoreCase))
                    {
                        scopeSections[currentScopeName].AddPermission(parts[1]);
                    }
                    continue;
                }

                // Parse users
                if (inUsersSection && trimmedLine.StartsWith("<") && trimmedLine.EndsWith(">"))
                {
                    // If defining a new user, add the previous one to the UserTree
                    if (currentUser != null)
                    {
                        UserTree.AddUser(currentUser);
                    }

                    var username = trimmedLine.Trim('<', '>');
                    currentUser = new User { Username = username.ToLower() };
                    continue;
                }

                if (inUsersSection && currentUser != null)
                {
                    var parts = trimmedLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        if (parts[0].Equals("Scope", StringComparison.OrdinalIgnoreCase))
                        {
                            var scopeName = parts[1];
                            if (!scopeSections.TryGetValue(scopeName, out var scope))
                            {
                                throw new Exception($"Scope '{scopeName}' referenced by user '{currentUser.Username}' is not declared in the SCOPES section.");
                            }
                            currentUser.AddScope(scope);
                        }
                        else if (parts[0].Equals("Password", StringComparison.OrdinalIgnoreCase))
                        {
                            // CHANGE THIS TO ACTUAL PASSWD PATH EVENTUALLY, WE'RE USING PLAINTEXT PASSWORDS RN
                            currentUser.PasswdPath = parts[1];
                        }
                    }
                }
            }

            // Add the last user to the UserTree after parsing is complete
            if (currentUser != null)
            {
                UserTree.AddUser(currentUser);
            }

            // Assign the Anonymous scope
            if (scopeSections.TryGetValue("Anonymous", out var anonymousScope))
            {
                AnonymousScope = anonymousScope;
            }
            else
            {
                // Default value in case the halfwit sysadmin deletes the anonymous scope from acki.conf
                AnonymousScope = new Scope("Anonymous", new[] { "Read", "Traverse" });
            }

            // Apply the global settings
            Port = int.Parse(globalSection.GetValueOrDefault("ListenPort", "5000"));
            Interactive = globalSection.GetValueOrDefault("Interactive", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
            InternalOnlyMode = globalSection.GetValueOrDefault("RestrictToLocalhost", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
            AllowAnonymous = globalSection.GetValueOrDefault("AllowAnonymous", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
            ServerRootDirectory = globalSection.GetValueOrDefault("ServerRootDirectory", "/");

            // Scopes should now be declared. It is safe to initialize the UserTree.
            UserTree.Init();

            // Debugging output
            foreach (var scope in scopeSections.Values)
            {
                Server.WriteLine(scope.ToString());
            }

            Server.WriteLine($"Loaded Configuration: \nPort: {Port}\nInternalOnlyMode: {InternalOnlyMode}\nAllowAnonymous: {AllowAnonymous}\nServerRootDirectory: {ServerRootDirectory}");
            Server.WriteLine($"Anonymous Scope: {AnonymousScope}");
        }


    }
}

// † You have been living here for as long as you can remember.