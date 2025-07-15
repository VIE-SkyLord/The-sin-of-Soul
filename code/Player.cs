using Godot;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody2D
{
	[Export] public int MaxSoulHP = 100, CurSoulHP;
	[Export] public int MaxSoulMP = 100, CurSoulMP, recMpPerSec=1;
	[Export] public float Speed = 500.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float Gravity = 980.0f;
	[Export] public float DashSpeed = 1000.0f; // Tốc độ lướt
	[Export] public float DashDuration = 0.2f; // Thời gian lướt (giây)
	[Export] public float DashCooldown = 1.0f; // Thời gian chờ giữa các lần lướt (giây)
	[Export] public float ReleaseJumpVelocity = -500.0f; // Vận tốc nhảy khi thoát xác
	[Export] public float ReleaseHorizontalVelocity = 200.0f; // Vận tốc ngang khi thoát xác
	[Export] public float KnockbackVelocity = 600.0f; // Vận tốc đẩy lùi khi va chạm kẻ địch sau lướt

	private bool isPossessing = false;
	private Node2D possessedEnemy = null;
	public Node2D _currentBody; // Public for camera to access
	private static readonly string LogFilePath = Path.Combine(OS.GetUserDataDir(), "player_debug.log");
	private CollisionShape2D playerCollision; // Reference to player's collision shape
	private bool isDashing = false; // Trạng thái đang lướt
	private float dashTimer = 0.0f; // Đếm thời gian lướt
	private float dashCooldownTimer = 0.0f; // Đếm thời gian hồi chiêu
	private uint originalCollisionLayer; // Lưu layer ban đầu của Player
	private uint originalCollisionMask;
	private Dictionary<Node2D, (uint Layer, uint Mask)> enemyCollisionSettings; // Lưu layer và mask ban đầu của từng Enemy
	private float mpAccumulator = 0.0f; // Biến tích lũy MP thập phân
	private float potionCooldownTimer = 0.0f; // Delay giữa các lần sử dụng bình máu
	private const float PotionCooldown = 5.0f; // Delay 5 giây
	private List<TextureRect> healthPotions = new List<TextureRect>(); // Danh sách các bình máu
	private float dashDirection; // Lưu hướng lướt để sử dụng khi kết thúc

	// Đường dẫn đến texture đầy và rỗng
	private readonly string fullPotionTexturePath = "res://img/kenney_abstract-platformer/PNG/GUI/Status/Healing_Potion.png";
	private readonly string emptyPotionTexturePath = "res://img/kenney_abstract-platformer/PNG/GUI/Status/Healing_Potion_Used.png";

	public override void _Ready()
	{
		CurSoulHP = MaxSoulHP;//set full HP
		CurSoulMP = MaxSoulMP;//set full MP
		_currentBody = this; // Default to player body
		playerCollision = GetNode<CollisionShape2D>("CollisionShape2D"); // Adjust node name if different
		if (playerCollision == null)
		{
			Log("Warning: CollisionShape2D not found on Player node!");
		}
		originalCollisionLayer = CollisionLayer; // Lưu layer ban đầu của Player
		CollisionMask &= ~(1u << 4); // Không va chạm với layer 5 ("DeadEnemy")
		originalCollisionMask = CollisionMask; // Lưu mask ban đầu sau khi điều chỉnh
		enemyCollisionSettings = new Dictionary<Node2D, (uint Layer, uint Mask)>(); // Khởi tạo dictionary
		Log("Player initialized at position: " + Position);
		SetProcessInput(true); // Ensure input processing is always enabled
		
		// Tìm và lưu các node HealthPotion trong scene
		foreach (Node node in GetTree().GetNodesInGroup("HealthPotion"))
		{
			if (node is TextureRect textureRect)
			{
				healthPotions.Add(textureRect);
				if (textureRect.Texture?.ResourcePath == fullPotionTexturePath)
				{
					Log($"Found full HealthPotion at position: {textureRect.GlobalPosition}");
				}
				else
				{
					Log($"Found HealthPotion (not full) at position: {textureRect.GlobalPosition}");
				}
			}
			else
			{
				Log($"Found node (not TextureRect) in HealthPotion group at unknown position");
			}
		}
	}	

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("possess_release"))
		{
			GD.Print("Input detected: possess_release pressed in _Input");
			Log($"Possess/Release key pressed. isPossessing = {isPossessing}, Player position: {Position}");
			if (!isPossessing)
			{
				Node2D nearestEnemy = FindNearestEnemy();
				if (nearestEnemy != null)
				{
					Log($"Nearest enemy found: {nearestEnemy.Name} at position: {nearestEnemy.Position}");
					PossessEnemy(nearestEnemy);
				}
				else
				{
					Log("No enemy found nearby to possess.");
				}
			}
			else
			{
				Log($"Attempting to release. Current enemy: {possessedEnemy?.Name}, Enemy position: {possessedEnemy?.Position}");
				ReleaseEnemy();
			}
		}

		// Xử lý né tránh (dodge)
		if (Input.IsActionJustPressed("dodge") && !isPossessing && !isDashing && dashCooldownTimer <= 0 && CurSoulMP>=20)
		{
			dashDirection = Input.GetAxis("move_left", "move_right");
			if (dashDirection != 0)
			{
				//Tiêu hao Mana
				CurSoulMP = Mathf.Max(0, CurSoulMP - 20);
				GD.Print($"Player dash. Current MP: {CurSoulMP}");
				StartDash(dashDirection);
			}
		}
		
		// Sử dụng bình máu với phím U
		if (@event.IsActionPressed("use_potion") && potionCooldownTimer <= 0 && healthPotions.Count > 0)
		{
			UseHealthPotion();
		}

		if (@event.IsActionPressed("take_damage")) // Nhấn Enter để test
		{
			TakeDamage(20);
		}
		if (@event.IsActionPressed("heal")) // Nhấn Esc để test
		{
			Heal(30);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// Cập nhật thời gian lướt và hồi chiêu
		if (isDashing)
		{
			dashTimer -= (float)delta;
			if (dashTimer <= 0)
			{
				EndDash();
			}
		}
		if (dashCooldownTimer > 0)
		{
			dashCooldownTimer -= (float)delta;
		}
		
		// Hồi MP theo thời gian
		mpAccumulator += recMpPerSec * (float)delta;
		int mpToAdd = (int)mpAccumulator;
		if (mpToAdd > 0)
		{
			 CurSoulMP = Mathf.Min(MaxSoulMP, CurSoulMP + mpToAdd);
			 mpAccumulator -= mpToAdd; // Giảm accumulator sau khi thêm
			 Log($"MP recovered: CurSoulMP = {CurSoulMP}, mpToAdd = {mpToAdd}");
		}
		
		// Cập nhật delay sử dụng bình máu
		if (potionCooldownTimer > 0)
		{
			potionCooldownTimer -= (float)delta;
		}
		
		// Áp dụng trọng lực
		if (!IsOnFloor() && !isDashing)
			velocity.Y += Gravity * (float)delta;

		// Xử lý nhảy
		if (Input.IsActionPressed("move_up") && IsOnFloor() && !isPossessing && !isDashing)
			velocity.Y = JumpVelocity;

		// Xử lý di chuyển
		if (!isDashing)
		{
			float direction = Input.GetAxis("move_left", "move_right");
			if (direction != 0 && !isPossessing)
				velocity.X = direction * Speed;
			else if (!isPossessing)
				velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private void StartDash(float direction)
	{
		if (isDashing || dashCooldownTimer > 0) return;

		isDashing = true;
		dashTimer = DashDuration;
		dashCooldownTimer = DashCooldown;
		dashDirection = direction;

		// Tắt va chạm với layer 2 (Enemy) trong CollisionMask của Player
		CollisionMask = 1u << 2; // Chỉ va chạm với layer 3 (World)
		SetCollisionMaskValue(2, false); // Đảm bảo tắt layer 2 (Enemy)

		// Giữ CollisionShape2D hoạt động
		if (playerCollision != null && playerCollision.Disabled)
		{
			playerCollision.Disabled = false;
		}

		// Lưu và vô hiệu hóa va chạm của tất cả Enemy trong nhóm "Enemies"
		enemyCollisionSettings.Clear();
		foreach (Node2D enemy in GetTree().GetNodesInGroup("Enemies"))
		{
			if (enemy is CharacterBody2D enemyCharacter)
			{
				enemyCollisionSettings[enemy] = (enemyCharacter.CollisionLayer, enemyCharacter.CollisionMask);
				enemyCharacter.CollisionLayer &= ~(1u << 0); // Loại bỏ layer 1 (Player) khỏi CollisionLayer
				enemyCharacter.CollisionMask &= ~(1u << 0); // Loại bỏ layer 1 (Player) khỏi CollisionMask
				GD.Print($"Adjusted CollisionLayer of enemy {enemy.Name} to: {enemyCharacter.CollisionLayer}, CollisionMask to: {enemyCharacter.CollisionMask}");
			}
		}

		// Áp dụng vận tốc dash
		Velocity = new Vector2(direction * DashSpeed, 0);
		GD.Print($"Started dash, CollisionLayer: {CollisionLayer}, CollisionMask: {CollisionMask}, CollisionShape Disabled: {playerCollision?.Disabled}, Position: {Position}");
	}

	private void EndDash()
	{
		isDashing = false;

		// Khôi phục CollisionMask và CollisionLayer ban đầu của Player
		CollisionMask = originalCollisionMask;   // Khôi phục về giá trị ban đầu
		CollisionLayer = originalCollisionLayer; // Khôi phục layer ban đầu

		// Khôi phục CollisionLayer và CollisionMask của tất cả Enemy
		foreach (var entry in enemyCollisionSettings)
		{
			if (entry.Key is CharacterBody2D enemyCharacter && entry.Key != null)
			{
				enemyCharacter.CollisionLayer = entry.Value.Layer; // Khôi phục layer ban đầu
				enemyCharacter.CollisionMask = entry.Value.Mask; // Khôi phục mask ban đầu
				GD.Print($"Restored CollisionLayer of enemy {entry.Key.Name} to: {enemyCharacter.CollisionLayer}, CollisionMask to: {enemyCharacter.CollisionMask}");
			}
		}
		enemyCollisionSettings.Clear();

		// Đảm bảo CollisionShape2D được kích hoạt
		if (playerCollision != null && playerCollision.Disabled)
		{
			playerCollision.Disabled = false;
			GD.Print("CollisionShape2D re-enabled in EndDash");
		}

		// Kiểm tra va chạm với kẻ địch tại điểm kết thúc lướt
		Node2D collidedEnemy = null;
		var collision = MoveAndCollide(Velocity * (float)GetPhysicsProcessDeltaTime(), true, 0.0f, true); // Test collision without moving
		if (collision != null)
		{
			var collider = collision.GetCollider() as Node2D;
			if (collider != null && collider.IsInGroup("Enemies"))
			{
				collidedEnemy = collider;
				// Tạm thời vô hiệu hóa MoveAndSlide của enemy để không bị đẩy
				if (collider is CharacterBody2D enemyCharacter)
				{
					enemyCharacter.Velocity = Vector2.Zero; // Ngăn enemy di chuyển do va chạm
				}
			}
		}

		if (collidedEnemy != null)
		{
			// Áp dụng lực đẩy lùi cho người chơi
			float knockbackDirection = -dashDirection; // Đẩy lùi ngược hướng lướt
			Velocity = new Vector2(knockbackDirection * KnockbackVelocity, ReleaseJumpVelocity); // Áp dụng lực đẩy lùi
			GD.Print($"Player collided with enemy {collidedEnemy.Name} at end of dash. Applying knockback: {Velocity}");
			Log($"Player knocked back after dash collision with {collidedEnemy.Name} at position: {collidedEnemy.Position}, new player velocity: {Velocity}");
		}
		else
		{
			// Nếu không va chạm, đặt lại vận tốc ngang
			Velocity = new Vector2(0, Velocity.Y);
		}

		// Di chuyển với vận tốc mới (bao gồm knockback nếu có)
		MoveAndSlide();
		GD.Print($"Ended dash, CollisionLayer: {CollisionLayer}, CollisionMask restored: {CollisionMask}, CollisionShape Disabled: {playerCollision?.Disabled}, Position: {Position}");
	}

	private Node2D FindNearestEnemy()
	{
		Node2D nearest = null;
		float nearestDistance = 150.0f; // Proximity threshold
		int enemyCount = GetTree().GetNodesInGroup("Enemies").Count;
		Log($"Searching for enemies. Total enemies in group: {enemyCount}");
		foreach (Node2D enemy in GetTree().GetNodesInGroup("Enemies"))
		{
			float distance = Position.DistanceTo(enemy.Position);
			Log($"Checking enemy {enemy.Name} at position {enemy.Position} with distance {distance}");
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = enemy;
			}
		}
		return nearest;
	}

	private void PossessEnemy(Node2D enemy)
	{
		isPossessing = true;
		possessedEnemy = enemy;
		_currentBody = enemy;
		Visible = false;
		if (playerCollision != null)
		{
			playerCollision.Disabled = true;
		}
		SetPhysicsProcess(false);
		Position = _currentBody.Position;
		if (possessedEnemy is Enermy enemyScript)
		{
			enemyCollisionSettings[enemy] = (enemyScript.CollisionLayer, enemyScript.CollisionMask); // Lưu layer và mask
			enemyScript.SetPossessed(true);
			enemyScript.SetPhysicsProcess(true);
			Log($"Possessed enemy: {enemy.Name} at position: {enemy.Position}, enemy layer: {enemyScript.CollisionLayer}");
		}
		else
		{
			Log($"Possess failed: {enemy.Name} is not of type Enermy.");
		}
	}

	private void ReleaseEnemy()
	{
		if (possessedEnemy != null)
		{
			if (possessedEnemy is Enermy enemyScript)
			{
				isPossessing = false;
				Log($"Enemy state before release: Velocity = {enemyScript.Velocity}, IsOnFloor = {enemyScript.IsOnFloor()}");
				enemyScript.SetPossessed(false);
				
				// Chuyển layer của cái xác sang "DeadEnemy" (Layer 5)
				enemyScript.CollisionLayer = 1 << 4; // Layer 5 (0-based index, bit 4)
				enemyScript.CollisionMask = 1 << 2; // Chỉ va chạm với layer 3 ("World")
				
				// Nhảy ra khỏi xác
				_currentBody = this;
				possessedEnemy = null;
				if (playerCollision != null)
				{
					playerCollision.Disabled = false;
				}
				Visible = true;
				SetPhysicsProcess(true);
				
				// Đặt vị trí linh hồn ngay trên cái xác
				Position = enemyScript.Position + new Vector2(0, -20); // Lên trên một chút
				
				// Áp dụng vận tốc nhảy ra
				float horizontalDirection = enemyScript.Velocity.X >= 0 ? 1.0f : -1.0f; // Dựa vào hướng của Enemy
				Velocity = new Vector2(horizontalDirection * ReleaseHorizontalVelocity, ReleaseJumpVelocity);
				
				Log($"Released enemy, returned to player control at position: {Position}, with velocity: {Velocity}, enemy layer changed to: {enemyScript.CollisionLayer}");
			}
			else
			{
				Log("Release failed: Possessed enemy is not of type Enermy.");
			}
		}
		else
		{
			Log("Release failed: No possessed enemy.");
		}
	}

	public void TakeDamage(int damage)
	{
		if (isDashing)
		{
			GD.Print($"Damage ignored during dash. Attempted damage: {damage}");
			Log($"Damage ignored during dash. Attempted damage: {damage}, Player position: {Position}");
			return;
		}
		CurSoulHP = Mathf.Max(0, CurSoulHP - damage);
		GD.Print($"Player took {damage} damage. Current HP: {CurSoulHP}");
		Log($"Player took {damage} damage. Current HP: {CurSoulHP}, Player position: {Position}");
	}

	public void Heal(int amount)
	{
		CurSoulHP = Mathf.Min(MaxSoulHP, CurSoulHP + amount);
		GD.Print($"Player healed {amount}. Current HP: {CurSoulHP}");
		Log($"Player healed {amount}. Current HP: {CurSoulHP}, Player position: {Position}");
	}
	
	private void UseHealthPotion()
	{
		// Tìm bình máu đầy từ cuối danh sách đến đầu
		TextureRect potionToUse = null;
		for (int i = healthPotions.Count - 1; i >= 0; i--)
		{
			if (healthPotions[i].Texture?.ResourcePath == fullPotionTexturePath)
			{
				potionToUse = healthPotions[i];
				break;
			}
		}

		if (potionToUse != null)
		{
			// Hồi 30% MaxSoulHP
			int healAmount = (int)(MaxSoulHP * 0.3f);
			CurSoulHP = Mathf.Min(MaxSoulHP, CurSoulHP + healAmount);
			GD.Print($"Used Health Potion, healed {healAmount}. Current HP: {CurSoulHP}");
			Log($"Used Health Potion, healed {healAmount}, remaining full potions: {healthPotions.Count(p => p.Texture?.ResourcePath == fullPotionTexturePath)}");
			// Thay texture từ đầy sang rỗng
			potionToUse.Texture = GD.Load<Texture2D>(emptyPotionTexturePath);

			// Bắt đầu cooldown 5 giây
			potionCooldownTimer = PotionCooldown;
		}
		else
		{
			Log("No full Health Potions available!");
		}
	}

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
