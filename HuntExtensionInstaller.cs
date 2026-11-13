using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LoLIdleDesktop;

// Baixa, valida e extrai a extensão "Hunt for LoL Idle" (Manifest V3) da Chrome Web Store.
// A extensão NÃO é distribuída junto com o app: o download acontece só quando o usuário
// clica em "🏹 Hunt" pela primeira vez e confirma. A pasta alvo fica em
// %LocalAppData%\LoLIdleDesktop\Extensions\HuntForLoLIdle.
//
// Estrutura do .crx3: magic "Cr24" + versão + header + payload ZIP. O header (que carrega
// chaves e assinaturas) é ignorado com segurança — o ZIP por si só protege a integridade
// dos arquivos via CRC. Basta localizar o primeiro "PK\x03\x04" no começo do pacote.
internal static class HuntExtensionInstaller
{
    public const string ExtensionFolderName = "HuntForLoLIdle";
    public const string DownloadUrl =
        "https://clients2.google.com/service/update2/crx?response=redirect&prodversion=120.0.0.0&acceptformat=crx3&x=id%3Dcpgahnaagiaikbpnjoncgomjjoimfdii%26uc";
    public const string UpdateCheckUrl =
        "https://clients2.google.com/service/update2/crx?response=updatecheck&prodversion=120.0.0.0&acceptformat=crx3&x=id%3Dcpgahnaagiaikbpnjoncgomjjoimfdii%26uc";
    public const string ExpectedName = "Hunt for LoL Idle";
    public const string PanelPage = "painel.html";

    public static string ExtensionsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoLIdleDesktop", "Extensions");

    public static string TargetFolder => Path.Combine(ExtensionsRoot, ExtensionFolderName);

    // Verdadeiro só se a pasta alvo existir e o manifest.json for o esperado (MV3 + nome certo).
    // De quebra, sanitiza o manifest de instalações já existentes (remove "notifications"):
    // quem instalou a versão antiga também fica mudo nas próximas sessões.
    public static bool IsExtensionExtracted()
    {
        var result = VerifyExtracted();
        if (result.ok)
            SanitizeManifest(TargetFolder);
        return result.ok;
    }

