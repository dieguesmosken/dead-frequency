// Scripts/Core/AppUpdater.cs
// Atualizador do celular (v0.10): no Android não existe launcher, então o próprio jogo faz o papel dele.
//   1. Pergunta ao GitHub pelos releases que têm APK (Internal: os vX.Y.Z.N-pre; público: só os vX.Y.Z).
//   2. Se houver build mais novo que o instalado (GameVersion.Build), o menu mostra "Atualizar".
//   3. Baixa o APK para user://update.apk mostrando o progresso e abre o instalador do Android.
//      Se o Android não deixar abrir direto, cai para o link no navegador (baixa e instala na mão).
// Instalar por cima só funciona se o APK novo tiver a MESMA assinatura do instalado: isso depende da chave
// de release do Matheus nos segredos do GitHub (ANDROID_KEYSTORE_*). Com chave de debug, desinstale antes.
using System;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public static class AppUpdater
{
    public const string ApkPath = "user://update.apk";

    /// <summary>Um APK publicado no GitHub.</summary>
    public sealed record ApkRelease(string Tag, string Url, long Size, string Page);

    public static bool Supported => OS.HasFeature("android");

    private static ApkRelease _found;
    private static bool _checked;

    /// <summary>Chama onNewer se houver APK mais novo que o build instalado (uma consulta por execução).</summary>
    public static void Check(Node host, Action<ApkRelease> onNewer)
    {
        if (_checked)
        {
            if (_found != null)
                onNewer(_found);
            return;
        }
        _checked = true;
        var http = new HttpRequest { Timeout = 10 };
        host.AddChild(http);
        http.RequestCompleted += (result, code, headers, body) =>
        {
            http.QueueFree();
            if (result != (long)HttpRequest.Result.Success || code != 200)
                return;
            try
            {
                _found = PickNewer(body);
                if (_found != null)
                    onNewer(_found);
            }
            catch (Exception e)
            {
                GD.PushWarning($"AppUpdater: resposta inesperada do GitHub ({e.Message}).");
            }
        };
        http.Request($"https://api.github.com/repos/{UpdateChecker.Repo}/releases?per_page=15",
            new[] { "User-Agent: DeadFrequency-Game", "Accept: application/vnd.github+json" });
    }

    private static ApkRelease PickNewer(byte[] body)
    {
        bool internalChannel = GameVersion.IsInternalBuild;
        string installed = GameVersion.Build;
        ApkRelease best = null;
        string bestVersion = installed;
        using JsonDocument doc = JsonDocument.Parse(body);
        foreach (JsonElement rel in doc.RootElement.EnumerateArray())
        {
            if (rel.GetProperty("draft").GetBoolean())
                continue;
            if (rel.GetProperty("prerelease").GetBoolean() && !internalChannel)
                continue; // quem tem o build público só recebe build público
            string tag = rel.GetProperty("tag_name").GetString() ?? "";
            string version = tag.TrimStart('v');
            if (GameVersion.CompareBuild(version, bestVersion) <= 0)
                continue;
            foreach (JsonElement asset in rel.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    continue;
                best = new ApkRelease(tag, asset.GetProperty("browser_download_url").GetString(),
                    asset.GetProperty("size").GetInt64(), rel.GetProperty("html_url").GetString());
                bestVersion = version;
                break;
            }
        }
        return best;
    }

    /// <summary>
    /// Baixa o APK (segue o redirecionamento do GitHub). progress(0..1) a cada quadro; done(ok, mensagem) no fim.
    /// Devolve o HttpRequest para o chamador acompanhar/cancelar.
    /// </summary>
    public static HttpRequest Download(Node host, ApkRelease release, Action<float> progress, Action<bool, string> done)
    {
        string path = ProjectSettings.GlobalizePath(ApkPath);
        if (FileAccess.FileExists(ApkPath))
            DirAccess.RemoveAbsolute(path);
        var http = new HttpRequest { DownloadFile = ApkPath, MaxRedirects = 10, Timeout = 0, UseThreads = true };
        host.AddChild(http);
        var timer = new Timer { WaitTime = 0.2, Autostart = true };
        http.AddChild(timer);
        timer.Timeout += () =>
        {
            long total = http.GetBodySize() > 0 ? http.GetBodySize() : release.Size;
            if (total > 0)
                progress(Mathf.Clamp(http.GetDownloadedBytes() / (float)total, 0f, 1f));
        };
        http.RequestCompleted += (result, code, headers, body) =>
        {
            http.QueueFree();
            bool ok = result == (long)HttpRequest.Result.Success && code == 200 && FileAccess.FileExists(ApkPath);
            done(ok, ok ? "Download concluído." : $"Não consegui baixar (erro {result}/{code}). Tente de novo ou use o navegador.");
        };
        Error err = http.Request(release.Url, new[] { "User-Agent: DeadFrequency-Game" });
        if (err != Error.Ok)
        {
            http.QueueFree();
            done(false, $"Não consegui começar o download ({err}).");
        }
        return http;
    }

    /// <summary>Abre o instalador do Android com o APK baixado; se não der, abre o link no navegador.</summary>
    public static void Install(ApkRelease release)
    {
        Error err = OS.ShellOpen(ProjectSettings.GlobalizePath(ApkPath));
        if (err != Error.Ok)
        {
            GD.PushWarning($"AppUpdater: o instalador não abriu ({err}); abrindo o link no navegador.");
            OS.ShellOpen(release.Url);
        }
    }

    /// <summary>Apaga o APK baixado (chamado ao abrir o jogo já atualizado).</summary>
    public static void CleanUp()
    {
        if (FileAccess.FileExists(ApkPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(ApkPath));
    }
}
