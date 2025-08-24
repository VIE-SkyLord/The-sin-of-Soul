using Godot;
using System;

public partial class SummonSlot : TextureButton
{
    [Signal] public delegate void SlotSelectedEventHandler(int slotIndex);
    
    [Export] public int SlotIndex = 0;
    [Export] public Texture2D LockedTexture;
    
    private Label nameLabel;
    private Label costLabel;

    public override void _Ready()
    {
        nameLabel = GetNode<Label>("NameLabel");
        costLabel = GetNode<Label>("CostLabel");
        
        Connect("pressed", new Callable(this, nameof(OnPressed)));
    }

    public void SetMonsterInfo(string monsterPath, int cost)
    {
        string monsterName = System.IO.Path.GetFileNameWithoutExtension(monsterPath);
        nameLabel.Text = monsterName;
        costLabel.Text = cost.ToString();
        TextureNormal = null; // Reset texture
        Disabled = false;
    }

    public void SetLocked()
    {
        nameLabel.Text = "Khóa";
        costLabel.Text = "";
        TextureNormal = LockedTexture;
        Disabled = true;
    }

    private void OnPressed()
    {
        EmitSignal("SlotSelected", SlotIndex);
    }
}