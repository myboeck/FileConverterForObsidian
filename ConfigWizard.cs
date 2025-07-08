using System.Text.Json;
using Spectre.Console;
using Application = System.Windows.Forms.Application;
using Color = Spectre.Console.Color;

namespace ObsidianGitMirror
{
    internal class ConfigWizard
    {
        public static void Run(string configPath)
        {
            ConfigModel config = File.Exists(configPath)
                ? JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(configPath)) ?? new ConfigModel()
                : new ConfigModel();

            Application.EnableVisualStyles(); // Für schöneren Dialog

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText("Vault Setup").Centered().Color(Color.Cyan1));

                AnsiConsole.MarkupLine($"[grey]Repository: [blue]{(string.IsNullOrWhiteSpace(config.RepositoryPath) ? "[red]Not set[/]" : config.RepositoryPath)}[/][/]");
                AnsiConsole.MarkupLine($"[grey]Vault:      [blue]{(string.IsNullOrWhiteSpace(config.VaultOutputPath) ? "[red]Not set[/]" : config.VaultOutputPath)}[/][/]");

                AnsiConsole.MarkupLine($"[grey]Extensions:[/]");

                if (Program.DefaultExtensions is { Count: > 0 })
                {
                    foreach (var ext in Program.DefaultExtensions)
                    {
                        AnsiConsole.MarkupLine($"  • [blue]{ext}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("  [red]No extensions defined[/]");
                }

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("\n[yellow]What would you like to do?[/]")
                        .AddChoices(new[]
                        {
                            "Set Repository Folder",
                            "Set Vault Folder",
                            "Clear Extensions",
                            "Save and Exit",
                            "Cancel"
                        }));

                if (choice == "Cancel")
                    break;

                if (choice == "Set Repository Folder")
                {
                    var selected = BrowseFolderDialog("Select your Git repository folder");
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        config.RepositoryPath = selected;
                        AnsiConsole.MarkupLine($"[green]✔ Repository folder set:[/] [blue]{selected}[/]");
                    }
                }
                else if (choice == "Set Vault Folder")
                {
                    var selected = BrowseFolderDialog("Select your Obsidian Vault folder");
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        config.VaultOutputPath = selected;
                        AnsiConsole.MarkupLine($"[green]✔ Vault folder set:[/] [blue]{selected}[/]");
                    }
                }
                else if (choice == "Save and Exit")
                {
                    // Standard-Extensions einfügen, wenn leer
                    if (config.AcceptedExtensions == null || config.AcceptedExtensions.Count == 0)
                    {
                        config.AcceptedExtensions = Program.DefaultExtensions;
                    }

                    File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                    AnsiConsole.MarkupLine("\n[green]✅ Configuration saved.[/]");
                    break;
                }
                else if (choice == "Clear Extensions")
                {
                    if (config.AcceptedExtensions?.Count > 0)
                    {
                        var confirm = AnsiConsole.Confirm("[yellow]⚠️ Really clear all accepted extensions?[/]", false);
                        if (confirm)
                        {
                            config.AcceptedExtensions.Clear();
                            AnsiConsole.MarkupLine("[green]✅ Extensions list has been cleared.[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[grey]No changes made.[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[grey]No extensions defined yet.[/]");
                    }
                }

                AnsiConsole.MarkupLine("\n[grey]Press Enter to return to menu...[/]");
                Console.ReadLine();
            }
        }

        private static string? BrowseFolderDialog(string title)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}
