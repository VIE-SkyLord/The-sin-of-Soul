using Godot;
using System;

public partial class SpSoulBar : TextureProgressBar
{
	[Export] public NodePath PlayerPath; // Tham chiếu đến node Player
	private Player player; // Biến lưu tham chiếu đến Player
	
	public override void _Ready()
	{
		// Lấy tham chiếu đến node Player
		if (PlayerPath != null)
		{
			player = GetNode<Player>(PlayerPath);
			if (player != null)
			{
				MaxValue = player.MaxSoulSP; // Đặt giá trị tối đa của ProgressBar
				Value = player.CurSoulSP;    // Đặt giá trị ban đầu
				UpdateSoulBar();           // Cập nhật ngay khi khởi tạo
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Cập nhật thanh máu theo thời gian thực khi CurSoulSP thay đổi
		if (player != null)
		{
			UpdateSoulBar();
		}
	}

	private void UpdateSoulBar()
	{
		if (player != null)
		{
			Value = player.CurSoulSP; // Đồng bộ giá trị với CurSoulSP
		}
	}
}
