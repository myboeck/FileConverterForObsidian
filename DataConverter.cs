using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;

namespace FileConverter
{
    public class DataConverter
    {
        // Enthält jetzt alle unterstützten textbasierten Dateiendungen
        private static readonly HashSet<string> TextBasedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".py", ".js", ".ts", ".java", ".c", ".cpp", ".html", ".css", ".json", ".txt", ".cs"
        };

        // Liste der übersprungenen Dateien
        public List<string> SkippedFiles { get; private set; } = new();

        /// <summary>
        /// Konvertiert alle unterstützten Dateien in einem Ordner und seinen Unterordnern in Markdown.
        /// </summary>
        /// <param name="folderPath">Der Pfad zum Stammordner.</param>
        public void ConvertFolder(string folderPath)
        {
            SkippedFiles.Clear();

            var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                var relPath = Path.GetRelativePath(folderPath, filePath);
                var ext = Path.GetExtension(filePath).ToLowerInvariant();

                // Ignoriere Dateien im Zielordner, um Endlosschleifen zu vermeiden
                if (filePath.Contains(Path.Combine(folderPath, "convertedfiles")))
                {
                    continue;
                }

                string? content = null;
                string lang = "text";

                try
                {
                    // Wähle die richtige Methode basierend auf der Dateiendung
                    switch (ext)
                    {
                        case ".pdf":
                            content = ExtractTextFromPdf(filePath);
                            lang = "text"; // PDF-Inhalt wird als reiner Text behandelt
                            break;
                        case ".docx":
                            content = ExtractTextFromDocx(filePath);
                            lang = "text"; // DOCX-Inhalt wird als reiner Text behandelt
                            break;
                        case ".jpg":
                        case ".jpeg":
                        case ".png": // PNG hinzugefügt, da Tesseract es auch kann
                            content = ExtractTextFromImage(filePath); // Hier wird die neue Logik angewendet
                            lang = "text"; // OCR-Ergebnis ist reiner Text
                            break;
                        default:
                            if (TextBasedExtensions.Contains(ext))
                            {
                                content = File.ReadAllText(filePath, Encoding.UTF8);
                                lang = ext.Length > 1 ? ext[1..] : "text";
                            }
                            else if (IsTextFile(filePath)) // Fallback für unbekannte, aber textähnliche Dateien
                            {
                                content = File.ReadAllText(filePath, Encoding.UTF8);
                                lang = "text";
                            }
                            break;
                    }

                    // Wenn Inhalt extrahiert wurde, konvertiere ihn
                    if (content != null)
                    {
                        if (!TryWriteMarkdownFile(filePath, content, lang, folderPath))
                        {
                            SkippedFiles.Add(relPath);
                        }
                    }
                    else
                    {
                        // Wenn kein Inhalt extrahiert werden konnte, überspringe die Datei
                        SkippedFiles.Add(relPath);
                    }
                }
                catch (Exception ex)
                {
                    // Fange alle Fehler während der Extraktion oder Konvertierung ab
                    Console.WriteLine($"[Error] Failed to process file {relPath}: {ex.Message}");
                    SkippedFiles.Add(relPath);
                }
            }
        }

        /// <summary>
        /// Schreibt den extrahierten Inhalt in eine Markdown-Datei.
        /// </summary>
        private bool TryWriteMarkdownFile(string originalPath, string content, string lang, string folderRoot)
        {
            try
            {
                var mdContent = $"```{lang}\n{content}\n```";

                string relativePath = Path.GetRelativePath(folderRoot, originalPath);
                var baseOutputDir = Path.Combine(folderRoot, "convertedfiles");
                string relativeDir = Path.GetDirectoryName(relativePath) ?? "";
                var outputDir = Path.Combine(baseOutputDir, relativeDir);

                Directory.CreateDirectory(outputDir);

                var mdFilename = Path.GetFileName(originalPath) + ".md";
                var mdPath = Path.Combine(outputDir, mdFilename);

                File.WriteAllText(mdPath, mdContent, Encoding.UTF8);
                Console.WriteLine($"Created {Path.GetRelativePath(folderRoot, mdPath)}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IO Error] Failed to write markdown for: {originalPath}\n{ex.Message}");
                return false;
            }
        }

        #region Text Extraction Methods

        private string ExtractTextFromPdf(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using (var reader = new PdfReader(filePath))
            {
                using (var pdfDoc = new PdfDocument(reader))
                {
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var strategy = new SimpleTextExtractionStrategy();
                        string currentText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                        text.Append(currentText);
                    }
                }
            }
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                if (wordDoc.MainDocumentPart?.Document.Body != null)
                {
                    foreach (var para in wordDoc.MainDocumentPart.Document.Body.Elements<Paragraph>())
                    {
                        text.AppendLine(para.InnerText);
                    }
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Extrahiert Text aus einem Bild, versucht zuerst Englisch, dann Deutsch, falls kein Text erkannt wird.
        /// Extracts text from an image, tries English first, then German if no text is recognized.
        /// </summary>
        /// <param name="filePath">Der Pfad zur Bilddatei. The path to the image file.</param>
        /// <returns>Der extrahierte Text oder ein leerer String, wenn kein Text erkannt wird. The extracted text or an empty string if no text is recognized.</returns>
        private string ExtractTextFromImage(string filePath)
        {
            string extractedText = string.Empty;

            // Zuerst versuchen wir, Text mit dem englischen Sprachmodell zu extrahieren.
            // First, we try to extract text using the English language model.
            try
            {
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    using (var img = Pix.LoadFromFile(filePath))
                    {
                        using (var page = engine.Process(img))
                        {
                            extractedText = page.GetText();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] OCR with English failed for {filePath}: {ex.Message}");
                // Continue to try German even if English fails due to an error
                extractedText = string.Empty; // Ensure it's empty to trigger German fallback
            }


            // Wenn kein oder nur Leerzeichen-Text erkannt wurde, versuchen wir es mit Deutsch.
            // If no or only whitespace text was recognized, we try with German.
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                Console.WriteLine($"[Info] No text or only whitespace recognized with English for {filePath}. Trying German...");
                try
                {
                    using (var engine = new TesseractEngine(@"./tessdata", "deu", EngineMode.Default))
                    {
                        using (var img = Pix.LoadFromFile(filePath))
                        {
                            using (var page = engine.Process(img))
                            {
                                extractedText = page.GetText();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] OCR with German failed for {filePath}: {ex.Message}");
                    extractedText = string.Empty; // Ensure it's empty if German also fails
                }
            }

            return extractedText;
        }

        #endregion

        /// <summary>
        /// Prüft, ob eine Datei wahrscheinlich eine Textdatei ist, indem nach Null-Bytes gesucht wird.
        /// Checks if a file is likely a text file by looking for null bytes.
        /// </summary>
        private bool IsTextFile(string path, int blockSize = 512)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[blockSize];
                var read = stream.Read(buffer, 0, Math.Min((int)stream.Length, blockSize));

                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] == 0) return false; // Binärdateien enthalten oft Null-Bytes. Binary files often contain null bytes.
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}