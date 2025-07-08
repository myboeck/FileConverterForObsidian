using System.Text;

namespace ObsidianGitMirror
{
    public class DataWriter
    {
        private readonly string _vaultRoot;

        public DataWriter(string vaultRoot)
        {
            _vaultRoot = vaultRoot;
        }

        public void WriteMarkdownFile(string relativeSourcePath, string markdown)
        {
            string ext = Path.GetExtension(relativeSourcePath);
            string outputDir = Path.Combine(_vaultRoot, "converted", Path.GetDirectoryName(relativeSourcePath) ?? "");

            string outputFileName = ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(relativeSourcePath)  // keep it as-is
                : Path.GetFileName(relativeSourcePath) + ".md";  // add .md

            string fullOutputPath = Path.Combine(outputDir, outputFileName);

            try
            {
                Directory.CreateDirectory(outputDir);
                File.WriteAllText(fullOutputPath, markdown, Encoding.UTF8);
                Console.WriteLine($"[✓] Gespeichert: {Path.GetRelativePath(_vaultRoot, fullOutputPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fehler] Schreiben fehlgeschlagen für: {relativeSourcePath} → {ex.Message}");
            }
        }
    }
}
