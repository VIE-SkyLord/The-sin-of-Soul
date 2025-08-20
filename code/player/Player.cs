using Godot;
using System;
using System.IO;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	[Export] public int MaxSoulHP = 100, CurSoulHP;
	[Export] public int MaxSoulMP = 100, CurSoulMP, recMpPerSec = 1;
	[Export] public int MaxSoulSP = 100, CurSoulSP;
	[Export] public int recSpPerSec = 1;
	[Export] public float Speed = 500.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float Gravity = 980.0f;
	[Export] public float DashSpeed = 1000.0f;
	[Export] public float DashDuration = 0.2f;
	[Export] public float DashCooldown = 1.0f;
	[Export] public float ReleaseJumpVelocity = -500.0f;
	[Export] public float ReleaseHorizontalVelocity = 200.0f;
	[Export] public float KnockbackVelocity = 600.0f;

	private bool isPossessing = false;
	private Node2D possessedEnemy = null;
	public Node2D _currentBody;

	[Export] public PackedScene[] MonsterScenes = new PackedScene[6];
	[Export] public int[] SummonCosts = new int[6];
	private int selectedMonsterSlot = 0;
	private Node2D summonedMonster = null;

	private static readonly string LogFilePath = Path.Combine(OS.GetUserDataDir(), "player_debug.log");
	private CollisionShape2D playerCollision;

	private AnimatedSprite2D animatedSprite;
	private bool facingRight = true;

	public bool isDashing = false;
	private float dashTimer = 0.0f;
	private float dashCooldownTimer = 0.0f;
	private uint originalCollisionLayer;
	private uint originalCollisionMask;
	private float dashDirection;

	private float mpAccumulator = 0.0f;
	private float spAccumulator = 0.0f;
	private float potionCooldownTimer = 0.0f;
	private const float PotionCooldown = 5.0f;
	private List<TextureRect> healthPotions = new List<TextureRect>();

	private bool isJumping = false;
	private float jumpCooldownTimer = 0.0f;
	private float coyoteTimer = 0.0f;
	private const float COYOTE_TIME = 0.1f;
	private const float JUMP_COOLDOWN = 0.3f;

	// Thêm biến để theo dõi trạng thái wall slide
	private bool isWallSliding = false;
	private float wallSlideTimer = 0.0f;
	private const float WALL_SLIDE_BUFFER_TIME = 0.1f; // 0.1 giây buffer

	private readonly string fullPotionTexturePath = "res://img/GUI/Status/Healing_Potion.png";
	private readonly string emptyPotionTexturePath = "res://img/GUI/Status/Healing_Potion_Used.png";

	private int originalSoulHP;
	private float possessedF;

	public override void _Ready()
	{
		CurSoulHP = MaxSoulHP;
		CurSoulMP = MaxSoulMP;
		CurSoulSP = MaxSoulSP;
		_currentBody = this;
		playerCollision = GetNode<CollisionShape2D>("CollisionShape2D");

		AddToGroup("Player");

		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		if (animatedSprite == null)
		{
			Log("Cảnh báo: Không tìm thấy AnimatedSprite2D!");
		}
		else
		{
			animatedSprite.Play("idle");
		}

		if (playerCollision == null)
		{
			Log("Cảnh báo: Không tìm thấy CollisionShape2D!");
		}

		CollisionLayer = 1u << 0;
		CollisionMask = (1u << 1) | (1u << 2);
		originalCollisionLayer = CollisionLayer;
		originalCollisionMask = CollisionMask;

		WallMinSlideAngle = Mathf.DegToRad(46); // góc tối thiểu ~46 độ mới tính là floor
		FloorBlockOnWall = false; // tránh bị khóa khi đồng thời va floor + wall
		SafeMargin = 0.02f;       // bớt 'dày' khi nảy vào tường
		FloorStopOnSlope = true;

		GD.Print($"Player setup - Layer: {CollisionLayer}, Mask: {CollisionMask}");
		SetProcessInput(true);

		foreach (Node node in GetTree().GetNodesInGroup("HealthPotion"))
		{
			if (node is TextureRect textureRect)
			{
				healthPotions.Add(textureRect);
			}
		}

		if (MonsterScenes[0] == null)
		{
			MonsterScenes[0] = GD.Load<PackedScene>("res://scenes/Skeleton.tscn");
			SummonCosts[0] = 30;
			if (MonsterScenes[0] == null)
			{
				GD.PrintErr("Không thể tải Skeleton.tscn!");
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		for (int i = 1; i <= 6; i++)
		{
			if (@event.IsActionPressed($"select_monster_{i}"))
			{
				selectedMonsterSlot = i - 1;
				GD.Print($"Selected monster slot: {selectedMonsterSlot + 1}");
			}
		}

		if (@event.IsActionPressed("summon_skeleton") && !isPossessing && summonedMonster == null && MonsterScenes[selectedMonsterSlot] != null && CurSoulSP >= SummonCosts[selectedMonsterSlot])
		{
			SummonMonster(selectedMonsterSlot);
		}

		if (@event.IsActionPressed("possess_release"))
		{
			if (!isPossessing)
			{
				Node2D nearestTarget = FindNearestPossessTarget();
				if (nearestTarget != null)
				{
					PossessTarget(nearestTarget);
				}
			}
			else
			{
				ReleaseTarget();
			}
		}

		if (Input.IsActionJustPressed("dodge") && !isPossessing && !isDashing && !isJumping && IsOnFloor() && dashCooldownTimer <= 0 && jumpCooldownTimer <= 0 && CurSoulMP >= 20)
		{
			dashDirection = Input.GetAxis("move_left", "move_right");
			if (dashDirection != 0)
			{
				CurSoulMP = Mathf.Max(0, CurSoulMP - 20);
				GD.Print($"Player dash. MP: {CurSoulMP}");
				StartDash(dashDirection);
			}
		}

		if (@event.IsActionPressed("use_potion") && potionCooldownTimer <= 0 && healthPotions.Count > 0)
		{
			GD.Print("Đã sử dụng HP potion");
			UseHealthPotion();
		}

		if (@event.IsActionPressed("take_damage"))
		{
			TakeDamage(20);
		}
		
		if (@event.IsActionPressed("heal"))
		{
			Heal(30);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		float deltaF = (float)delta;

		if (dashCooldownTimer > 0) dashCooldownTimer -= deltaF;
		if (jumpCooldownTimer > 0) jumpCooldownTimer -= deltaF;
		if (potionCooldownTimer > 0) potionCooldownTimer -= deltaF;

		if (isDashing)
		{
			dashTimer -= deltaF;
			if (dashTimer <= 0)
			{
				EndDash();
			}
		}

		mpAccumulator += recMpPerSec * deltaF;
		int mpToAdd = (int)mpAccumulator;
		if (mpToAdd > 0)
		{
			CurSoulMP = Mathf.Min(MaxSoulMP, CurSoulMP + mpToAdd);
			mpAccumulator -= mpToAdd;
		}

		spAccumulator += (recSpPerSec / 3.0f) * deltaF;
		int spToAdd = (int)spAccumulator;
		if (spToAdd > 0)
		{
			CurSoulSP = Mathf.Min(MaxSoulSP, CurSoulSP + spToAdd);
			spAccumulator -= spToAdd;
		}

		bool onFloor = IsOnFloor();
		bool onWall = IsOnWall();
		
		if (onFloor)
		{
			coyoteTimer = COYOTE_TIME;
			if (isJumping && velocity.Y >= 0)
			{
				isJumping = false;
				jumpCooldownTimer = JUMP_COOLDOWN;
			}
			// Reset wall sliding khi chạm đất
			isWallSliding = false;
			wallSlideTimer = 0.0f;
		}
		else
		{
			coyoteTimer -= deltaF;
		}

		if (!onFloor && !isDashing)
		{
			velocity.Y += Gravity * (velocity.Y > 0 ? 1.5f : 1.0f) * deltaF;
		}

		// --- INPUT & WALL LOGIC ---
		float direction = Input.GetAxis("move_left", "move_right");

		// Bật/tắt snap để không 'níu' vào tường khi đang trên không
		if (IsOnFloor()) FloorSnapLength = 8f; else FloorSnapLength = 0f;

		if (!isDashing && !isPossessing)
		{
			// Tính vận tốc ngang theo input trước
			float desiredX = direction * Speed;

			// Kiểm tra wall slide với buffer time để tránh flickering
			bool shouldWallSlide = false;
			
			if (onWall && !onFloor && velocity.Y >= 0)
			{
				Vector2 wn = GetWallNormal();       // pháp tuyến của tường
				bool wallLeft  = wn.X >  0.5f;      // tường bên trái (pháp tuyến hướng +X)
				bool wallRight = wn.X < -0.5f;      // tường bên phải (pháp tuyến hướng -X)

				// Kiểm tra nếu người chơi đang cố gắng áp vào tường
				bool tryingToPressWall = (wallLeft && direction < 0) || (wallRight && direction > 0);

				if (tryingToPressWall)
				{
					shouldWallSlide = true;
				}
			}

			// Cập nhật wall slide state với buffer
			if (shouldWallSlide)
			{
				isWallSliding = true;
				wallSlideTimer = WALL_SLIDE_BUFFER_TIME; // Reset buffer timer
			}
			else
			{
				if (wallSlideTimer > 0)
				{
					wallSlideTimer -= deltaF;
					// Giữ wall slide state trong thời gian buffer
					isWallSliding = (wallSlideTimer > 0);
				}
				else
				{
					isWallSliding = false;
				}
			}

			// Áp dụng physics dựa trên trạng thái wall slide
			if (isWallSliding && shouldWallSlide) // Chỉ áp dụng physics khi thực sự đang wall slide
			{
				// WALL SLIDE: giới hạn tốc độ rơi
				const float MIN_SLIDE = 140f;  // rơi tối thiểu để không đứng dính
				const float MAX_SLIDE = 260f;  // rơi tối đa để cảm giác trượt
				if (velocity.Y < MIN_SLIDE) velocity.Y = MIN_SLIDE;
				if (velocity.Y > MAX_SLIDE) velocity.Y = MAX_SLIDE;

				// KHÓA vận tốc ngang về phía tường
				velocity.X = 0;
			}
			else
			{
				velocity.X = Mathf.MoveToward(velocity.X, desiredX, Speed);
			}

			// Cập nhật hướng nhìn
			if (direction != 0 && animatedSprite != null)
			{
				bool newFacingRight = direction > 0;
				if (newFacingRight != facingRight)
				{
					facingRight = newFacingRight;
					animatedSprite.FlipH = !facingRight;
				}
			}
		}

		if (Input.IsActionJustPressed("move_up") && coyoteTimer > 0 && !isPossessing && !isDashing && !IsOnWallOnly() && jumpCooldownTimer <= 0)
		{
			velocity.Y = JumpVelocity;
			coyoteTimer = 0;
			isJumping = true;
			jumpCooldownTimer = JUMP_COOLDOWN;
			// Reset wall sliding khi nhảy
			isWallSliding = false;
			wallSlideTimer = 0.0f;
		}

		Velocity = velocity;
		MoveAndSlide();

		if (isPossessing && summonedMonster != null)
		{
			CurSoulHP = GetMonsterHealth(summonedMonster);
		}

		// Debug log để kiểm tra trạng thái
		// GD.Print($"OnFloor: {onFloor}, OnWall: {onWall}, WallSliding: {isWallSliding}, VelY: {velocity.Y:F1}");

		// Cập nhật hoạt ảnh với logic wall slide
		if (animatedSprite != null && !isPossessing)
		{
			string anim = GetCurrentAnimation(onFloor, velocity);
			if (animatedSprite.Animation != anim)
			{
				animatedSprite.Play(anim);
				// Debug log hoạt ảnh
				// GD.Print($"Playing animation: {anim}");
			}
		}
	}

	// Hàm mới để xác định hoạt ảnh hiện tại
	private string GetCurrentAnimation(bool onFloor, Vector2 velocity)
	{
		// Ưu tiên cao nhất: Dash
		if (isDashing)
		{
			return "dash";
		}
		
		// Trên mặt đất
		if (onFloor)
		{
			return Mathf.Abs(velocity.X) > 50 ? "run" : "idle";
		}
		
		// Trên không - kiểm tra theo thứ tự ưu tiên
		// 1. Wall slide (ưu tiên cao nhất khi trên không)
		if (isWallSliding)
		{
			return "slidewall";
		}
		
		// 2. Jump (đang bay lên)
		if (velocity.Y < -50) // Thêm ngưỡng để tránh flickering
		{
			return "jump";
		}
		
		// 3. Fall (đang rơi xuống)
		return "fall";
	}

	private void SummonMonster(int slot)
	{
		if (MonsterScenes[slot] == null)
		{
			GD.PrintErr($"Monster scene in slot {slot} is null!");
			return;
		}

		Node2D monster = MonsterScenes[slot].Instantiate() as Node2D;
		if (monster == null)
		{
			GD.PrintErr($"Failed to instantiate monster from slot {slot}!");
			return;
		}

		monster.Position = Position + new Vector2(facingRight ? 60 : -60, 0);
		GetParent().AddChild(monster);
		summonedMonster = monster;
		CurSoulSP -= SummonCosts[slot];

		GD.Print($"Summoned monster from slot {slot}. SP: {CurSoulSP}");
	}

	private Node2D FindNearestPossessTarget()
	{
		Node2D nearest = null;
		float nearestDistance = 150.0f;

		if (summonedMonster != null && !summonedMonster.IsQueuedForDeletion())
		{
			float distance = Position.DistanceTo(summonedMonster.Position);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = summonedMonster;
			}
		}

		foreach (Node2D enemy in GetTree().GetNodesInGroup("Enemies"))
		{
			float distance = Position.DistanceTo(enemy.Position);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = enemy;
			}
		}

		return nearest;
	}

	private void PossessTarget(Node2D target)
	{
		if (target == null) return;

		float F = 0.0f;
		int E = 0;
		int curE = 0;

		if (target == summonedMonster)
		{
			F = GetMonsterPossessThreshold(target);
			E = GetMonsterMaxHealth(target);
			curE = GetMonsterHealth(target);
		}
		else if (target is SlashSwordEnemy sword)
		{
			F = sword.PossessThresholdF;
			E = sword.MaxHealth;
			curE = sword.CurrentHealth;
		}
		else if (target is LightArcherEnemy archer)
		{
			F = archer.PossessThresholdF;
			E = archer.MaxHealth;
			curE = archer.CurrentHealth;
		}
		else
		{
			Log("Cannot possess: Invalid target type");
			return;
		}

		if (curE > (int)(E * F))
		{
			Log($"Target HP ({curE}) > {F*100}% of MaxHealth ({E})");
			return;
		}

		originalSoulHP = CurSoulHP;
		possessedF = F;
		int HPS = CurSoulHP + (int)(E * F);

		if (target == summonedMonster)
		{
			SetMonsterHealth(target, HPS);
			if (target is CharacterBody2D body)
			{
				body.SetProcessInput(true);
				body.SetProcess(true);
			}
		}
		else if (target is SlashSwordEnemy sword)
		{
			sword.CurrentHealth = HPS;
		}
		else if (target is LightArcherEnemy archer)
		{
			archer.CurrentHealth = HPS;
		}

		isPossessing = true;
		possessedEnemy = target;
		_currentBody = target;
		Visible = false;
		if (playerCollision != null) playerCollision.Disabled = true;
		SetPhysicsProcess(false);
		SetProcessInput(false);
		Position = _currentBody.Position;

		GD.Print($"Possessed target {target.Name}. HP: {HPS}, SP: {CurSoulSP}");
	}

	private void ReleaseTarget()
	{
		if (possessedEnemy == null) return;

		CurSoulHP = Math.Min(originalSoulHP, GetMonsterHealth(possessedEnemy));
		CurSoulSP = (int)(CurSoulSP - (CurSoulSP * (1 - possessedF)));

		if (possessedEnemy == summonedMonster)
		{
			if (possessedEnemy is CharacterBody2D body)
			{
				body.SetProcessInput(false);
			}
		}
		else if (possessedEnemy is SlashSwordEnemy sword)
		{
			sword.CollisionLayer = 1 << 4;
			sword.CollisionMask = 1 << 2;
		}
		else if (possessedEnemy is LightArcherEnemy archer)
		{
			archer.CollisionLayer = 1 << 4;
			archer.CollisionMask = 1 << 2;
		}

		isPossessing = false;
		_currentBody = this;
		if (playerCollision != null) playerCollision.Disabled = false;
		Visible = true;
		SetPhysicsProcess(true);
		SetProcessInput(true);
		Position = possessedEnemy.Position + new Vector2(0, -20);

		float horizontalDirection = ((CharacterBody2D)possessedEnemy).Velocity.X >= 0 ? 1.0f : -1.0f;
		Velocity = new Vector2(horizontalDirection * ReleaseHorizontalVelocity, ReleaseJumpVelocity);
		if (animatedSprite != null)
		{
			facingRight = horizontalDirection > 0;
			animatedSprite.FlipH = !facingRight;
		}

		possessedEnemy = null;
		GD.Print($"Released target. HP: {CurSoulHP}, SP: {CurSoulSP}");
	}

	private int GetMonsterHealth(Node2D monster)
	{
		if (monster is SlashSwordEnemy sword) return sword.CurrentHealth;
		if (monster is LightArcherEnemy archer) return archer.CurrentHealth;
		return 0;
	}

	private int GetMonsterMaxHealth(Node2D monster)
	{
		if (monster is SlashSwordEnemy sword) return sword.MaxHealth;
		if (monster is LightArcherEnemy archer) return archer.MaxHealth;
		return 100;
	}

	private float GetMonsterPossessThreshold(Node2D monster)
	{
		if (monster is SlashSwordEnemy sword) return sword.PossessThresholdF;
		if (monster is LightArcherEnemy archer) return archer.PossessThresholdF;
		return 0.5f;
	}

	private void SetMonsterHealth(Node2D monster, int health)
	{
		if (monster is SlashSwordEnemy sword) sword.CurrentHealth = health;
		else if (monster is LightArcherEnemy archer) archer.CurrentHealth = health;
	}

	public void OnMonsterDeath(Node2D deadMonster)
	{
		if (summonedMonster == deadMonster)
		{
			if (isPossessing)
			{
				CurSoulHP = 1;
				isPossessing = false;
				_currentBody = this;
				if (playerCollision != null) playerCollision.Disabled = false;
				Visible = true;
				SetPhysicsProcess(true);
				SetProcessInput(true);
				Position = deadMonster.Position + new Vector2(0, -20);
				GD.Print("Possessed monster died! HP reduced to 1");
			}
			summonedMonster = null;
		}
	}

	private void StartDash(float direction)
	{
		if (isDashing || dashCooldownTimer > 0) return;

		isDashing = true;
		dashTimer = DashDuration;
		dashCooldownTimer = DashCooldown;
		dashDirection = direction;

		if (animatedSprite != null)
		{
			facingRight = direction > 0;
			animatedSprite.FlipH = !facingRight;
			animatedSprite.Play("dash");
		}

		originalCollisionLayer = CollisionLayer;
		originalCollisionMask = CollisionMask;

		CollisionLayer = 1u << 3;
		CollisionMask = 1u << 2;

		Velocity = new Vector2(direction * DashSpeed, 0);

		GD.Print("Dash started");
	}

	private void EndDash()
	{
		isDashing = false;
		CollisionLayer = originalCollisionLayer;
		CollisionMask = originalCollisionMask;
		Velocity = new Vector2(0, Velocity.Y);
		MoveAndSlide();
		GD.Print("Dash ended");
	}

	private void UseHealthPotion()
	{
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
			CurSoulHP = Mathf.Min(MaxSoulHP, CurSoulHP + (int)(MaxSoulHP * 0.3f));
			potionToUse.Texture = GD.Load<Texture2D>(emptyPotionTexturePath);
			potionCooldownTimer = PotionCooldown;
		}
	}

	public void TakeDamage(int damage)
	{
		if (isDashing) return;
		CurSoulHP = Mathf.Max(0, CurSoulHP - damage);
	}

	public void Heal(int amount)
	{
		CurSoulHP = Mathf.Min(MaxSoulHP, CurSoulHP + amount);
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
			GD.PrintErr($"Không thể ghi vào file log: {e.Message}");
		}
	}
}
