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
        private static readonly GithubSource Source =
            new(RepoConstants.GitHubRepoUrl, null, false);

        // Criado sob demanda: instanciar o UpdateManager em dev não quebra nada, mas
        // evita trabalho desnecessário até a primeira checagem.
        private UpdateManager? _manager;
        private UpdateManager Manager => _manager ??= new UpdateManager(Source);

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
