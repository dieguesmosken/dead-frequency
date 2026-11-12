using Godot;

public partial class CaveGenerator : Node3D
{
    [Export] public PackedScene EntryRoomScene;
    [Export] public Godot.Collections.Array<PackedScene> MiddleRoomScenes = new();
    [Export] public PackedScene FinalRepeaterRoomScene;
    [Export] public int RoomCount = 10;
    [Export] public ulong Seed = 966;

    public override void _Ready()
    {
        GenerateCave();
    }

    public void GenerateCave()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = Seed;

        if (EntryRoomScene == null || FinalRepeaterRoomScene == null) return;

        Node3D currentRoom = EntryRoomScene.Instantiate<Node3D>();
        AddChild(currentRoom);

        for (int i = 0; i < RoomCount; i++)
        {
            if (MiddleRoomScenes.Count == 0) break;

            var exitMarker = currentRoom.GetNodeOrNull<Marker3D>("Saida");
            if (exitMarker == null) break;

            int idx = rng.RandiRange(0, MiddleRoomScenes.Count - 1);
            var nextRoom = MiddleRoomScenes[idx].Instantiate<Node3D>();
            AddChild(nextRoom);

            AlignRoomToMarker(nextRoom, exitMarker);
            currentRoom = nextRoom;
        }

        var finalExit = currentRoom.GetNodeOrNull<Marker3D>("Saida");
        if (finalExit != null)
        {
            var finalRoom = FinalRepeaterRoomScene.Instantiate<Node3D>();
            AddChild(finalRoom);
            AlignRoomToMarker(finalRoom, finalExit);
        }
    }

    private void AlignRoomToMarker(Node3D room, Marker3D targetExit)
    {
        var entryMarker = room.GetNodeOrNull<Marker3D>("Entrada");
        if (entryMarker == null)
        {
            room.GlobalTransform = targetExit.GlobalTransform;
            return;
        }

        Transform3D offset = entryMarker.Transform.AffineInverse();
        room.GlobalTransform = targetExit.GlobalTransform * offset;
    }
}
