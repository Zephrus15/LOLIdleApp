using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LoLIdleDesktop
{
    // Resultado de uma consulta à API pública do GitHub Releases.
    internal sealed class GitHubReleaseInfo
    {
        public required string TagName { get; init; }
        public string? Name { get; init; }
        public required string HtmlUrl { get; init; }
        public string? PublishedAt { get; init; }
    }

    // Consulta "última release publicada" do repositório público sem token.
    // Qualquer falha (rede, HTTP, JSON, parse de versão) devolve null —
    // quem chama decide não exibir nada como "nova versão".
    internal static class GitHubReleaseChecker
    {
        private const string LatestReleaseUrl = RepoConstants.LatestReleaseApiUrl;

        // major.minor.patch opcionalmente prefixado por "v" (ex.: "v1.4.7" ou "1.4.7")
        private static readonly Regex VersionRegex =
            new(@"^v?(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            // A API do GitHub exige User-Agent; sem ele responde 403.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoLIdleDesktop-UpdateChecker");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        // Extrai major.minor.patch do texto; null se não bater o formato esperado.
        public static Version? TryParseVersion(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            Match match = VersionRegex.Match(text.Trim());
            if (!match.Success)
                return null;

            if (!int.TryParse(match.Groups[1].Value, out int major)) return null;
            if (!int.TryParse(match.Groups[2].Value, out int minor)) return null;
            if (!int.TryParse(match.Groups[3].Value, out int patch)) return null;

            return new Version(major, minor, patch);
        }

        // Compara duas versões extraídas pela regex (ordinal via System.Version.CompareTo:
        // Major, depois Minor, depois Build). null se qualquer uma não parsear.
        public static int? CompareVersions(string? a, string? b)
        {
            Version? va = TryParseVersion(a);
            Version? vb = TryParseVersion(b);
            if (va == null || vb == null)
                return null;
            return va.CompareTo(vb);
        }

        public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync()
        {
            try
            {
                using HttpResponseMessage response =
                    await Http.GetAsync(LatestReleaseUrl).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                string? tag = root.TryGetProperty("tag_name", out JsonElement tagElement)
                    ? tagElement.GetString()
                    : null;
                string? name = root.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString()
                    : null;
                string? url = root.TryGetProperty("html_url", out JsonElement urlElement)
                    ? urlElement.GetString()
                    : null;
                string? publishedAt = root.TryGetProperty("published_at", out JsonElement publishedElement)
                    ? publishedElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(url))
                    return null;

                Version? version = TryParseVersion(tag);
                if (version == null)
                    return null; // tag fora do padrão: nada é tratado como "mais novo"

                return new GitHubReleaseInfo
                {
                    TagName = tag.Trim(),
                    Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                    HtmlUrl = url.Trim(),
                    PublishedAt = string.IsNullOrWhiteSpace(publishedAt) ? null : publishedAt.Trim()
                };
            }
            catch
            {
                // Silencioso por design: a checagem nunca derruba o app.
                return null;
            }
        }
    }
}
