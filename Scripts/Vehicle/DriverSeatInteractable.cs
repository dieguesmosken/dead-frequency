// Scripts/Vehicle/DriverSeatInteractable.cs
// Porta do veículo. Mirar e apertar E senta o jogador no banco.
// v0.9.6: porta do carona (Passenger) e, se outro jogador já está dirigindo, a porta do motorista vira carona.
using Godot;

namespace DeadFrequency;

public partial class DriverSeatInteractable : Area3D, IInteractable
{
    [Export] public VanController Van { get; set; }
    [Export] public Node3D SeatMarker { get; set; }
    [Export] public Node3D ExitMarker { get; set; }
    /// <summary>Porta do carona: entra no banco do passageiro (não dirige).</summary>
    [Export] public bool Passenger { get; set; }

    public override void _Ready()
    {
        if (Van == null || SeatMarker == null || ExitMarker == null)
            GD.PushWarning($"{Name}: preencha Van, SeatMarker e ExitMarker no Inspector.");
    }

    private string VehicleWord => Van?.Kind == "moto" ? "a moto" : Van?.Kind == "truck" ? "o caminhão" : Van?.Kind == "car" ? CarWord : "a van";

    /// <summary>"o táxi", "o Opalinha", "a van de viagem".</summary>
    private string CarWord => (Van.DisplayName.StartsWith("van") ? "a " : "o ") + Van.DisplayName;

    /// <summary>Entra como carona: porta do carona, ou motorista ocupado por outro jogador.</summary>
    private bool AsPassenger => Passenger || (Van != null && Van.HasRemoteDriver);

    public string GetInteractionPrompt(PlayerController player)
    {
        if (player.State != PlayerState.Walking)
            return "";
        if (AsPassenger && Van?.PassengerSeat == null)
            return $"Outro jogador está usando {VehicleWord}";
        if (player.IsHolding)
            return Van?.Kind == "moto" ? "Guarde a caixa no baú da moto antes de subir" : "Solte a caixa antes de entrar";
        if (AsPassenger)
            return Van?.Kind == "moto" ? "[E] Subir na garupa" : "[E] Pegar carona";
        return Van?.Kind switch
        {
            "moto" => "[E] Pilotar a moto de entrega",
            "truck" => "[E] Pegar o caminhão emprestado",
            "car" => "[E] Dirigir " + CarWord,
            _ => "[E] Dirigir a van",
        };
    }

    public bool CanInteract(PlayerController player) =>
        player.State == PlayerState.Walking && !player.IsHolding && Van != null && SeatMarker != null
        && (!AsPassenger || Van.PassengerSeat != null);

    public void Interact(PlayerController player)
    {
        if (AsPassenger)
            player.EnterVehicle(Van, Van.PassengerSeat, Van.PassengerExit ?? ExitMarker, passenger: true);
        else
            player.EnterVehicle(Van, SeatMarker, ExitMarker);
    }
}
