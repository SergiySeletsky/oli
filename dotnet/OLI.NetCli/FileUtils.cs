using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

public static class FileUtils
{
    public static void Touch(string path)
    {
        if (!File.Exists(path))
        {
            using var _ = File.Create(path);
        }
        else
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }
    }

    public static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    public static void MoveDirectory(string sourceDir, string destDir)
    {
        CopyDirectory(sourceDir, destDir);
        Directory.Delete(sourceDir, true);
    }

    public static void RenameDirectory(string sourceDir, string newName)
    {
        var parent = Directory.GetParent(sourceDir)?.FullName ?? ".";
        var dest = Path.Combine(parent, newName);
        Directory.Move(sourceDir, dest);
    }

    public static void CompressFile(string sourceFile, string destZip)
    {
        using var archive = ZipFile.Open(destZip, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile));
    }

    public static void DecompressFile(string zipPath, string destFile)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault();
        if (entry != null)
        {
            entry.ExtractToFile(destFile, true);
        }
    }

    public static bool IsBinaryFile(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[8000];
        int read = stream.Read(buffer, 0, buffer.Length);
        for (int i = 0; i < read; i++) if (buffer[i] == 0) return true;
        return false;
    }

    public static string[] LoadIgnorePatterns(string root)
    {
        var list = new List<string>();
        var git = Path.Combine(root, ".gitignore");
        if (File.Exists(git))
        {
            foreach (var line in File.ReadAllLines(git))
            {
                var l = line.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                list.Add(l);
            }
        }
        return list.ToArray();
    }

    public static bool IsIgnored(string file, string[] patterns)
    {
        foreach (var pat in patterns)
        {
            if (pat.EndsWith("/"))
            {
                if (file.Contains(Path.DirectorySeparatorChar + pat.TrimEnd('/') + Path.DirectorySeparatorChar))
                    return true;
            }
            else if (file.EndsWith(pat) || Path.GetFileName(file).Contains(pat.Trim('*')))
            {
                return true;
            }
        }
        return false;
    }

    public static IEnumerable<string> GlobSearch(string pattern)
    {
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
        matcher.AddInclude(pattern);
        var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(Directory.GetCurrentDirectory())));
        return result.Files.Select(f => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), f.Path)))
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f));
    }

    public static IEnumerable<string> GlobSearchInDir(string dir, string pattern)
    {
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
        matcher.AddInclude(pattern);
        var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(dir)));
        return result.Files.Select(f => Path.GetFullPath(Path.Combine(dir, f.Path)))
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f));
    }

    public static IEnumerable<string> GlobSearchAdv(string dir, string pattern)
    {
        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
        matcher.AddInclude(pattern);
        foreach (var pat in LoadIgnorePatterns(dir))
            matcher.AddExclude(pat);
        var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(dir)));
        return result.Files.Select(f => Path.GetFullPath(Path.Combine(dir, f.Path)))
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f));
    }

    public static IEnumerable<(string File, int Line, string Text)> GrepSearch(string dir, string pattern, string? includePattern = null)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        IEnumerable<string> files = includePattern != null
            ? GlobSearchInDir(dir, includePattern)
            : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsBinaryFile(file)) continue;
            int lineNum = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNum++;
                if (regex.IsMatch(line))
                    yield return (file, lineNum, line);
            }
        }
    }

    public static IEnumerable<(string File, int Line, string Text)> GrepSearchAdv(string dir, string pattern, string? includePattern = null)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        var ignores = LoadIgnorePatterns(dir);
        IEnumerable<string> files = includePattern != null
            ? GlobSearchInDir(dir, includePattern)
            : Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsIgnored(file, ignores) || IsBinaryFile(file)) continue;
            int lineNum = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNum++;
                if (regex.IsMatch(line))
                    yield return (file, lineNum, line);
            }
        }
    }
}
