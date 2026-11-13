// Scripts/Vehicle/CabinView.cs
// Primeira pessoa nos carros (v0.10): quando o jogador local está sentado e em primeira pessoa, mostra o
// interior simples (painel, volante, forro) e esconde os vidros opacos da "estufa" do CarModels, que por
// dentro tapavam a visão. De fora (ou em terceira pessoa) tudo volta ao normal.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CabinView : Node3D
{
    private VanController _van;
    private Node3D _cabin;
    private readonly List<Node3D> _glass = new();
    private bool _inside;

    public static CabinView Attach(VanController van, Node3D cabin)
    {
        var view = new CabinView { Name = "CabinView", _van = van, _cabin = cabin };
        van.AddChild(view);
        return view;
    }

    public override void _Ready()
    {
        foreach (Node n in _van.FindChildren(CarModels.GlassName + "*", "MeshInstance3D", true, false))
            _glass.Add((Node3D)n);
        _cabin.Visible = false;
    }

    public override void _Process(double delta)
    {
        PlayerController p = PlayerController.Local;
        bool inside = p != null && p.CurrentVehicle == _van && !p.IsThirdPerson && p.State is PlayerState.Driving or PlayerState.Riding;
        if (inside == _inside)
            return;
        _inside = inside;
        _cabin.Visible = inside;
        foreach (Node3D g in _glass)
            if (IsInstanceValid(g))
                g.Visible = !inside;
    }
}
