using Godot;
using System;

public partial class SpSoulBar : TextureProgressBar
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
				MaxValue = player.MaxSoulSP; // Set maximum value of ProgressBar
				Value = player.CurSoulSP;    // Set initial value
				UpdateSoulBar();           // Update immediately on initialization
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Update SP bar in real-time when CurSoulSP changes
		if (player != null)
		{
			UpdateSoulBar();
		}
	}

	private void UpdateSoulBar()
	{
		if (player != null)
		{
			// SP always uses player's original values, not possessed enemy
			MaxValue = player.MaxSoulSP;
			Value = player.CurSoulSP; // Sync value with CurSoulSP
		}
	}
}
