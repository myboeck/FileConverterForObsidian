using Spectre.Console;
using System.Diagnostics;
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
            Console.WriteLine("✴ Drücke [Ctrl]+K gefolgt von [S] für Sync, [I] für Full Sync, oder [Q] zum Beenden.");

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
                    else if (keyInfo.Key == ConsoleKey.I)
                    {
                        Console.WriteLine("🚀 Starte Vollständige Repository-Synchronisierung (alle Dateien)...");
                        SyncAllFilesInRepo(config!);
                    }
                    else if (keyInfo.Key == ConsoleKey.Q)
                    {
                        running = false;
                        break;
                    }
                    else
                    {
                        Console.WriteLine("⛔ Ungültige Tastenkombination. Drücke [Ctrl]+K gefolgt von [S], [I] oder [Q].");
                    }
                }
            }
        }

        static void TriggerPipeline(ConfigModel config)
        {
            var receiver = new DataReceiver(_configPath!);
            var changedFiles = receiver.GetChangedFiles();

            if (changedFiles.Count == 0)
            {
                Console.WriteLine("ℹ️ Keine Änderungen gefunden.");
                Console.WriteLine("⏳ Warte auf Commits...");
                Console.WriteLine("✴ Drücke [Ctrl]+K gefolgt von [S], [I] oder [Q].");
                return;
            }

            var patchLimitBytes = PATCH_LIMIT_BYTES;

            var patch = new List<string>();
            long currentPatchSize = 0;
            int patchIndex = 1;

            var converter = new DataConverter();
            var writer = new DataWriter(config.VaultOutputPath);

            foreach (var file in changedFiles)
            {
                var fileInfo = new FileInfo(file);
                long fileSize = fileInfo.Exists ? fileInfo.Length : 0;

                if ((currentPatchSize + fileSize) > patchLimitBytes && patch.Any())
                {
                    Console.WriteLine($"\n📦 Patch #{patchIndex} – Verarbeite {patch.Count} Dateien...");
                    SyncFiles(patch, config);
                    patch.Clear();
                    currentPatchSize = 0;
                    patchIndex++;
                }

                patch.Add(file);
                currentPatchSize += fileSize;
            }

            if (patch.Any())
            {
                Console.WriteLine($"\n📦 Patch #{patchIndex} – Verarbeite {patch.Count} Dateien...");
                SyncFiles(patch, config);
            }

            Console.WriteLine($"✅ {changedFiles.Count} Datei(en) verarbeitet in {patchIndex} Patch(es).");
            Console.WriteLine("⏳ Warte auf Commits...");
            Console.WriteLine("✴ Drücke [Ctrl]+K gefolgt von [S], [I] oder [Q].");
        }

        /// <summary>
        /// Universelle Methode zum Synchronisieren einzelner Dateien
        /// </summary>
        static void SyncFiles(List<string> files, ConfigModel config)
        {
            var converter = new DataConverter();
            var writer = new DataWriter(config.VaultOutputPath);

            int fileIndex = 0;
            foreach (var filePath in files)
            {
                fileIndex++;
                try
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();

                    if (IsFileLocked(filePath, out string errorMessage))
                    {
                        Console.WriteLine($"[🔒 Gesperrt] Datei {filePath} konnte nicht geöffnet werden: {errorMessage}");
                        continue;
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

            GC.Collect();
            Console.WriteLine("✅ Synchronisierung abgeschlossen.");
        }

        /// <summary>
        /// Komplettes Repository durchsuchen und alle unterstützten Dateien synchronisieren
        /// </summary>
        static void SyncAllFilesInRepo(ConfigModel config)
        {
            var repoPath = config.RepositoryPath;

            var allFiles = Directory.EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
                .Where(file => DefaultExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"🌍 {allFiles.Count} unterstützte Dateien gefunden.");

            SyncFiles(allFiles, config);

            Console.WriteLine("✅ Vollständige Repository-Synchronisierung abgeschlossen.");
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