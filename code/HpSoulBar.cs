using Godot;
using System;

public partial class HpSoulBar : TextureProgressBar
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
				MaxValue = player.MaxSoulHP; // Đặt giá trị tối đa của ProgressBar
				Value = player.CurSoulHP;    // Đặt giá trị ban đầu
				UpdateHealthBar();           // Cập nhật ngay khi khởi tạo
			}
			else
			{
				GD.PrintErr("Player node not found!");
			}
		}
	}

	public override void _Process(double delta)
	{
		// Cập nhật thanh máu theo thời gian thực khi CurSoulHP thay đổi
		if (player != null)
		{
			UpdateHealthBar();
		}
	}

	private void UpdateHealthBar()
	{
		if (player != null)
		{
			Value = player.CurSoulHP; // Đồng bộ giá trị với CurSoulHP
			// Thêm hiệu ứng khi máu thấp
			if (Value / MaxValue < 0.25f)
			{
				Modulate = new Color(1.0f, 0.0f, 0.0f); // Đổi màu thành đỏ khi máu dưới 30%
			}
			else
			{
				Modulate = new Color(1, 1, 1); // Màu mặc định
			}
		}
	}
}
