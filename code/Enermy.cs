using Godot;
using System;
using System.IO;

public partial class Enermy : CharacterBody2D
{
	[Export] public float Speed = 500.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float Gravity = 980.0f;
	[Export] public float DetectionRange = 300.0f; // Bán kính phát hiện người chơi
	[Export] public float AttackRange = 50.0f; // Bán kính tấn công
	[Export] public float AttackCooldown = 1.0f; // Thời gian chờ giữa các lần tấn công (giây)
	[Export] public int AttackDamage = 10; // Sát thương mỗi lần tấn công

	private bool isPossessed = false; // Trạng thái bị chiếm hữu
	private float attackCooldownTimer = 0.0f; // Bộ đếm thời gian hồi chiêu tấn công
	private Player player; // Tham chiếu đến người chơi
	private static readonly string LogFilePath = Path.Combine(OS.GetUserDataDir(), "enemy_debug.log");

	public override void _Ready()
	{
		// Tìm node Player trong scene
		player = GetTree().GetFirstNodeInGroup("Player") as Player;
		if (player == null)
		{
			Log("Warning: Player node not found in group 'Player'!");
		}
		else
		{
			Log($"Enemy initialized, found Player at position: {player.Position}");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// Áp dụng trọng lực
		if (!IsOnFloor())
			velocity.Y += Gravity * (float)delta;

		// Cập nhật thời gian hồi chiêu tấn công
		if (attackCooldownTimer > 0)
		{
			attackCooldownTimer -= (float)delta;
		}

		if (isPossessed)
		{
			// Xử lý di chuyển và nhảy khi bị chiếm hữu
			if (Input.IsActionPressed("move_up") && IsOnFloor())
			{
				velocity.Y = JumpVelocity;
				GD.Print("Enemy jump input detected");
				Log($"Enemy jump input detected, velocity: {velocity}");
			}

			float direction = Input.GetAxis("move_left", "move_right");
			if (direction != 0)
			{
				velocity.X = direction * Speed;
				GD.Print($"Enemy moving with direction: {direction}");
				Log($"Enemy moving with direction: {direction}, velocity: {velocity}");
			}
			else
			{
				velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			}
		}
		else
		{
			// Khi không bị chiếm hữu, phát hiện và di chuyển tới người chơi
			if (player != null && !player.isDashing) // Không đuổi theo khi người chơi đang lướt
			{
				float distanceToPlayer = Position.DistanceTo(player.Position);
				if (distanceToPlayer <= DetectionRange)
				{
					// Di chuyển tới người chơi
					float direction = Mathf.Sign(player.Position.X - Position.X);
					velocity.X = direction * Speed;
					GD.Print($"Enemy moving toward player, direction: {direction}, distance: {distanceToPlayer}");
					Log($"Enemy moving toward player at {player.Position}, direction: {direction}, distance: {distanceToPlayer}, velocity: {velocity}");

					// Tấn công nếu trong phạm vi tấn công
					if (distanceToPlayer <= AttackRange && attackCooldownTimer <= 0)
					{
						AttackPlayer();
					}
				}
				else
				{
					// Dừng di chuyển nếu ngoài tầm phát hiện
					velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed * (float)delta * 2);
					Log($"Player out of detection range, stopping movement, distance: {distanceToPlayer}");
				}
			}
			else
			{
				// Dừng di chuyển nếu không tìm thấy người chơi hoặc người chơi đang lướt
				velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed * (float)delta * 2);
				if (player == null)
				{
					Log("No player found, stopping movement");
				}
				else
				{
					Log($"Player is dashing, stopping pursuit, player position: {player.Position}");
				}
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// Hàm tấn công người chơi
	private void AttackPlayer()
	{
		if (player != null)
		{
			player.TakeDamage(AttackDamage);
			attackCooldownTimer = AttackCooldown;
			GD.Print($"Enemy attacked player, dealing {AttackDamage} damage");
			Log($"Enemy attacked player at {player.Position}, dealing {AttackDamage} damage");
		}
	}

	// Hàm thiết lập trạng thái chiếm hữu (gọi bởi Player)
	public void SetPossessed(bool possessed)
	{
		isPossessed = possessed;
		GD.Print($"Enemy possession state set to: {isPossessed}");
		Log($"Enemy possession state set to: {isPossessed}, position: {Position}");
	}

	// Hàm ghi log
	private void Log(string message)
	{
		string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		string logEntry = $"[{timestamp}] {message}";
		try
		{
			using (StreamWriter writer = new StreamWriter(LogFilePath, true))
			{
				writer.WriteLine(logEntry);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to write to log file: {e.Message}");
		}
	}
}
