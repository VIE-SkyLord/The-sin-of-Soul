using Godot;

public partial class MovingGrassBlock : AnimatableBody2D
{
	[Export] public Vector2 Direction = new Vector2(0, 1); // Hướng di chuyển
	[Export] public float Distance = 1000f; // Khoảng cách (pixel)
	[Export] public float Speed = 100f; // Tốc độ (pixel/giây)

	private Vector2 startPos;
	private Vector2 endPos;
	private bool isMovingToEnd = true;

	public override void _Ready()
	{
		// Bật sync_to_physics để mang theo người chơi
		SyncToPhysics = true;
		startPos = Position;
		endPos = startPos + Direction.Normalized() * Distance;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Xác định mục tiêu và tính toán vận tốc
		Vector2 target = isMovingToEnd ? endPos : startPos;
		Vector2 toTarget = (target - Position).Normalized();
		Vector2 velocity = toTarget * Speed;

		// Cập nhật vị trí trực tiếp
		Position += velocity * (float)delta;

		// Đổi hướng khi gần mục tiêu
		if (Position.DistanceTo(target) < 5f)
		{
			isMovingToEnd = !isMovingToEnd;
		}
	}
}
