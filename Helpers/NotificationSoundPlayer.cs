using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace LoLIdleDesktop
{
    // Player dos sons de notificação (volume geral + timbre por família).
    // Os WAVs são Resources WPF ("Assets/notify_*.wav"), mas o MediaPlayer precisa de
    // um caminho/URI de arquivo — por isso os sons são extraídos sob demanda para
    // %LocalAppData%\LoLIdleDesktop\sounds\notify_*.wav. Um marcador .version (versão
    // do assembly) controla a invalidação do cache: bater = pular extração; não bater
    // ou ausente = re-extrai tudo e regrava o marcador.
    //
    // O helper é estático e não conhece o MainWindow: o mapa de timbres salvo
    // (SoundTimbres em WindowSettings) é injetado via propriedade estática
    // SoundTimbres — referência ao mesmo Dictionary do settings (mutação in-place).
    internal static class NotificationSoundPlayer
    {
        // Família/timbre → arquivo embutido (sem extensão). Base do fallback original:
        // sem preferência salva, cada família toca o seu WAV original.
        private static readonly Dictionary<string, string> FileByKey =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["mission"] = "notify_mission",
                ["potion"] = "notify_potion",
                ["legendary"] = "notify_legendary",
                ["epic"] = "notify_epic",
                ["inventory"] = "notify_inventory",
                ["level"] = "notify_level",
            };

        // Mapa família → timbre escolhido. Chave ausente/vazia/inválida = som original.
        // O MainWindow atribui este setter com a referência do Dictionary do settings.
        internal static IReadOnlyDictionary<string, string>? SoundTimbres { get; set; }

        private static readonly object _extractLock = new();
        // MediaPlayer precisa permanecer vivo durante o playback: guarda as instâncias
        // ativas até MediaEnded/MediaFailed/Close para o GC não interromper o som.
        private static readonly List<MediaPlayer> _activePlayers = new();
        private static readonly object _activePlayersLock = new();

        // Toca o som da família usando o timbre salvo em SoundTimbres (fallback: original).
        public static void PlayFamily(string family, int volumePercent, Action<string>? log = null)
        {
            string familyKey = NormalizeFamily(family);
            string soundKey = familyKey;
            try
            {
                if (SoundTimbres != null &&
                    SoundTimbres.TryGetValue(familyKey, out var saved) &&
                    !string.IsNullOrWhiteSpace(saved) &&
                    IsKnownKey(saved))
                {
                    soundKey = saved;
                }
            }
            catch (Exception ex)
            {
                // Dicionário possivelmente sendo mutado em outra thread: cai no original.
                log?.Invoke($"NotificationSoundPlayer: falha lendo SoundTimbres ({familyKey}): {ex.Message} — usando som original");
                soundKey = familyKey;
            }

            PlayKey(soundKey, familyKey, volumePercent, log);
        }

        // Toca o timbre ESCOLHIDO no dropdown (mesmo não salvo) — usado pelo botão "▶ Tocar".
        public static void PreviewFamily(string family, string? soundKey, int volumePercent, Action<string>? log = null)
        {
            string familyKey = NormalizeFamily(family);
            string key = !string.IsNullOrWhiteSpace(soundKey) && IsKnownKey(soundKey)
                ? soundKey!
                : familyKey;
            PlayKey(key, familyKey, volumePercent, log);
        }

        private static string NormalizeFamily(string family) =>
            FileByKey.ContainsKey(family ?? string.Empty) ? family!.ToLowerInvariant() : "mission";

        private static bool IsKnownKey(string key) =>
            string.Equals(key, "silent", StringComparison.OrdinalIgnoreCase) || FileByKey.ContainsKey(key);

        private static void PlayKey(string soundKey, string familyKey, int volumePercent, Action<string>? log)
        {
            int volume = Math.Clamp(volumePercent, 0, 100);
            if (volume <= 0 ||
                string.Equals(soundKey, "silent", StringComparison.OrdinalIgnoreCase))
            {
                // Volume 0 ou timbre Mudo: silêncio intencional (não cai no fallback Asterisk).
                log?.Invoke($"NotificationSoundPlayer: mudo (família={familyKey}, timbre={soundKey}, volume={volume}%)");
                return;
            }

            // MediaPlayer (WPF) é ligado à thread que o criou: garante execução no dispatcher
            // da UI, venha a chamada de onde vier. Falha de dispatch (ex.: shutdown) não propaga.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                try
                {
                    dispatcher.BeginInvoke(new Action(() => PlayKey(soundKey, familyKey, volume, log)));
                }
                catch (Exception ex)
                {
                    log?.Invoke($"NotificationSoundPlayer: falha no dispatch para a UI ({ex.Message}) — som ignorado");
                }
                return;
            }

            MediaPlayer? player = null;
            try
            {
                string file = EnsureExtracted(FileByKey[soundKey]);
                player = new MediaPlayer
                {
                    Volume = volume / 100.0
                };
                player.Open(new Uri(file, UriKind.Absolute));
                player.Play();
            }
            catch (Exception ex)
            {
                // Open/Play falhou: fecha o player se criado — nunca vaza para a track list.
                try { player?.Close(); } catch { }
                log?.Invoke($"NotificationSoundPlayer: falha no som (família={familyKey}, timbre={soundKey}): {ex.Message} — fallback Asterisk");
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                return;
            }

            // Só rastreia DEPOIS de Open/Play bem-sucedidos (remoção no MediaEnded/MediaFailed).
            TrackPlayer(player);
            log?.Invoke($"NotificationSoundPlayer: som tocado (família={familyKey}, timbre={soundKey}, volume={volume}%)");
        }

        // Versão estável do assembly para versionar o cache de extração
        // (muda a cada release — WAVs novos após update não ficam presos no cache).
        private static string GetCacheVersion() =>
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "0.0.0.0";

        // O cache só é válido com o marcador .version igual à versão atual
        // E todos os WAVs esperados presentes com tamanho > 0.
        private static bool IsCacheValid(string dir)
        {
            try
            {
                string marker = Path.Combine(dir, ".version");
                if (!File.Exists(marker))
                    return false;
                if (!string.Equals(File.ReadAllText(marker).Trim(), GetCacheVersion(),
                        StringComparison.Ordinal))
                    return false;
                foreach (var fileName in FileByKey.Values)
                {
                    string path = Path.Combine(dir, fileName + ".wav");
                    if (!File.Exists(path) || new FileInfo(path).Length == 0)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Garante que TODOS os WAVs embutidos estejam extraídos e o marcador .version
        // gravado em %LocalAppData%\LoLIdleDesktop\sounds\
        // (extração atômica por arquivo via tmp + move; o cache inteiro é revalidado
        // pelo marcador — se ele não bater, re-extrai tudo).
        private static string EnsureExtracted(string fileNameNoExt)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LoLIdleDesktop",
                "sounds");
            string target = Path.Combine(dir, fileNameNoExt + ".wav");

            if (IsCacheValid(dir) && File.Exists(target) && new FileInfo(target).Length > 0)
                return target;

            lock (_extractLock)
            {
                // Double-check após adquirir o lock (outra thread pode ter re-extraído).
                if (IsCacheValid(dir) && File.Exists(target) && new FileInfo(target).Length > 0)
                    return target;

                Directory.CreateDirectory(dir);
                foreach (var fileName in FileByKey.Values)
                {
                    var resource = Application.GetResourceStream(
                        new Uri("pack://application:,,,/Assets/" + fileName + ".wav"));
                    if (resource == null)
                        throw new FileNotFoundException($"Recurso embutido não encontrado: Assets/{fileName}.wav");

                    string dest = Path.Combine(dir, fileName + ".wav");
                    string tmp = dest + ".tmp";
                    using (var rs = resource.Stream)
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                        rs.CopyTo(fs);
                    // tmp + move (overwrite): reduz a janela de arquivo pela metade,
                    // mas não é garantia contra crash no meio — o marcador .version
                    // revalida o conjunto inteiro na próxima execução.
                    File.Move(tmp, dest, overwrite: true);
                }

                // Grava o marcador por último: um re-extract interrompido no meio
                // não deixa o cache "válido" pela metade.
                File.WriteAllText(Path.Combine(dir, ".version"), GetCacheVersion());

                return target;
            }
        }

        // Mantém o MediaPlayer vivo até o playback terminar; remove-o da lista no fim.
        private static void TrackPlayer(MediaPlayer player)
        {
            lock (_activePlayersLock)
                _activePlayers.Add(player);

            void Release(object? sender, EventArgs e)
            {
                try { player.Close(); } catch { }
                lock (_activePlayersLock)
                    _activePlayers.Remove(player);
            }

            player.MediaEnded += Release;
            player.MediaFailed += (sender, e) => Release(sender, e);
        }
    }
}
