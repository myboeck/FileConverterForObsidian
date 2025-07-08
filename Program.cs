using Spectre.Console;
using System.Text.Json;

namespace ObsidianGitMirror
{
    internal class Program
    {
        private static string? _configPath = "config.json";
        private static DateTime _lastProcessedCommit = DateTime.MinValue;
        const long PATCH_LIMIT_BYTES = 4L * 1024 * 1024 * 1024; // 4 GB
        public static readonly List<string> DefaultExtensions = new()
    {
        ".cs", ".py", ".js", ".java", ".ts", ".html", ".css",
        ".md", ".txt", ".json", ".cpp", ".c", ".xlsx", ".docx"
    };

        [STAThread]
        static void Main(string[] args)
        {
            bool running = true;
            Console.WriteLine("👀 GitToObsidianSync gestartet…");

            ConfigModel? config = null;
            string headLogPath = "";

            while (running)
            {
                ConfigWizard.Run(_configPath!);

                try
                {
                    var configJson = File.ReadAllText(_configPath!);
                    config = JsonSerializer.Deserialize<ConfigModel>(configJson)!;

                    if (string.IsNullOrWhiteSpace(config.RepositoryPath) || !Directory.Exists(config.RepositoryPath))
                    {
                        AnsiConsole.MarkupLine("[red]❌ Repository path not set or does not exist.[/]");
                        AnsiConsole.MarkupLine("[grey]Drücke [underline]Enter[/] um fortzufahren...[/]");
                        Console.ReadLine();
                        continue;
                    }

                    headLogPath = Path.Combine(config.RepositoryPath, ".git", "logs", "HEAD");
                    if (!File.Exists(headLogPath))
                    {
                        AnsiConsole.MarkupLine($"[red]❌ Kein gültiges Git-Repository gefunden unter:[/] [blue]{config.RepositoryPath}[/]");
                        AnsiConsole.MarkupLine("[grey]Drücke [underline]Enter[/] um fortzufahren...[/]");
                        Console.ReadLine();
                        continue;
                    }

                    break; // Alles ok
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ Fehler beim Laden der config.json:[/] {ex.Message}");
                    AnsiConsole.MarkupLine("[grey]Drücke [underline]Enter[/] um fortzufahren...[/]");
                    Console.ReadLine();
                    continue;
                }
            }

            // Watcher initialisieren
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(headLogPath)!);
            watcher.Filter = "HEAD";
            watcher.NotifyFilter = NotifyFilters.LastWrite;

            watcher.Changed += (sender, e) =>
            {
                Thread.Sleep(100);

                var commitTime = File.GetLastWriteTime(headLogPath);
                if (commitTime <= _lastProcessedCommit)
                    return;

                _lastProcessedCommit = commitTime;
                Console.WriteLine($"\n🔁 Commit erkannt ({commitTime}) – starte Verarbeitung…");

                TriggerPipeline(config!);
            };

            watcher.EnableRaisingEvents = true;

            Console.WriteLine("⏳ Warte auf Commits...");
            Console.WriteLine("✴ Drücke [Ctrl]+K gefolgt von [S] für Sync oder [Q] zum Beenden.");

            bool awaitingCombo = false;

            while (running)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (!awaitingCombo)
                {
                    // Warte auf Ctrl+K
                    if (keyInfo.Key == ConsoleKey.K && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        awaitingCombo = true;
                        Console.Write("(K) ➝ ");
                    }
                }
                else
                {
                    awaitingCombo = false;

                    if (keyInfo.Key == ConsoleKey.S)
                    {
                        Console.WriteLine("🧠 Manuelle Synchronisierung wird ausgeführt...");
                        TriggerPipeline(config!);
                    }
                    else if (keyInfo.Key == ConsoleKey.Q)
                    {
                        running = false;
                        break;
                    }
                    else
                    {
                        Console.WriteLine("⛔ Ungültige Tastenkombination. Drücke [Ctrl]+K gefolgt von [S] oder [Q].");
                    }
                }
            }
        }


        static void TriggerPipeline(ConfigModel config)
        {
            var receiver = new DataReceiver(_configPath!);
            var converter = new DataConverter();
            var writer = new DataWriter(config.VaultOutputPath);

            var changedFiles = receiver.GetChangedFiles();
            var patchLimitBytes = PATCH_LIMIT_BYTES;

            var patch = new List<string>();
            long currentPatchSize = 0;
            int patchIndex = 1;

            foreach (var file in changedFiles)
            {
                var fileInfo = new FileInfo(file);
                long fileSize = fileInfo.Exists ? fileInfo.Length : 0;

                if ((currentPatchSize + fileSize) > patchLimitBytes && patch.Any())
                {
                    ProcessPatch(config, patch, converter, writer, config.RepositoryPath, patchIndex++);
                    patch.Clear();
                    currentPatchSize = 0;
                }

                patch.Add(file);
                currentPatchSize += fileSize;
            }

            // Letzten Patch verarbeiten
            if (patch.Any())
            {
                ProcessPatch(config, patch, converter, writer, config.RepositoryPath, patchIndex++);
            }

            Console.WriteLine($"✅ {changedFiles.Count} Datei(en) verarbeitet in {patchIndex - 1} Patch(es).");
            Console.WriteLine("⏳ Warte auf Commits...");
            Console.WriteLine("✴ Drücke [Ctrl]+K gefolgt von [S] für Sync oder [Q] zum Beenden.");
        }

        static void ProcessPatch(ConfigModel config, List<string> files, DataConverter converter, DataWriter writer, string repoPath, int patchNumber)
        {
            Console.WriteLine($"\n📦 Patch #{patchNumber} – {files.Count} Datei(en) werden verarbeitet…");

            foreach (var filePath in files)
            {
                try
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();

                    // Prüfe, ob die Datei überhaupt geöffnet werden kann
                    if (IsFileLocked(filePath, out string errorMessage))
                    {
                        Console.WriteLine($"[🔒 Gesperrt] Datei {filePath} konnte nicht geöffnet werden.");
                        Console.WriteLine(errorMessage);
                        continue; // zur nächsten Datei springen
                    }

                    if (converter.IsExcel(ext))
                    {
                        var excelFiles = converter.ConvertExcelToMarkdown(filePath);
                        foreach (var (relPath, md) in excelFiles)
                        {
                            writer.WriteMarkdownFile(relPath, md);
                        }
                    }
                    else
                    {
                        var relPath = Path.GetRelativePath(config.RepositoryPath, filePath);
                        var (mdContent, _) = converter.ConvertToMarkdown(filePath);
                        writer.WriteMarkdownFile(relPath, mdContent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fehler] Datei {filePath} konnte nicht verarbeitet werden: {ex.Message}");
                }
            }

            GC.Collect(); // Optional: Speicher aufräumen
            Console.WriteLine($"✅ Patch #{patchNumber} abgeschlossen.");
        }

        public static bool IsFileLocked(string filePath, out string reason)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    reason = "";
                    return false;
                }
            }
            catch (IOException ex)
            {
                reason = ex.Message;
                return true;
            }
        }
    }
}