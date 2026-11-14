using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace LoLIdleDesktop
{
    // Fachada fina sobre o UpdateManager do Velopack. Existe para deixar o MainWindow
    // livre de detalhes de fonte/stack e para concentrar o único ponto de criação do
    // UpdateManager (o mesmo manager é reutilizado entre checagem, download e apply).
    //
    // Fonte: GithubSource apontando para o repositório público (sem token).
    // NotInstalledException (de qualquer método daqui) sinaliza "isto não é uma
    // instalação Velopack" (dev via bin/, ou portable) — quem chama cai no fallback
    // HTTP do GitHubReleaseChecker. Isso é esperado, não é erro.
    internal sealed class VelopackUpdateService
    {
        // Hook de TESTE LOCAL (dev): se LOLIDLE_UPDATE_SOURCE estiver definida e não vazia,
    // o UpdateManager usa esse feed (ex.: um diretório local com os pacotes Velopack)
    // em vez do GitHub. Serve para exercitar checagem/download/delta/apply sem publicar
    // nada. Sem a variável, o caminho é o GithubSource do repositório público, como sempre.
    // Falhas no caminho local sobem para o fluxo existente (captura + log), nunca crash.
        private static string? LocalSourceOverride =>
            Environment.GetEnvironmentVariable("LOLIDLE_UPDATE_SOURCE");

        private UpdateManager? _manager;
        private UpdateManager Manager => _manager ??= CreateManager();

        private static UpdateManager CreateManager()
        {
            string? local = LocalSourceOverride;
            return string.IsNullOrWhiteSpace(local)
                ? new UpdateManager(new GithubSource(RepoConstants.GitHubRepoUrl, null, false))
                : new UpdateManager(local);
        }

        // Devolve null quando não há update (instalação Velopack saudável e atualizada).
        // Lança NotInstalledException fora de instalação Velopack; demais erros sobem
        // para quem chama decidir (o MainWindow loga e marca a checagem como falha).
        public Task<UpdateInfo?> CheckAsync() => Manager.CheckForUpdatesAsync();

        public Task DownloadAsync(UpdateInfo update, Action<int> progress) =>
            Manager.DownloadUpdatesAsync(update, progress);

        // Aplica o update baixado e reinicia o app (encerra este processo).
        // Chamar só depois de DownloadAsync concluir.
        public void ApplyAndRestart(UpdateInfo update) =>
            Manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }
}
