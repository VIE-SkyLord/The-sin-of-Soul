using Godot;

public partial class CameraFollow : Camera2D
{
	[Export]
	private NodePath _playerPath;

	private Player _player;

	public override void _Ready()
	{
		if (_playerPath != null && !string.IsNullOrEmpty(_playerPath))
		{
			_player = GetNode<Player>(_playerPath);
		}

		if (_player == null)
		{
			GD.PrintErr("Player not found! Kiểm tra lại đường dẫn hoặc gán node trong Inspector.");
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
