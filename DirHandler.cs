using System.Runtime.InteropServices;
using TQServer;

public static class DirHandler
{
    public static string InvokeFileListing(List<string> args, Dictionary<string, string> flags, string currentDirectory)
    {
        try
        {
            if (args == null || args.Count == 0)
            {
                return "ls: invalid command input";
            }

            string targetDirectory = currentDirectory;

            // Determine target directory based on arguments
            // This should be in CoreHandler but this was written before I standardized that and I'm too lazy to fix it :) :) :) :)
            if (args.Count > 0)
            {
                string relativePath = args[0];

                if (currentDirectory == Config.ServerRootDirectory)
                {
                    relativePath = relativePath.Replace("\\", ".\\");
                    relativePath = relativePath.Replace("/", "./");
                }

                targetDirectory = ResolvePath(relativePath, currentDirectory);
                if (targetDirectory == null)
                {
                    return "ls: invalid path";
                }
            }

            // Check for detailed flag
            bool detailed = !flags.ContainsKey("short") && !flags.ContainsKey("s");

            // List directory contents
            return ListDirectory(targetDirectory, detailed);
        }
        catch (Exception ex)
        {
            return $"ls: {ex.Message}";
        }
    }

    public enum EffectiveMkdirResult { OK, EXISTS, FAIL }
    public static EffectiveMkdirResult CreateDirectory(string path, string currentDirectory)
    {
        try
        {
            // Resolve the full file path
            string resolvedFilePath = ResolvePath(path, currentDirectory);

            // Check if resolved path already exists
            if (Path.Exists(resolvedFilePath)) { return EffectiveMkdirResult.EXISTS; }

            Directory.CreateDirectory(resolvedFilePath);
            return EffectiveMkdirResult.OK;
        }
        catch
        {
            return EffectiveMkdirResult.FAIL;
        }
    }

