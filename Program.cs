using Spectre.Console;
using System;
using System.IO;
using System.Windows.Forms;

namespace FileConverter
{
    internal class Program
    {
        static string FolderPath = "";

        [STAThread]
        static void Main(string[] args)
        {
            var converter = new DataConverter();

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(
                    new FigletText("File → MD")
                        .Centered()
                        .Color(Spectre.Console.Color.Green));

                AnsiConsole.MarkupLine($"[grey]Current working folder: {(string.IsNullOrWhiteSpace(FolderPath) ? "[red]Not set[/]" : $"[blue]{FolderPath}[/]")}[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[yellow]Select an option[/]:")
                        .AddChoices(new[]
                        {
                            "Select Work Folder",
                            "Create Markdown project from folder",
                            "Exit"
                        }));

                if (choice == "Exit")
                    break;

                if (choice == "Select Work Folder")
                {
                    var selectedFolder = BrowseFolderDialog();
                    if (!string.IsNullOrWhiteSpace(selectedFolder))
                    {
                        FolderPath = selectedFolder;
                        AnsiConsole.MarkupLine($"[green]✅ Folder path set to:[/] [blue]{FolderPath}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]No folder selected.[/]");
                    }
                }
                else if (choice == "Create Markdown project from folder")
                {
                    string folderToConvert = FolderPath;

                    if (string.IsNullOrWhiteSpace(folderToConvert))
                    {
                        AnsiConsole.MarkupLine("[red]No working folder selected. Please select one first.[/]");
                        AnsiConsole.MarkupLine("[grey]Press Enter to return to menu...[/]");
                        Console.ReadLine();
                        continue;
                    }

                    if (!Directory.Exists(folderToConvert))
                    {
                        AnsiConsole.MarkupLine($"[red]Folder does not exist:[/] {folderToConvert}");
                        continue;
                    }

                    try
                    {
                        AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .Start("Converting files...", ctx =>
                            {
                                converter.ConvertFolder(folderToConvert);
                            });

                        AnsiConsole.MarkupLine("\n[bold green]Conversion process finished.[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]An error occurred during conversion:[/] {ex.Message}");
                        AnsiConsole.WriteException(ex);
                        continue;
                    }

                    if (converter.SkippedFiles.Count > 0)
                    {
                        AnsiConsole.MarkupLine("\n[bold yellow]⚠️ The following files were skipped (unsupported format or error):[/]");
                        var table = new Table().Border(TableBorder.Rounded);
                        table.AddColumn("Skipped Files");

                        foreach (var file in converter.SkippedFiles)
                        {
                            table.AddRow(file);
                        }
                        AnsiConsole.Write(table);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("\n[green]✅ All supported files converted successfully.[/]");
                    }
                }

                AnsiConsole.MarkupLine("\n[grey]Press Enter to return to menu...[/]");
                Console.ReadLine();
            }
        }

        private static string? BrowseFolderDialog()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the working folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}
