using Godot;

namespace DeadFrequency;

public partial class WorldItem : RigidBody3D, IInteractable
{
    public const string GroupName = "world_items";

    public string WorldId { get; private set; } = "";
    public string ItemId { get; private set; } = "";

    public static WorldItem Create(string worldId, string itemId, Vector3 position, Quaternion rotation)
    {
        ItemDef def = ItemCatalog.Get(itemId);
        if (def == null || def.Car)
            return null;

        var item = new WorldItem
        {
            Name = "WorldItem_" + worldId,
            WorldId = worldId,
            ItemId = itemId,
            Position = position,
            Quaternion = rotation.Normalized(),
            Mass = 1f,
            LinearDamp = 1.5f,
            AngularDamp = 2f,
            CollisionLayer = 4,
            CollisionMask = 1,
        };
        item.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.24f, 0.18f, 0.24f) } });
        item.AddChild(CreateVisual("item:" + itemId));
        item.AddToGroup(GroupName);
        return item;
    }

    public static MeshInstance3D CreateVisual(string heldValue)
    {
        bool cargo = !heldValue.StartsWith("item:");
        string itemId = cargo ? "" : heldValue[5..];
        ItemDef def = cargo ? null : ItemCatalog.Get(itemId);
        Color color = cargo ? new Color(0.58f, 0.37f, 0.19f) : def?.Color ?? Colors.White;
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = cargo ? new Vector3(0.34f, 0.25f, 0.28f) : new Vector3(0.2f, 0.2f, 0.2f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.8f },
            Position = cargo ? Vector3.Zero : new Vector3(0f, -0.02f, 0f),
        };
        return mesh;
    }

    public override void _EnterTree() => AddToGroup(GroupName);

    public string GetInteractionPrompt(PlayerController player)
    {
        ItemDef def = ItemCatalog.Get(ItemId);
        return player.CanPickUpWorldItem(ItemId) ? $"Pegar {def?.Name ?? "item"}" : "Mochila cheia";
    }

    public bool CanInteract(PlayerController player) => player.CanPickUpWorldItem(ItemId);

    public void Interact(PlayerController player) => player.PickUpWorldItem(this);
}
