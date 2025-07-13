using Godot;

public partial class CameraFollow : Camera2D
{
	private Player _player;

	public override void _Ready()
	{
		_player = GetNode<Player>("/root/Game/Player"); // Adjust path to your scene

		if (_player == null)
		{
			GD.PrintErr("Player not found! Kiểm tra lại tên node hoặc đường dẫn trong scene.");
		}
	}

	public override void _Process(double delta)
	{
		if (_player != null && _player._currentBody != null)
		{
			Position = _player._currentBody.GlobalPosition;
		}
	}
}
