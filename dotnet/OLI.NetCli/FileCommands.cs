using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public static class FileCommands
{
    public static void Register(RootCommand root)
    {
        var readPathOption = new Argument<string>("path");

        var readFileCmd = new Command("read-file", "Read file contents") { readPathOption };
        readFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            Console.WriteLine(File.ReadAllText(path));
            await Task.CompletedTask;
        }, readPathOption);

        var readNumberedCmd = new Command("read-file-numbered", "Read file with line numbers") { readPathOption };
        readNumberedCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
                Console.WriteLine($"{i + 1,4} | {lines[i]}");
            await Task.CompletedTask;
        }, readPathOption);

        var offsetOption = new Option<int>("--offset", () => 0);
        var limitOption = new Option<int?>("--limit", () => null);
        var readLinesCmd = new Command("read-file-lines", "Read a range of lines")
        {
            readPathOption, offsetOption, limitOption
        };
        readLinesCmd.SetHandler(async (string path, int offset, int? limit) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = File.ReadAllLines(path);
            var start = Math.Clamp(offset, 0, lines.Length);
            var end = limit.HasValue ? Math.Min(start + limit.Value, lines.Length) : lines.Length;
            for (int i = start; i < end; i++)
                Console.WriteLine($"{i + 1,4} | {lines[i]}");
            await Task.CompletedTask;
        }, readPathOption, offsetOption, limitOption);

        var contentOption2 = new Option<string>("--content") { IsRequired = true };
        var writeFileCmd = new Command("write-file", "Write content to a file") { readPathOption, contentOption2 };
        writeFileCmd.SetHandler(async (string path, string content) =>
        {
            File.WriteAllText(path, content);
            Console.WriteLine("File written");
            await Task.CompletedTask;
        }, readPathOption, contentOption2);

        var writeDiffCmd = new Command("write-file-diff", "Show diff then write file")
        {
            readPathOption, contentOption2
        };
        writeDiffCmd.SetHandler(async (string path, string content) =>
        {
            var old = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var diff = Program.GenerateDiff(old, content);
            File.WriteAllText(path, content);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, contentOption2);

        var genWriteDiffCmd = new Command("generate-write-diff", "Preview diff without writing")
        {
            readPathOption, contentOption2
        };
        genWriteDiffCmd.SetHandler(async (string path, string content) =>
        {
            var old = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var diff = Program.GenerateDiff(old, content);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, contentOption2);

        var oldOpt = new Option<string>("--old") { IsRequired = true };
        var newOpt = new Option<string>("--new") { IsRequired = true };
        var editFileCmd = new Command("edit-file", "Replace text in a file")
        {
            readPathOption, oldOpt, newOpt
        };
        editFileCmd.SetHandler(async (string path, string oldStr, string newStr) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var content = File.ReadAllText(path);
            var newContent = content.Replace(oldStr, newStr);
            var diff = Program.GenerateDiff(content, newContent);
            File.WriteAllText(path, newContent);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, oldOpt, newOpt);

        var genEditDiffCmd = new Command("generate-edit-diff", "Preview edit diff")
        {
            readPathOption, oldOpt, newOpt
        };
        genEditDiffCmd.SetHandler(async (string path, string oldStr, string newStr) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var content = File.ReadAllText(path);
            var newContent = content.Replace(oldStr, newStr);
            var diff = Program.GenerateDiff(content, newContent);
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, readPathOption, oldOpt, newOpt);

        var appendContentOption = new Option<string>("--content") { IsRequired = true };
        var appendFileCmd = new Command("append-file", "Append content to a file")
        {
            readPathOption, appendContentOption
        };
        appendFileCmd.SetHandler(async (string path, string content) =>
        {
            await File.AppendAllTextAsync(path, content);
            Console.WriteLine("File appended");
        }, readPathOption, appendContentOption);

        var copyDestOption = new Option<string>("--dest") { IsRequired = true };
        var copyFileCmd = new Command("copy-file", "Copy file to destination")
        {
            readPathOption, copyDestOption
        };
        copyFileCmd.SetHandler(async (string path, string dest) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            File.Copy(path, dest, true);
            Console.WriteLine($"Copied to {dest}");
            await Task.CompletedTask;
        }, readPathOption, copyDestOption);

        var moveDestOption = new Option<string>("--dest") { IsRequired = true };
        var moveFileCmd = new Command("move-file", "Move file to destination")
        {
            readPathOption, moveDestOption
        };
        moveFileCmd.SetHandler(async (string path, string dest) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            File.Move(path, dest, true);
            Console.WriteLine($"Moved to {dest}");
            await Task.CompletedTask;
        }, readPathOption, moveDestOption);

        var renameDestOption = new Option<string>("--new-path") { IsRequired = true };
        var renameFileCmd = new Command("rename-file", "Rename a file")
        {
            readPathOption, renameDestOption
        };
        renameFileCmd.SetHandler(async (string path, string newPath) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            File.Move(path, newPath, true);
            Console.WriteLine($"Renamed to {newPath}");
            await Task.CompletedTask;
        }, readPathOption, renameDestOption);

        var deleteFileCmd = new Command("delete-file", "Delete a file") { readPathOption };
        deleteFileCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            File.Delete(path);
            Console.WriteLine("File deleted");
            await Task.CompletedTask;
        }, readPathOption);

        var fileExistsCmd = new Command("file-exists", "Check if a file exists") { readPathOption };
        fileExistsCmd.SetHandler(async (string path) =>
        {
            Console.WriteLine(File.Exists(path) ? "true" : "false");
            await Task.CompletedTask;
        }, readPathOption);

        var touchFileCmd = new Command("touch-file", "Create empty file if missing") { readPathOption };
        touchFileCmd.SetHandler(async (string path) =>
        {
            FileUtils.Touch(path);
            Console.WriteLine($"Touched {path}");
            await Task.CompletedTask;
        }, readPathOption);

        var dirPathOption = new Option<string>("--path", () => ".");
        var copyDirDestOpt = new Option<string>("--dest") { IsRequired = true };
        var copyDirCmd = new Command("copy-directory", "Copy directory recursively") { dirPathOption, copyDirDestOpt };
        copyDirCmd.SetHandler(async (string path, string dest) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            FileUtils.CopyDirectory(path, dest);
            Console.WriteLine($"Copied {path} to {dest}");
            await Task.CompletedTask;
        }, dirPathOption, copyDirDestOpt);

        var moveDirDestOpt = new Option<string>("--dest") { IsRequired = true };
        var moveDirCmd = new Command("move-directory", "Move directory recursively") { dirPathOption, moveDirDestOpt };
        moveDirCmd.SetHandler(async (string path, string dest) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            FileUtils.MoveDirectory(path, dest);
            Console.WriteLine($"Moved {path} to {dest}");
            await Task.CompletedTask;
        }, dirPathOption, moveDirDestOpt);

        var renameDirNameOpt = new Option<string>("--name") { IsRequired = true };
        var renameDirCmd = new Command("rename-directory", "Rename directory") { dirPathOption, renameDirNameOpt };
        renameDirCmd.SetHandler(async (string path, string name) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            FileUtils.RenameDirectory(path, name);
            Console.WriteLine($"Renamed {path} to {name}");
            await Task.CompletedTask;
        }, dirPathOption, renameDirNameOpt);

        var listDirCmd = new Command("list-directory", "List directory contents") { dirPathOption };
        listDirCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            foreach (var entry in Directory.GetFileSystemEntries(path))
                Console.WriteLine(entry);
            await Task.CompletedTask;
        }, dirPathOption);

        var listDirRecursiveCmd = new Command("list-directory-recursive", "List directory recursively") { dirPathOption };
        listDirRecursiveCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
                Console.WriteLine(entry);
            await Task.CompletedTask;
        }, dirPathOption);

        var headLinesOpt = new Option<int>("--lines", () => 10);
        var headFileCmd = new Command("head-file", "Read first lines of file") { readPathOption, headLinesOpt };
        headFileCmd.SetHandler(async (string path, int lines) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            foreach (var line in File.ReadLines(path).Take(lines)) Console.WriteLine(line);
            await Task.CompletedTask;
        }, readPathOption, headLinesOpt);

        var tailLinesOpt = new Option<int>("--lines", () => 10);
        var tailFileCmd = new Command("tail-file", "Read last lines of file") { readPathOption, tailLinesOpt };
        tailFileCmd.SetHandler(async (string path, int lines) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var allLines = File.ReadAllLines(path);
            foreach (var line in allLines.TakeLast(lines)) Console.WriteLine(line);
            await Task.CompletedTask;
        }, readPathOption, tailLinesOpt);

        var fileSizeCmd = new Command("file-size", "Show size of a file") { readPathOption };
        fileSizeCmd.SetHandler(async (string path) =>
        {
            var size = File.Exists(path) ? new FileInfo(path).Length : 0;
            Console.WriteLine(size);
            await Task.CompletedTask;
        }, readPathOption);

        var createDirCmd = new Command("create-directory", "Create a directory") { dirPathOption };
        createDirCmd.SetHandler(async (string path) =>
        {
            Directory.CreateDirectory(path);
            Console.WriteLine($"Created {path}");
            await Task.CompletedTask;
        }, dirPathOption);

        var deleteDirCmd = new Command("delete-directory", "Delete a directory") { dirPathOption };
        deleteDirCmd.SetHandler(async (string path) =>
        {
            if (!Directory.Exists(path)) { Console.WriteLine("Directory not found"); return; }
            Directory.Delete(path, true);
            Console.WriteLine("Directory deleted");
            await Task.CompletedTask;
        }, dirPathOption);

        var dirExistsCmd = new Command("dir-exists", "Check if directory exists") { dirPathOption };
        dirExistsCmd.SetHandler(async (string path) =>
        {
            Console.WriteLine(Directory.Exists(path) ? "true" : "false");
            await Task.CompletedTask;
        }, dirPathOption);

        var fileInfoCmd = new Command("file-info", "Show file metadata") { readPathOption };
        fileInfoCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path) && !Directory.Exists(path)) { Console.WriteLine("Path not found"); return; }
            var info = new FileInfo(path);
            Console.WriteLine($"Path: {info.FullName}\nSize: {info.Length} bytes\nModified: {info.LastWriteTime}");
            await Task.CompletedTask;
        }, readPathOption);

        var countLinesCmd = new Command("count-lines", "Count lines in a file") { readPathOption };
        countLinesCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var lines = await File.ReadAllLinesAsync(path);
            Console.WriteLine(lines.Length);
        }, readPathOption);

        root.Add(readFileCmd);
        root.Add(readNumberedCmd);
        root.Add(readLinesCmd);
        root.Add(writeFileCmd);
        root.Add(writeDiffCmd);
        root.Add(genWriteDiffCmd);
        root.Add(editFileCmd);
        root.Add(genEditDiffCmd);
        root.Add(appendFileCmd);
        root.Add(copyFileCmd);
        root.Add(moveFileCmd);
        root.Add(renameFileCmd);
        root.Add(deleteFileCmd);
        root.Add(fileExistsCmd);
        root.Add(touchFileCmd);
        root.Add(copyDirCmd);
        root.Add(moveDirCmd);
        root.Add(renameDirCmd);
        root.Add(listDirCmd);
        root.Add(listDirRecursiveCmd);
        root.Add(headFileCmd);
        root.Add(tailFileCmd);
        root.Add(fileSizeCmd);
        root.Add(createDirCmd);
        root.Add(deleteDirCmd);
        root.Add(dirExistsCmd);
        root.Add(fileInfoCmd);
        root.Add(countLinesCmd);

        var binPathArg = new Argument<string>("path");
        var base64Opt = new Option<string>("--base64") { IsRequired = true };
        var readBinaryCmd = new Command("read-binary-file", "Read file as base64") { binPathArg };
        readBinaryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var bytes = await File.ReadAllBytesAsync(path);
            Console.WriteLine(Convert.ToBase64String(bytes));
        }, binPathArg);

        var writeBinaryCmd = new Command("write-binary-file", "Write base64 content to file") { binPathArg, base64Opt };
        writeBinaryCmd.SetHandler(async (string path, string b64) =>
        {
            byte[] data;
            try { data = Convert.FromBase64String(b64); }
            catch { Console.WriteLine("Invalid base64 data"); return; }
            await File.WriteAllBytesAsync(path, data);
            Console.WriteLine("Binary file written");
        }, binPathArg, base64Opt);

        var fileHashCmd = new Command("file-hash", "Compute SHA256 hash of a file") { binPathArg };
        fileHashCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            using var sha = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(path);
            var hash = await sha.ComputeHashAsync(stream);
            Console.WriteLine(Convert.ToHexString(hash).ToLower());
        }, binPathArg);

        var fileWordCountCmd = new Command("file-word-count", "Count words in a file") { binPathArg };
        fileWordCountCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var text = await File.ReadAllTextAsync(path);
            var count = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            Console.WriteLine(count);
        }, binPathArg);

        root.Add(readBinaryCmd);
        root.Add(writeBinaryCmd);
        root.Add(fileHashCmd);
        root.Add(fileWordCountCmd);
    }
}
