// Scripts/UI/SceneLoader.cs
// Troca de cena com tela de carregamento (v0.10.1): em vez de congelar o menu até a cena abrir,
// carrega em segundo plano (ResourceLoader threaded) mostrando a barra no tema CRT verde.
// A tela fica na raiz da árvore, então sobrevive à troca e só some quando a cena nova já está de pé
// (o CityGenerator abre a tela dele, com o mesmo visual, para as etapas do mundo).
using Godot;

namespace DeadFrequency;

public static class SceneLoader
{
    /// <summary>Carrega a cena em segundo plano e troca. header/title opcionais (capítulo do Modo História).</summary>
    public static async void Go(SceneTree tree, string scenePath, string header = null, string title = null)
    {
        if (tree == null)
            return;
        if (!ResourceLoader.Exists(scenePath) || ResourceLoader.LoadThreadedRequest(scenePath) != Error.Ok)
        {
            tree.ChangeSceneToFile(scenePath);
            return;
        }

        // A raiz pode estar montando filhos (chamada vinda de _Ready): espera um quadro.
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var screen = new WorldLoadingScreen { Name = "SceneLoading" };
        tree.Root.AddChild(screen);
        screen.SetHeading(header, title);
        screen.SetProgress("Abrindo a cena...", 0, 100, 0f);

        var progress = new Godot.Collections.Array();
        while (true)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(scenePath, progress);
            if (status == ResourceLoader.ThreadLoadStatus.InProgress)
            {
                float ratio = progress.Count > 0 ? (float)progress[0].AsDouble() : 0f;
                screen.SetProgress("Carregando recursos...", Mathf.RoundToInt(ratio * 100f), 100, ratio * 0.3f);
                continue;
            }
            if (status != ResourceLoader.ThreadLoadStatus.Loaded)
            {
                GD.PushError($"SceneLoader: falhou ao carregar '{scenePath}'.");
                screen.SetFailure("Não deu para abrir a cena.");
                await tree.ToSignal(tree.CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
                screen.QueueFree();
                return;
            }
            break;
        }

        screen.SetProgress("Entrando no mundo...", 100, 100, 0.3f);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var packed = (PackedScene)ResourceLoader.LoadThreadedGet(scenePath);
        tree.ChangeSceneToPacked(packed);

        // Dois quadros: a cena nova entra, roda o _Ready (e abre a tela do CityGenerator) e só então esta some.
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(screen))
            screen.QueueFree();
    }
}