    public enum EffectiveAppendResult { OK, DNE, FAIL }
    public static EffectiveAppendResult AppendFile(string target, string content, string currentDirectory)
    {
        string resolvedPath = ResolvePath(target, currentDirectory);
        if (!File.Exists(resolvedPath)) return EffectiveAppendResult.DNE;

        try
        {
            using (var stream = new FileStream(resolvedPath, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("\n"+content);
            }

            return EffectiveAppendResult.OK;
        }
        catch
        {
            return EffectiveAppendResult.FAIL;
        }
    }

    public enum EffectiveTouchResult { OK, EXISTS, FAIL }
    public static EffectiveTouchResult CreateFile(string path, string currentDirectory)
    {
        try
        {
            // Resolve the full file path
            string resolvedFilePath = ResolvePath(path, currentDirectory);

            // Check if resolved path already exists
            if (File.Exists(resolvedFilePath)) { return EffectiveTouchResult.EXISTS; }

            var f = File.Create(resolvedFilePath);
            f.Close();
            return EffectiveTouchResult.OK;
        }
        catch
        {
            return EffectiveTouchResult.FAIL;
        }
    }

    public static string WriteFileIndiscriminate(string path, string content, string currentDirectory)
    {
        try
        {
            string resolvedPath = ResolvePath(path, currentDirectory);
            for (int i = 2; i < 5; i++) /* lol. lmao even. */
            {
                if (!File.Exists(resolvedPath)) { File.Create(resolvedPath).Dispose(); break; }
                else { /*Remove existing file contents (dangerous)*/ File.Delete(resolvedPath); }
            }
            using (var stream = new FileStream(resolvedPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }

            return "";
        }
        catch { return ""; }
    }

    public enum EffectiveMoveResult { OK, NO_SRC, DEST_EXISTS, SAME, FAIL }
    public static EffectiveMoveResult MoveFile(string source, string destination, string currentDirectory, bool keepOriginal = false, bool force = false)
    {
        try
        {
            // Resolve canonical file paths
            string resolvedSource = ResolvePath(source, currentDirectory);
            string resolvedDestination = ResolvePath(destination, currentDirectory);

            // If resolvedDestination is a directory, append original filename to the dest.
            if (Directory.Exists(resolvedDestination))
            {
                resolvedDestination = Path.Combine(resolvedDestination, Path.GetFileName(resolvedSource));
            }

            // Fail if they're the same.
            if (resolvedDestination == resolvedSource) return EffectiveMoveResult.SAME;

            // Fail if the source file doesn't exist.
            if (!File.Exists(resolvedSource)) {  return EffectiveMoveResult.NO_SRC; }

            // Fail if destination file already exists.
            if (File.Exists(resolvedDestination)) 
            { 
                if(!force) return EffectiveMoveResult.DEST_EXISTS;

                // Forcibly overwrite the destination by deleting it (yikes!)
                try { File.Delete(resolvedDestination); } catch { }
            }


            // Good to go.
            if (keepOriginal) { File.Copy(resolvedSource, resolvedDestination); }
            else { File.Move(resolvedSource, resolvedDestination); }
            return EffectiveMoveResult.OK;

        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return EffectiveMoveResult.FAIL;
        }
    }

    public static EffectiveMoveResult DeleteFile(string path, string currentDirectory)
    {
        string resolvedSource = ResolvePath(path, currentDirectory);
        if (!File.Exists(resolvedSource)) return EffectiveMoveResult.NO_SRC;

        File.Delete(resolvedSource);
        return EffectiveMoveResult.OK;
    }

    public static EffectiveMoveResult DeleteDirectory(string path, string currentDirectory)
    {
        string resolvedSource = ResolvePath(path, currentDirectory);
        if (!Directory.Exists(resolvedSource)) return EffectiveMoveResult.NO_SRC;
        try { Directory.Delete(resolvedSource, true); }
        catch { return EffectiveMoveResult.FAIL; }

        return EffectiveMoveResult.OK;
    }

    public static string ResolvePath(string relativePath, string currentDirectory)
    {
        try
        {
            // Normalize the server root directory path
            string serverRoot = Path.GetFullPath(Config.ServerRootDirectory);

            // . always resolves to the current directory
            if (relativePath == ".")
            {
                return Path.GetFullPath(currentDirectory);
            }

            // paths starting with "/" or "\" are absolute paths relative to the server root
            if (relativePath.StartsWith("/") || relativePath.StartsWith("\\"))
            {
                relativePath = relativePath.TrimStart('/', '\\');
                string resolvedPath = Path.GetFullPath(Path.Combine(serverRoot, relativePath));

                // dont let them escape server root, reset it to server root if they try
                return resolvedPath.StartsWith(serverRoot, StringComparison.OrdinalIgnoreCase) ? resolvedPath : serverRoot; 
            }

            string combinedPath = Path.Combine(currentDirectory, relativePath);
            string resolvedPathRelative = Path.GetFullPath(combinedPath);

            // Just in case, check again.
            if (resolvedPathRelative.StartsWith(serverRoot, StringComparison.OrdinalIgnoreCase)) return resolvedPathRelative;
            else return serverRoot;
        }
        catch
        {
            return null; // Indicates invalid path
        }
    }

    public static string GetPerceivedPath(string fullPath, string rootDirectory)
    {
        string resolvedPath = Path.GetFullPath(fullPath);

        // Calculate the perceived path by making it relative to the root directory
        if (resolvedPath.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return resolvedPath.Substring(rootDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        // If not within the root directory, return the full path 'cause ¯\_(ツ)_/¯
        return resolvedPath;
    }

    public static string ListDirectory(string path, bool detailed = true)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path)) return "ls: is a file";
                return "ls: directory does not exist";
            }

            var entries = new List<string>();

            // directories
            foreach (var directory in Directory.GetDirectories(path))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(directory);
                string entry = detailed
                    ? $"[DIR] {dirInfo.Name.PadRight(30)} \t {dirInfo.CreationTime}"
                    : $"[DIR] {dirInfo.Name}";
                entries.Add(entry);
            }

            // files
            foreach (var file in Directory.GetFiles(path))
            {
                FileInfo fileInfo = new FileInfo(file);
                string entry = detailed
                    ? $"[FILE] {fileInfo.Name.PadRight(30)} \t {fileInfo.Length.ToString().PadLeft(8)} bytes \t {fileInfo.CreationTime}"
                    : $"[FILE] {fileInfo.Name}";
                entries.Add(entry);
            }

            return $"\nListing of /{GetPerceivedPath(path, Config.ServerRootDirectory)}\n\n" + (entries.Any() ? string.Join("\n", entries) : "Empty directory.") + "\n";
        }
        catch (Exception ex)
        {
            return $"ls: {ex.Message}";
        }
    }

    public static string ReadFile(string path, string currentDirectory)
    {
        try
        {
            string resolvedFilePath = ResolvePath(path, currentDirectory);

            if (Path.Exists(resolvedFilePath) && !File.Exists(resolvedFilePath)) return $"cat: is a directory";
            if (!File.Exists(resolvedFilePath)) return $"cat: file '{path}' does not exist"; 

            return File.ReadAllText(resolvedFilePath);
        }
        catch (Exception ex)
        {
            return $"cat: {ex.Message}";
        }
    }
}
