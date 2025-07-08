using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace ObsidianGitMirror
{
    public class DataConverter
    {
        public bool IsExcel(string ext) => ext == ".xlsx";
        public bool IsWord(string ext) => ext == ".docx";

        /// <summary>
        /// Für alle "normalen" Dateien – gibt ein einzelnes Markdown-Dokument zurück.
        /// </summary>
        public (string markdown, string language) ConvertToMarkdown(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Datei nicht gefunden", filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var lang = ext.Length > 1 ? ext[1..] : "text";

            // Spezialfall .md
            if (ext == ".md")
            {
                var content = File.ReadAllText(filePath, Encoding.UTF8);
                return (content, "markdown");
            }

            // .docx
            if (ext == ".docx")
            {
                var content = ExtractTextFromDocx(filePath);
                return (content, "text");
            }

            // Textbasierte Formate
            string contentText = File.ReadAllText(filePath, Encoding.UTF8);
            contentText = contentText.Replace("\t", "    ");
            var md = $"```{lang}\n{contentText}\n```";
            return (md, lang);
        }

        /// <summary>
        /// Für Excel-Dateien – gibt eine Liste von Markdown-Dateien pro Tabellenzeile zurück.
        /// </summary>
        public List<(string relativePath, string markdown)> ConvertExcelToMarkdown(string filePath)
        {
            var result = new List<(string relativePath, string markdown)>();
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            using var workbook = new XLWorkbook(filePath);

            foreach (var sheet in workbook.Worksheets)
            {
                // Fall 1: Formatierte Tabellen im Sheet
                foreach (var table in sheet.Tables)
                {
                    var headers = table.Fields.Select(f => f.Name).ToList();
                    var dataRows = table.DataRange.Rows();

                    int index = 1;
                    foreach (var row in dataRows)
                    {
                        var values = row.Cells().Select(c => c.GetValue<string>()).ToList();
                        var frontmatter = new StringBuilder();
                        frontmatter.AppendLine("---");

                        for (int i = 0; i < headers.Count && i < values.Count; i++)
                        {
                            string key = headers[i];
                            string value = values[i];
                            frontmatter.AppendLine($"{key}: {EscapeYaml(value)}");
                        }

                        // Tabellenname als Tag
                        frontmatter.AppendLine($"tags: [\"{EscapeYaml(table.Name)}\"]");
                        frontmatter.AppendLine("---");

                        string markdown = frontmatter.ToString();
                        string relativePath = Path.Combine(fileName, $"{sheet.Name}_{index:000}.md");

                        result.Add((relativePath, markdown));
                        index++;
                    }
                }

                // Fall 2: Sheet hat keine Tabellen → "not_assigned"
                if (!sheet.Tables.Any())
                {
                    var range = sheet.RangeUsed();
                    if (range == null)
                        continue;

                    var headers = range.Row(1).Cells().Select(c => c.GetValue<string>()).ToList();
                    var dataRows = range.RowsUsed().Skip(1);

                    int index = 1;
                    foreach (var row in dataRows)
                    {
                        var values = row.Cells().Select(c => c.GetValue<string>()).ToList();
                        var frontmatter = new StringBuilder();
                        frontmatter.AppendLine("---");

                        for (int i = 0; i < headers.Count && i < values.Count; i++)
                        {
                            string key = headers[i];
                            string value = values[i];
                            frontmatter.AppendLine($"{key}: {EscapeYaml(value)}");
                        }

                        frontmatter.AppendLine("tags: [\"not_assigned\"]");
                        frontmatter.AppendLine("---");

                        string markdown = frontmatter.ToString();
                        string relativePath = Path.Combine(fileName, $"{sheet.Name}_{index:000}.md");

                        result.Add((relativePath, markdown));
                        index++;
                    }
                }
            }

            return result;
        }


        private string EscapeYaml(string input)
        {
            return input.Replace(":", "\\:").Replace("\"", "\\\"");
        }

        private string ExtractTextFromDocx(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false);
            if (wordDoc.MainDocumentPart?.Document.Body != null)
            {
                foreach (var para in wordDoc.MainDocumentPart.Document.Body.Elements<Paragraph>())
                {
                    text.AppendLine(para.InnerText);
                }
            }
            return text.ToString();
        }
    }
}
