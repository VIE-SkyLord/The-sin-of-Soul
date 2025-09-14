using Godot;
using System;

public partial class MpSoulBar : TextureProgressBar
{
	[Export] public NodePath PlayerPath; // Reference to Player node
	private Player player; // Player reference
	
	public override void _Ready()
	{
		// Get reference to Player node
		if (PlayerPath != null)
		{
			player = GetNode<Player>(PlayerPath);
			if (player != null)
			{
				MaxValue = player.MaxSoulMP; // Set maximum value of ProgressBar
				Value = player.CurSoulMP;    // Set initial value
				UpdateManaBar();           // Update immediately on initialization
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Update mana bar in real-time when CurSoulMP changes
		if (player != null)
		{
			UpdateManaBar();
		}
	}

	private void UpdateManaBar()
	{
		if (player != null)
		{
			// MP always uses player's original values, not possessed enemy
			MaxValue = player.MaxSoulMP;
			Value = player.CurSoulMP; // Sync value with CurSoulMP
		}
	}
}
