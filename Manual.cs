using System;
using System.Collections.Generic;

namespace TQServer
{
    public class Manual
    {
        private static readonly Dictionary<string, string> manualDict = new()
        {
            { "echo", "Usage: echo [text]\nEchos the input text back to the user." },
            { "cd", "Usage: cd [directory]\nChanges the current working directory." },
            { "ls", "Usage: ls [directory]\nLists the contents of a directory. By default lists current directory, can be invoked with relative or absolute path." },
            { "pwd", "Usage: pwd [-r]\nPrints the current working directory.\n\nFlags:\n  -r: Show the real directory (admin only)." },
            { "curl", "Usage: curl [options] <url>\nMakes an HTTP request to the specified URL.\n\nFlags:\n -X: Specify the HTTP request method (GET, POST, etc.)\n\nExample: curl -X=POST localhost:8000" },
            { "logs", "Usage: logs\nDisplays server logs." },
            { "exit", "Usage: exit\nLogs out the current user." },
            { "mv", "Usage: mv <file1> <file2>\nMoves or renames a file as described." },
            { "cp", "Usage: cp <file1> <file2>\nCopies a file as described." },
            { "rm", "Usage: rm <path>\nDeletes a file or directory. -r flag must be specified to delete directories.\n\nFlags:\n -r: Allow recursive deletion\n -f: Automatically approve prompts." },
            { "cat", "Usage: cat <path> [additional paths]\nReads a file from disk. May be invoked with additional paths." },
            { "argdbg", "Usage: argdbg [any]\nReturns parsed results of the command's arguments and flags. For debugging or testing purposes.\n\nFlags:\n Any.\n\nExample: argdbg -FLAG=1 ARG1 -R -T=TEST" },
            { "touch", "Usage: touch <file>\nCreates an empty file at a specified path.\n\nExamples:\ntouch tempfile.dat\ntouch /tmpdir/empty.txt" },
            { "mkdir", "Usage: mkdir <path>\nCreates a new directory if it does not already exist.\n\nExample: mkdir .\\Pictures" },
            { "dumpconfig", "Usage: dumpconfig\nLists relevant server config options." },
            { "users", "Usage: users\nShows a list of users on the system as well as their scopes and permissions." },
            { "exec", "Usage: exec <program> [arguments] [flags=flagvalues]\nLaunches an external program with the supplied arguments and flags relayed to it. Can also be invoked by suffixing your command with .EXE.\n\nExample: exec explorer.exe C:\\Users\nExample: exec /bin/cloudflared --url=localhost:8080\nExample: regedit.exe" },
            { "su", "Usage: su [username]\nCan be used to switch to another user. You can provide the username as an argument, or type it into the live prompt.\nFlags:\n -p: Password provided inline.\n\nExample: su\nExample: su jacket\nExample: su jacket -p=secret" },
        };

        public static string GetManual(string command)
        {
            return manualDict.TryGetValue(command, out var manual)
                ? manual
                : $"No manual entry for '{command}'.";
        }
    }
}
