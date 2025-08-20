using Godot;
using System;

public partial class MpSoulBar : TextureProgressBar
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
				MaxValue = player.MaxSoulMP; // Đặt giá trị tối đa của ProgressBar
				Value = player.CurSoulMP;    // Đặt giá trị ban đầu
				UpdateManaBar();           // Cập nhật ngay khi khởi tạo
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Cập nhật thanh máu theo thời gian thực khi CurSoulMP thay đổi
		if (player != null)
		{
			UpdateManaBar();
		}
	}

	private void UpdateManaBar()
	{
		if (player != null)
		{
			Value = player.CurSoulMP; // Đồng bộ giá trị với CurSoulMP
		}
	}
}
