using Godot;
using System;

public partial class HpSoulBar : TextureProgressBar
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
				UpdateHealthBar(); // Update immediately on initialization
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Update health bar in real-time
		if (player != null)
		{
			UpdateHealthBar();
		}
	}

	private void UpdateHealthBar()
	{
		if (player != null)
		{
			// FIXED: Get current health and max health based on possession state
			int currentHP = player.GetCurrentHP();
			int maxHP = player.GetCurrentMaxHP();
			
			MaxValue = maxHP;
			Value = currentHP;
			
			// Add visual effect when health is low
			if (Value / MaxValue < 0.25f)
			{
				Modulate = new Color(1.0f, 0.0f, 0.0f); // Red when health below 25%
			}
			else
			{
				Modulate = new Color(1, 1, 1); // Default color
			}
		}
	}
}
