using Godot;

public partial class MovingPlatform : AnimatableBody2D
{
	[Export] public float DetectionRange = 1500.0f; // Khoảng cách phát hiện người chơi
	[Export] public float MoveDistance = 260.0f; // Khoảng cách di chuyển lên trên
	[Export] public float MoveSpeed = 100.0f; // Tốc độ di chuyển
	[Export] public NodePath PlayerPath; // Đường dẫn đến node người chơi
	
	private Node2D player;
	private Vector2 originalPosition;
	private Vector2 targetPosition;
	private bool isMoving = false;
	private bool hasActivated = false; // Để tránh kích hoạt nhiều lần
	private bool isAtTarget = false;
	
	public override void _Ready()
	{
		// Lưu vị trí ban đầu
		originalPosition = GlobalPosition;
		targetPosition = originalPosition + new Vector2(0, -MoveDistance);
		
		// Tìm người chơi
		if (PlayerPath != null)
		{
			player = GetNode<Node2D>(PlayerPath);
		}
		else
		{
			// Tự động tìm node có tên "Player"
			player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
			if (player == null)
			{
				player = GetNode<Node2D>("../Player"); // Thử tìm trong cùng parent
			}
		}
		
		if (player == null)
		{
			GD.PrintErr("Không tìm thấy người chơi! Hãy đặt người chơi vào group 'player' hoặc thiết lập PlayerPath.");
		}
	}
	
	public override void _PhysicsProcess(double delta)
	{
		if (player == null) return;
		
		float distanceToPlayer = GlobalPosition.DistanceTo(player.GlobalPosition);
		
		// Kiểm tra nếu người chơi đến gần và chưa kích hoạt
		if (distanceToPlayer <= DetectionRange && !hasActivated && !isAtTarget)
		{
			hasActivated = true;
			isMoving = true;
		}
		
		// Di chuyển platform nếu đang trong trạng thái di chuyển
		if (isMoving)
		{
			MovePlatform(delta);
		}
	}
	
	private void MovePlatform(double delta)
	{
		Vector2 direction = (targetPosition - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(targetPosition);
		
		if (distance > 5.0f) // Còn khoảng cách để di chuyển
		{
			Vector2 movement = direction * MoveSpeed * (float)delta;
			GlobalPosition += movement;
		}
		else
		{
			// Đã đến đích
			GlobalPosition = targetPosition;
			isMoving = false;
			isAtTarget = true;
		}
	}
	
	// Phương thức để reset platform về vị trí ban đầu (có thể gọi từ script khác)
	public void ResetPosition()
	{
		GlobalPosition = originalPosition;
		hasActivated = false;
		isMoving = false;
		isAtTarget = false;
	}
	
	// Phương thức để kích hoạt thủ công
	public void ActivatePlatform()
	{
		if (!hasActivated && !isAtTarget)
		{
			hasActivated = true;
			isMoving = true;
		}
	}
	
	// Vẽ vùng phát hiện khi đang debug
	public override void _Draw()
	{
		if (Engine.IsEditorHint()) return;
		
		// Vẽ vòng tròn phát hiện (chỉ trong debug mode)
		if (OS.IsDebugBuild())
		{
			DrawCircle(Vector2.Zero, DetectionRange, Colors.Yellow * 0.3f);
			DrawArc(Vector2.Zero, DetectionRange, 0, Mathf.Tau, 32, Colors.Yellow, 2.0f);
		}
	}
}