    public static (bool ok, string? error) VerifyExtracted()
    {
        string manifestPath = Path.Combine(TargetFolder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            // Alguns .crx vêm embrulhados numa única pasta-raiz: achata antes de reprovar.
            TryFlattenSingleRootFolder();
            manifestPath = Path.Combine(TargetFolder, "manifest.json");
        }

        if (!File.Exists(manifestPath))
            return (false, "O pacote da extensão não contém manifest.json na raiz.");

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;

            int mv = root.TryGetProperty("manifest_version", out var mvProp) &&
                     mvProp.TryGetInt32(out var v) ? v : 0;
            string name = root.TryGetProperty("name", out var nProp)
                ? (nProp.GetString() ?? "").Trim()
                : "";

            if (mv != 3)
                return (false, $"manifest_version inesperado ({(mv == 0 ? "ausente" : mv)}). Esperado 3.");
            if (!string.Equals(name, ExpectedName, StringComparison.OrdinalIgnoreCase))
                return (false, $"A extensão baixada não é '{ExpectedName}' (nome no manifest: '{name}').");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, "manifest.json inválido: " + ex.Message);
        }
    }

    // Lê o atributo "version" do manifest.json da cópia local instalada. Null se ausente/inválido.
    public static string? ReadLocalVersion()
    {
        try
        {
            string manifestPath = Path.Combine(TargetFolder, "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch (Exception ex)
        {
            try { MainWindow.LogFloatingWindowEvent($"Hunt: falha ao ler versão local — {ex.GetType().Name}: {ex.Message}"); } catch { }
            return null;
        }
    }

    // Consulta o endpoint público de update-check da Chrome Web Store (anônimo, sem auth).
    // Devolve a versão publicada na loja, ou null se a checagem falhar por qualquer motivo
    // (timeout, XML inválido, rede caída) — quem chama decide o que fazer com o null.
    public static async Task<string?> CheckStoreVersionAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LoLIdleDesktop");
            string xml = await http.GetStringAsync(UpdateCheckUrl).ConfigureAwait(false);

            var doc = XDocument.Parse(xml);
            // O namespace do elo principal (gupdate) pode variar; casa por LocalName.
            var updatecheck = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "updatecheck");
            string? version = updatecheck?.Attribute("version")?.Value;
            if (string.IsNullOrWhiteSpace(version))
            {
                MainWindow.LogFloatingWindowEvent("Hunt: update-check sem atributo version — ignorando.");
                return null;
            }
            return version;
        }
        catch (Exception ex)
        {
            try { MainWindow.LogFloatingWindowEvent($"Hunt: falha ao checar atualização — {ex.GetType().Name}: {ex.Message}"); } catch { }
            return null;
        }
    }

    // Remove a permissão "notifications" do manifest.json da cópia LOCAL instalada.
    // Sem ela, chrome.notifications não existe no service worker e o fundo.js
    // (que faz `if (!chrome.notifications?.create) return;`) fica mudo — em todos
    // os perfis, para sempre. Só mexe na entrada "notifications"; preserva as demais
    // permissões e todo o resto do manifest. Idempotente. Em qualquer erro, loga e
    // prossegue com o manifest intacto (menos ruim que falhar a instalação).
    // Obs.: numa instalação ativa (browser process já rodando), a mudança só vale
    // após reabrir o app — o próximo boot re-registra a extensão a partir da pasta.
    private static void SanitizeManifest(string pasta)
    {
        try
        {
            string manifestPath = Path.Combine(pasta, "manifest.json");
            if (!File.Exists(manifestPath))
                return;

            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            if (JsonNode.Parse(json) is not JsonObject root)
                return;
            if (root["permissions"] is not JsonArray permissions)
                return;

            bool removed = false;
            for (int i = permissions.Count - 1; i >= 0; i--)
            {
                if (permissions[i] is JsonValue value &&
                    value.TryGetValue(out string? permission) &&
                    string.Equals(permission, "notifications", StringComparison.OrdinalIgnoreCase))
                {
                    permissions.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
                return; // não estava lá: nada muda

            string rewritten = root.ToJsonString(); // JSON minificado
            File.WriteAllText(manifestPath, rewritten, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            MainWindow.LogFloatingWindowEvent(
                "HuntInstall: permissão notifications removida do manifest (avisos da extensão desativados para este perfil local).");
        }
        catch (Exception ex)
        {
            try
            {
                MainWindow.LogFloatingWindowEvent(
                    $"HuntInstall: não consegui sanitizar o manifest ({ex.GetType().Name}: {ex.Message}); manifest intacto, seguindo mesmo assim.");
            }
            catch { /* log nunca derruba o fluxo */ }
        }
    }

    // Baixa o .crx, localiza o ZIP a partir do payload e extrai para a pasta alvo.
    // Em qualquer falha, a pasta alvo fica limpa (ou como estava, se já estava OK).
    public static async Task<(bool ok, string? error)> DownloadAndExtractAsync()
    {
        byte[] crx;
        try
        {
            Directory.CreateDirectory(ExtensionsRoot);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("LoLIdleDesktop-HuntInstaller");
            crx = await http.GetByteArrayAsync(DownloadUrl).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return (false, "A conexão demorou demais (tempo limite de 60s). Verifica a internet.");
        }
        catch (HttpRequestException ex)
        {
            return (false, "Não consegui baixar da Chrome Web Store: " + ex.Message);
        }
        catch (Exception ex)
        {
            return (false, "Falha ao baixar a extensão: " + ex.Message);
        }

        int offset = FindZipOffset(crx);
        if (offset < 0)
            return (false, "Não achei o conteúdo ZIP dentro do pacote .crx baixado.");

        var delete = SafeDeleteTargetFolder();
        if (!delete.ok)
            return delete;

        try
        {
            Directory.CreateDirectory(TargetFolder);
            string targetFull = Path.GetFullPath(TargetFolder);
            string targetWithSep = targetFull + Path.DirectorySeparatorChar;

            using var ms = new MemoryStream(crx, offset, crx.Length - offset, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            foreach (var entry in zip.Entries)
            {
                string destPath = Path.GetFullPath(Path.Combine(targetFull, entry.FullName));
                // Proteção contra zip-slip: nada pode sair da pasta alvo.
                if (!destPath.StartsWith(targetWithSep, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destPath, targetFull, StringComparison.OrdinalIgnoreCase))
                    return (false, "O pacote contém um caminho de arquivo inválido/inseguro.");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            SafeDeleteTargetFolder(); // não deixa pasta parcial para trás
            return (false, "Erro ao extrair a extensão: " + ex.Message);
        }

        var verify = VerifyExtracted();
        if (!verify.ok)
        {
            SafeDeleteTargetFolder(); // pacote íntegro mas não é o que esperamos: não insiste
            return verify;
        }

        // Extraído e válido: remove a permissão "notifications" do manifest local
        // ANTES de qualquer perfil registrar a extensão.
        SanitizeManifest(TargetFolder);

        return (true, null);
    }

    // Localiza o primeiro "PK\x03\x04" (assinatura de local file header do ZIP) nos
    // primeiros 8192 bytes — depois desse ponto o header do .crx acabou e começa o ZIP.
    private static int FindZipOffset(byte[] data)
    {
        int max = Math.Min(8192, data.Length - 4);
        for (int i = 0; i < max; i++)
        {
            if (data[i] == 0x50 && data[i + 1] == 0x4B && data[i + 2] == 0x03 && data[i + 3] == 0x04)
                return i;
        }
        return -1;
    }

    // Se o .crx extrair tudo dentro de uma única pasta raiz, sobe o conteúdo um nível.
    private static void TryFlattenSingleRootFolder()
    {
        try
        {
            var dirs = Directory.GetDirectories(TargetFolder);
            if (dirs.Length != 1)
                return;
            string inner = dirs[0];
            if (!File.Exists(Path.Combine(inner, "manifest.json")))
                return;

            foreach (string file in Directory.GetFiles(inner, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(inner, file);
                string dest = Path.GetFullPath(Path.Combine(TargetFolder, rel));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(file, dest, overwrite: true);
            }
            Directory.Delete(inner, recursive: true);
        }
        catch
        {
            // Silencioso de propósito: a verificação seguinte reporta o erro com detalhes.
        }
    }

    // Apaga SÓ a pasta alvo da extensão, e só se ela estiver dentro da raiz de extensões.
    private static (bool ok, string? error) SafeDeleteTargetFolder()
    {
        try
        {
            if (!Directory.Exists(TargetFolder))
                return (true, null);

            string rootFull = Path.GetFullPath(ExtensionsRoot);
            string targetFull = Path.GetFullPath(TargetFolder);
            if (!targetFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return (false, "Pasta de destino fora da raiz de extensões — abortando por segurança.");

            Directory.Delete(targetFull, recursive: true);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, "Não consegui limpar a pasta da extensão: " + ex.Message);
        }
    }
}
