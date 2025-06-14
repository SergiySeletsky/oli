using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class MemoryCommands
{
    public static void Register(RootCommand root)
    {
        // memory-info
        var memoryInfoCmd = new Command("memory-info", "Show memory file path and content");
        memoryInfoCmd.SetHandler(async () =>
        {
            if (File.Exists(Program.MemoryPath))
            {
                Console.WriteLine($"Memory path: {Program.MemoryPath}");
                Console.WriteLine(File.ReadAllText(Program.MemoryPath));
            }
            else
            {
                Console.WriteLine("No memory file found");
            }
            await Task.CompletedTask;
        });

        // add-memory
        var sectionOption = new Option<string>("--section") { IsRequired = true };
        var memoryOption = new Option<string>("--memory") { IsRequired = true };
        var addMemoryCmd = new Command("add-memory", "Add a memory entry") { sectionOption, memoryOption };
        addMemoryCmd.SetHandler(async (string section, string memory) =>
        {
            var content = File.Exists(Program.MemoryPath) ? File.ReadAllText(Program.MemoryPath) : "";
            var header = $"## {section}";
            if (!content.Contains(header)) content += $"\n{header}\n";
            var idx = content.IndexOf(header) + header.Length;
            content = content.Insert(idx, $"\n- {memory}");
            File.WriteAllText(Program.MemoryPath, content);
            Console.WriteLine("Memory added");
            await Task.CompletedTask;
        }, sectionOption, memoryOption);

        // replace-memory-file
        var contentOption = new Option<string>("--content") { IsRequired = true };
        var replaceMemoryCmd = new Command("replace-memory-file", "Replace entire memory file") { contentOption };
        replaceMemoryCmd.SetHandler(async (string content) =>
        {
            File.WriteAllText(Program.MemoryPath, content);
            Console.WriteLine("Memory file replaced");
            await Task.CompletedTask;
        }, contentOption);

        // append-memory-file
        var appendContentOpt = new Option<string>("--content") { IsRequired = true };
        var appendMemoryCmd = new Command("append-memory-file", "Append text to memory file") { appendContentOpt };
        appendMemoryCmd.SetHandler(async (string content) =>
        {
            var existing = File.Exists(Program.MemoryPath) ? File.ReadAllText(Program.MemoryPath) : string.Empty;
            File.WriteAllText(Program.MemoryPath, existing + content);
            Console.WriteLine("Memory file updated");
            await Task.CompletedTask;
        }, appendContentOpt);

        // import-memory-file
        var importMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var importMemoryCmd = new Command("import-memory-file", "Load memory file from path") { importMemoryPathOpt };
        importMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            File.Copy(path, Program.MemoryPath, true);
            Console.WriteLine("Memory file imported");
            await Task.CompletedTask;
        }, importMemoryPathOpt);

        // export-memory-file
        var exportMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var exportMemoryCmd = new Command("export-memory-file", "Write memory file to path") { exportMemoryPathOpt };
        exportMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            File.Copy(Program.MemoryPath, path, true);
            Console.WriteLine($"Memory file exported to {path}");
            await Task.CompletedTask;
        }, exportMemoryPathOpt);

        // memory-lines
        var linesStartOpt = new Option<int>("--start", () => 1);
        var linesEndOpt = new Option<int>("--end", () => int.MaxValue);
        var memoryLinesCmd = new Command("memory-lines", "Show lines from memory file") { linesStartOpt, linesEndOpt };
        memoryLinesCmd.SetHandler(async (int start, int end) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            start = Math.Max(1, start); end = Math.Min(lines.Length, end);
            for (int i = start; i <= end; i++) Console.WriteLine($"{i}: {lines[i-1]}");
            await Task.CompletedTask;
        }, linesStartOpt, linesEndOpt);

        // memory-head
        var memHeadLinesOpt = new Option<int>("--lines", () => 10);
        var memoryHeadCmd = new Command("memory-head", "Show first lines of memory") { memHeadLinesOpt };
        memoryHeadCmd.SetHandler(async (int lines) =>
        {
            Console.WriteLine(MemoryUtils.Head(Program.MemoryPath, lines));
            await Task.CompletedTask;
        }, memHeadLinesOpt);

        // memory-tail
        var memTailLinesOpt = new Option<int>("--lines", () => 10);
        var memoryTailCmd = new Command("memory-tail", "Show last lines of memory") { memTailLinesOpt };
        memoryTailCmd.SetHandler(async (int lines) =>
        {
            Console.WriteLine(MemoryUtils.Tail(Program.MemoryPath, lines));
            await Task.CompletedTask;
        }, memTailLinesOpt);

        // insert-memory-lines
        var insertIndexOpt = new Option<int>("--index") { IsRequired = true };
        var insertTextOpt = new Option<string>("--text") { IsRequired = true };
        var insertMemoryCmd = new Command("insert-memory-lines", "Insert lines into memory file") { insertIndexOpt, insertTextOpt };
        insertMemoryCmd.SetHandler(async (int index, string text) =>
        {
            var lines = File.Exists(Program.MemoryPath) ? File.ReadAllLines(Program.MemoryPath).ToList() : new List<string>();
            var newLines = text.Split('\n');
            index = Math.Clamp(index - 1, 0, lines.Count);
            lines.InsertRange(index, newLines);
            File.WriteAllLines(Program.MemoryPath, lines);
            Console.WriteLine("Memory updated");
            await Task.CompletedTask;
        }, insertIndexOpt, insertTextOpt);

        // replace-memory-lines
        var replaceStartOpt = new Option<int>("--start") { IsRequired = true };
        var replaceEndOpt = new Option<int>("--end") { IsRequired = true };
        var replaceTextOpt = new Option<string>("--text") { IsRequired = true };
        var replaceMemoryLinesCmd = new Command("replace-memory-lines", "Replace range of memory lines") { replaceStartOpt, replaceEndOpt, replaceTextOpt };
        replaceMemoryLinesCmd.SetHandler(async (int start, int end, string text) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).ToList();
            start = Math.Max(1, start); end = Math.Min(lines.Count, end);
            lines.RemoveRange(start - 1, Math.Max(0, end - start + 1));
            lines.InsertRange(start - 1, text.Split('\n'));
            File.WriteAllLines(Program.MemoryPath, lines);
            Console.WriteLine("Memory updated");
            await Task.CompletedTask;
        }, replaceStartOpt, replaceEndOpt, replaceTextOpt);

        // merge-memory-file
        var mergeMemoryPathOpt = new Option<string>("--path") { IsRequired = true };
        var mergeMemoryCmd = new Command("merge-memory-file", "Merge another memory file") { mergeMemoryPathOpt };
        mergeMemoryCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
            var existing = File.Exists(Program.MemoryPath) ? File.ReadAllText(Program.MemoryPath) : string.Empty;
            var extra = File.ReadAllText(path);
            File.WriteAllText(Program.MemoryPath, existing + extra);
            Console.WriteLine("Memory files merged");
            await Task.CompletedTask;
        }, mergeMemoryPathOpt);

        // reset-memory-file
        var resetMemoryCmd = new Command("reset-memory-file", "Delete and recreate memory file");
        resetMemoryCmd.SetHandler(async () =>
        {
            var template = "# oli.md\n\n## Project Structure\n";
            File.WriteAllText(Program.MemoryPath, template);
            Console.WriteLine("Memory file reset");
            await Task.CompletedTask;
        });

        // memory-path
        var memoryPathCmd = new Command("memory-path", "Show path of memory file");
        memoryPathCmd.SetHandler(async () =>
        {
            Console.WriteLine(Program.MemoryPath);
            await Task.CompletedTask;
        });

        // memory-exists
        var memoryExistsCmd = new Command("memory-exists", "Check for memory file");
        memoryExistsCmd.SetHandler(async () =>
        {
            Console.WriteLine(File.Exists(Program.MemoryPath) ? "true" : "false");
            await Task.CompletedTask;
        });

        // create-memory-file
        var createMemoryCmd = new Command("create-memory-file", "Create memory file if missing");
        createMemoryCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath))
            {
                var template = "# oli.md\n\n## Project Structure\n";
                File.WriteAllText(Program.MemoryPath, template);
                Console.WriteLine("Memory file created");
            }
            else
            {
                Console.WriteLine("Memory file already exists");
            }
            await Task.CompletedTask;
        });

        // parsed-memory
        var parseMemoryCmd = new Command("parsed-memory", "Show parsed memory sections");
        parseMemoryCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            string? current = null;
            var map = new Dictionary<string, List<string>>();
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    current = line[3..];
                    map[current] = new List<string>();
                }
                else if (line.StartsWith("- ") && current != null)
                {
                    map[current].Add(line[2..]);
                }
            }
            foreach (var kv in map)
            {
                Console.WriteLine($"Section: {kv.Key}");
                foreach (var entry in kv.Value) Console.WriteLine($"  - {entry}");
            }
            await Task.CompletedTask;
        });

        // copy-memory-section
        var copySectionOpt = new Option<string>("--section") { IsRequired = true };
        var copyDestOpt = new Option<string>("--dest") { IsRequired = true };
        var copySectionCmd = new Command("copy-memory-section", "Copy memory section to file") { copySectionOpt, copyDestOpt };
        copySectionCmd.SetHandler(async (string section, string dest) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var collecting = false;
            var selected = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    collecting = line[3..].Trim() == section;
                    continue;
                }
                if (collecting && line.StartsWith("- ")) selected.Add(line);
            }
            File.WriteAllLines(dest, selected);
            Console.WriteLine($"Section copied to {dest}");
            await Task.CompletedTask;
        }, copySectionOpt, copyDestOpt);

        // memory-section-lines
        var sectionLinesOpt = new Option<string>("--section") { IsRequired = true };
        var memorySectionLinesCmd = new Command("memory-section-lines", "Show lines for a memory section") { sectionLinesOpt };
        memorySectionLinesCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            bool capture = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    if (capture) break;
                    capture = line[3..].Trim() == section;
                    continue;
                }
                if (capture && line.StartsWith("- ")) Console.WriteLine(line[2..]);
            }
            await Task.CompletedTask;
        }, sectionLinesOpt);

        // rename-memory-section
        var renameOldOpt = new Option<string>("--old") { IsRequired = true };
        var renameNewOpt = new Option<string>("--new") { IsRequired = true };
        var renameSectionCmd = new Command("rename-memory-section", "Rename a memory section") { renameOldOpt, renameNewOpt };
        renameSectionCmd.SetHandler(async (string old, string @new) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == $"## {old}")
                {
                    lines[i] = $"## {@new}";
                    File.WriteAllLines(Program.MemoryPath, lines);
                    Console.WriteLine("renamed");
                    await Task.CompletedTask;
                    return;
                }
            }
            Console.WriteLine("section not found");
            await Task.CompletedTask;
        }, renameOldOpt, renameNewOpt);

        // memory-sort-lines
        var sortLinesCmd = new Command("memory-sort-lines", "Sort memory file lines alphabetically");
        sortLinesCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .OrderBy(l => l)
                .ToArray();
            File.WriteAllLines(Program.MemoryPath, lines);
            Console.WriteLine("sorted lines");
            await Task.CompletedTask;
        });

        // swap-memory-sections
        var swapAOpt = new Option<string>("--first") { IsRequired = true };
        var swapBOpt = new Option<string>("--second") { IsRequired = true };
        var swapSectionCmd = new Command("swap-memory-sections", "Swap two memory sections") { swapAOpt, swapBOpt };
        swapSectionCmd.SetHandler(async (string first, string second) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).ToList();
            int idxA = lines.FindIndex(l => l.StartsWith("## "+first));
            int idxB = lines.FindIndex(l => l.StartsWith("## "+second));
            if (idxA == -1 || idxB == -1) { Console.WriteLine("Section not found"); return; }
            if (idxA > idxB) { var t = idxA; idxA = idxB; idxB = t; var tmp = first; first = second; second = tmp; }
            int endA = lines.Skip(idxA+1).TakeWhile(l => !l.StartsWith("## ")).Count();
            int endB = lines.Skip(idxB+1).TakeWhile(l => !l.StartsWith("## ")).Count();
            var secA = lines.GetRange(idxA, endA+1);
            var secB = lines.GetRange(idxB, endB+1);
            lines.RemoveRange(idxB, endB+1);
            lines.InsertRange(idxB, secA);
            lines.RemoveRange(idxA, endA+1);
            lines.InsertRange(idxA, secB);
            File.WriteAllLines(Program.MemoryPath, lines);
            Console.WriteLine("Sections swapped");
            await Task.CompletedTask;
        }, swapAOpt, swapBOpt);

        // memory-section-count
        var sectionCountCmd = new Command("memory-section-count", "Count memory sections");
        sectionCountCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("0"); return; }
            var count = File.ReadLines(Program.MemoryPath).Count(l => l.StartsWith("## "));
            Console.WriteLine(count);
            await Task.CompletedTask;
        });

        // memory-entry-count
        var entryCountCmd = new Command("memory-entry-count", "Count total memory entries");
        entryCountCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("0"); return; }
            var count = File.ReadLines(Program.MemoryPath).Count(l => l.StartsWith("- "));
            Console.WriteLine(count);
            await Task.CompletedTask;
        });

        // memory-template
        var memoryTemplateCmd = new Command("memory-template", "Show default memory template");
        memoryTemplateCmd.SetHandler(async () =>
        {
            Console.WriteLine("# oli.md\n\n## Project Structure\n- example\n\n## Build Commands\n- example\n\n## Test Commands\n- example\n\n## Architecture\n- example");
            await Task.CompletedTask;
        });

        // memory-size
        var memorySizeCmd = new Command("memory-size", "Show memory file size");
        memorySizeCmd.SetHandler(async () =>
        {
            var size = File.Exists(Program.MemoryPath) ? new FileInfo(Program.MemoryPath).Length : 0;
            Console.WriteLine(size);
            await Task.CompletedTask;
        });

        // search-memory
        var memSearchTextOpt = new Option<string>("--text") { IsRequired = true };
        var searchMemoryCmd = new Command("search-memory", "Search memory for text") { memSearchTextOpt };
        searchMemoryCmd.SetHandler(async (string text) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            foreach (var line in File.ReadLines(Program.MemoryPath))
                if (line.Contains(text, StringComparison.OrdinalIgnoreCase)) Console.WriteLine(line);
            await Task.CompletedTask;
        }, memSearchTextOpt);

        // delete-memory-lines
        var deletePatternOpt = new Option<string>("--pattern") { IsRequired = true };
        var deleteMemoryLineCmd = new Command("delete-memory-lines", "Remove memory lines matching pattern") { deletePatternOpt };
        deleteMemoryLineCmd.SetHandler(async (string pattern) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath).ToList();
            var remaining = lines.Where(l => !l.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
            File.WriteAllLines(Program.MemoryPath, remaining);
            Console.WriteLine(lines.Count - remaining.Count);
            await Task.CompletedTask;
        }, deletePatternOpt);

        // memory-dedupe-lines
        var memoryDedupeCmd = new Command("memory-dedupe-lines", "Remove duplicate lines in memory");
        memoryDedupeCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var deduped = lines.Distinct().ToArray();
            File.WriteAllLines(Program.MemoryPath, deduped);
            Console.WriteLine(lines.Length - deduped.Length);
            await Task.CompletedTask;
        });

        var diffArg = new Argument<string>("path");
        var memoryDiffCmd = new Command("memory-diff", "Diff memory file with another") { diffArg };
        memoryDiffCmd.SetHandler(async (string path) =>
        {
            if (!File.Exists(path) || !File.Exists(Program.MemoryPath)) { Console.WriteLine("file missing"); return; }
            var diff = Program.GenerateDiff(File.ReadAllText(Program.MemoryPath), File.ReadAllText(path));
            Console.WriteLine(diff);
            await Task.CompletedTask;
        }, diffArg);

        // delete-memory-section
        var deleteSectionOption = new Option<string>("--section") { IsRequired = true };
        var deleteMemorySectionCmd = new Command("delete-memory-section", "Remove a memory section") { deleteSectionOption };
        deleteMemorySectionCmd.SetHandler(async (string section) =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            var lines = File.ReadAllLines(Program.MemoryPath);
            var result = new List<string>();
            bool skip = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    skip = line[3..] == section;
                    if (!skip) result.Add(line);
                    continue;
                }
                if (!skip) result.Add(line);
            }
            File.WriteAllLines(Program.MemoryPath, result);
            Console.WriteLine($"Deleted section {section}");
            await Task.CompletedTask;
        }, deleteSectionOption);

        // list-memory-sections
        var listMemorySectionsCmd = new Command("list-memory-sections", "List memory sections");
        listMemorySectionsCmd.SetHandler(async () =>
        {
            if (!File.Exists(Program.MemoryPath)) { Console.WriteLine("No memory file found"); return; }
            foreach (var line in File.ReadLines(Program.MemoryPath))
                if (line.StartsWith("## ")) Console.WriteLine(line[3..]);
            await Task.CompletedTask;
        });

        // memory-section-exists
        var sectionExistsArg = new Argument<string>("section");
        var sectionExistsCmd = new Command("memory-section-exists", "Check if a memory section exists") { sectionExistsArg };
        sectionExistsCmd.SetHandler(async (string section) =>
        {
            bool exists = File.Exists(Program.MemoryPath) &&
                File.ReadLines(Program.MemoryPath).Any(l => l.Trim() == $"## {section}");
            Console.WriteLine(exists ? "true" : "false");
            await Task.CompletedTask;
        }, sectionExistsArg);

        root.AddCommand(memoryInfoCmd);
        root.AddCommand(addMemoryCmd);
        root.AddCommand(replaceMemoryCmd);
        root.AddCommand(appendMemoryCmd);
        root.AddCommand(importMemoryCmd);
        root.AddCommand(exportMemoryCmd);
        root.AddCommand(memoryLinesCmd);
        root.AddCommand(memoryHeadCmd);
        root.AddCommand(memoryTailCmd);
        root.AddCommand(insertMemoryCmd);
        root.AddCommand(replaceMemoryLinesCmd);
        root.AddCommand(mergeMemoryCmd);
        root.AddCommand(resetMemoryCmd);
        root.AddCommand(memoryPathCmd);
        root.AddCommand(memoryExistsCmd);
        root.AddCommand(createMemoryCmd);
        root.AddCommand(parseMemoryCmd);
        root.AddCommand(copySectionCmd);
        root.AddCommand(memorySectionLinesCmd);
        root.AddCommand(renameSectionCmd);
        root.AddCommand(sortLinesCmd);
        root.AddCommand(swapSectionCmd);
        root.AddCommand(sectionCountCmd);
        root.AddCommand(entryCountCmd);
        root.AddCommand(memoryTemplateCmd);
        root.AddCommand(memorySizeCmd);
        root.AddCommand(searchMemoryCmd);
        root.AddCommand(deleteMemoryLineCmd);
        root.AddCommand(memoryDedupeCmd);
        root.AddCommand(memoryDiffCmd);
        root.AddCommand(deleteMemorySectionCmd);
        root.AddCommand(listMemorySectionsCmd);
        root.AddCommand(sectionExistsCmd);
    }
}
