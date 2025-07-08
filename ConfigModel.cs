namespace ObsidianGitMirror
{
    internal class ConfigModel
    {
        public string RepositoryPath { get; set; } = "";
        public string VaultOutputPath { get; set; } = "";
        public List<string> AcceptedExtensions { get; set; } = new();
    }
}
