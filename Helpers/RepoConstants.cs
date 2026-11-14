namespace LoLIdleDesktop
{
    // Constantes únicas do repositório oficial. Tudo que aponta para o GitHub
    // (checagem de update, Velopack, links da aba Sobre) passa por aqui.
    internal static class RepoConstants
    {
        public const string GitHubRepoUrl = "https://github.com/Zephrus15/LOLIdleApp";
        public const string ReleasesTagBaseUrl = GitHubRepoUrl + "/releases/tag/";
        public const string LatestReleaseApiUrl =
            "https://api.github.com/repos/Zephrus15/LOLIdleApp/releases/latest";
    }
}
