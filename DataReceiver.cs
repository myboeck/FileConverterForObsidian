using System.Diagnostics;
using System.Text.Json;

namespace ObsidianGitMirror
{
    internal class DataReceiver
    {
        private readonly string _repoPath;
        private readonly HashSet<string> _extensions;

        public DataReceiver(string configPath)
        {
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigModel>(configJson)!;
            _repoPath = config.RepositoryPath;
            _extensions = config.AcceptedExtensions != null
                ? new HashSet<string>(config.AcceptedExtensions, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();
        }

        public List<string> GetChangedFiles()
        {
            var changedFiles = new List<string>();

            // Git-Befehl für letzte Commit-Änderungen (Hinzugefügt, Modifiziert)
            var gitArgs = "diff --name-status HEAD~1 HEAD";

            Console.WriteLine($"\n[Git] Ausgeführt im Repo: {_repoPath}");
            Console.WriteLine($"[Git] Befehl: git {gitArgs}\n");

            foreach (var ext in _extensions)
            {
                Console.WriteLine($"  • {ext}");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c git {gitArgs}",
                    WorkingDirectory = _repoPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                Console.WriteLine($"[Git-Output] {line}");

                var parts = line.Split('\t');
                if (parts.Length < 2)
                {
                    Console.WriteLine("[Warnung] Ungültige Zeile, wird übersprungen.");
                    continue;
                }

                var status = parts[0].Trim();
                var fileRelPath = parts[1].Trim();
                var fullPath = Path.Combine(_repoPath, fileRelPath);
                var ext = Path.GetExtension(fileRelPath).ToLowerInvariant();

                Console.WriteLine($"  ↪ Status: {status}, Datei: {fileRelPath}, Extension: {ext}");

                if ((status == "A" || status == "M") && _extensions.Contains(ext))
                {
                    Console.WriteLine($"  ✅ Datei akzeptiert: {fullPath}");
                    changedFiles.Add(fullPath);
                }
                else
                {
                    Console.WriteLine($"  ⛔ Datei ignoriert (Status: {status}, Extension: {ext})");
                }
            }

            Console.WriteLine($"\n[Git] Insgesamt akzeptierte Dateien: {changedFiles.Count}");
            return changedFiles;
        }
    }
}
