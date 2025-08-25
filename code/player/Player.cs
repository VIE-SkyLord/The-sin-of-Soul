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
	[Export] public float StealthDuration = 5.0f;
	[Export] public float StealthCooldown = 10.0f;
	[Export] public float BlockDuration = 2.0f;
	[Export] public float BlockCooldown = 5.0f;
	[Export] public int MaxJumps = 2;
	[Export] public int MaxDashes = 2;

	private bool isPossessing = false;
	private Node2D possessedEnemy = null;
	public Node2D _currentBody;

	[Export] public PackedScene[] MonsterScenes = new PackedScene[6];
	[Export] public int[] SummonCosts = new int[6];
	[Export] public int[] UnlockedSlots = new int[6] { 1, 1, 0, 0, 0, 0 };
	public int selectedMonsterSlot = 0;
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

	private bool isWallSliding = false;
	private float wallSlideTimer = 0.0f;
	private const float WALL_SLIDE_BUFFER_TIME = 0.1f;
	
	private bool isStealth = false;
	private float stealthTimer = 0.0f;
	private float stealthCooldownTimer = 0.0f;
	private bool isBlocking = false;
	private float blockTimer = 0.0f;
	private float blockCooldownTimer = 0.0f;
	private int currentJumps = 0;
	private int currentDashes = 0;
	private bool canAirDash = false;

	private readonly string fullPotionTexturePath = "res://img/GUI/Status/Healing_Potion.png";
	private readonly string emptyPotionTexturePath = "res://img/GUI/Status/Healing_Potion_Used.png";

	public override void _Ready()
	{
		CurSoulHP = MaxSoulHP;
		CurSoulMP = MaxSoulMP;
		CurSoulSP = MaxSoulSP;
		currentJumps = MaxJumps;
		currentDashes = MaxDashes;
		_currentBody = this;
		playerCollision = GetNode<CollisionShape2D>("CollisionShape2D");

		AddToGroup("Player");

		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		if (animatedSprite == null) Log("Cảnh báo: Không tìm thấy AnimatedSprite2D!");
		else animatedSprite.Play("idle");

		if (playerCollision == null) Log("Cảnh báo: Không tìm thấy CollisionShape2D!");

		CollisionLayer = 1u << 0;
		CollisionMask = (1u << 1) | (1u << 2);
		originalCollisionLayer = CollisionLayer;
		originalCollisionMask = CollisionMask;

		WallMinSlideAngle = Mathf.DegToRad(46);
		FloorBlockOnWall = false;
		SafeMargin = 0.02f;
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
			if (MonsterScenes[0] == null) GD.PrintErr("Không thể tải Skeleton.tscn!");
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		/*for (int i = 1; i <= 6; i++)
		{
			if (@event.IsActionPressed($"select_monster_{i}"))
			{
				selectedMonsterSlot = i - 1;
				GD.Print($"Selected monster slot: {selectedMonsterSlot + 1}");
			}
		}*/

		/*if (@event.IsActionPressed("summon_monster") && !isPossessing && summonedMonster == null && MonsterScenes[selectedMonsterSlot] != null && CurSoulSP >= SummonCosts[selectedMonsterSlot])
		{
			SummonMonster(selectedMonsterSlot);
		}*/

		if (@event.IsActionPressed("possess_release"))
		{
			GD.Print("Possess/release action pressed, isPossessing: " + isPossessing);
			if (!isPossessing)
			{
				Node2D nearestTarget = FindNearestPossessTarget();
				if (nearestTarget != null) PossessTarget(nearestTarget);
			}
			else
			{
				ReleaseTarget();
			}
		}

		if (@event.IsActionPressed("dodge") && !isPossessing && !isDashing && !isJumping && IsOnFloor() && dashCooldownTimer <= 0 && jumpCooldownTimer <= 0 && CurSoulMP >= 20)
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

		if (@event.IsActionPressed("attack") && isPossessing && _currentBody != null)
		{
			GD.Print($"Attack pressed - isPossessing: {isPossessing}, _currentBody: {_currentBody.Name}");
	
			// Kiểm tra trạng thái attack
			bool canAttack = true;
			if (_currentBody.HasMethod("IsAttacking"))
			{
				canAttack = !(bool)_currentBody.Call("IsAttacking");
				GD.Print($"Can attack: {canAttack}");
			}
			
			if (canAttack)
			{
				// ... phần xử lý attack
				GD.Print($"Attempting attack with {_currentBody.Name}");
			
				// Đảm bảo cập nhật hướng nhìn trước khi tấn công
				float direction = Input.GetAxis("move_left", "move_right");
				if (direction != 0)
				{
					facingRight = direction > 0;
					if (_currentBody.HasMethod("UpdateFacing"))
					{
						_currentBody.Call("UpdateFacing", direction);
					}
				}
				
				if (_currentBody.HasMethod("PerformAttack"))
				{
					_currentBody.Call("PerformAttack");
				}
				else
				{
					GD.Print($"No PerformAttack method in {_currentBody.Name}");
				}
			}
		}
		
		if (@event.IsActionPressed("stealth") && !isStealth && stealthCooldownTimer <= 0 && CurSoulMP >= 30)
		{
			ActivateStealth();
		}

		if (@event.IsActionPressed("block") && !isBlocking && blockCooldownTimer <= 0 && CurSoulMP >= 20)
		{
			ActivateBlock();
		}

		if (@event.IsActionPressed("double_jump") && isJumping && !IsOnFloor() && currentJumps > 0)
		{
			PerformDoubleJump();
		}

		if (@event.IsActionPressed("air_dash") && !IsOnFloor() && currentDashes > 0 && CurSoulMP >= 15)
		{
			PerformAirDash();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = _currentBody is CharacterBody2D body ? body.Velocity : Velocity;
		float deltaF = (float)delta;

		if (dashCooldownTimer > 0) dashCooldownTimer -= deltaF;
		if (jumpCooldownTimer > 0) jumpCooldownTimer -= deltaF;
		if (potionCooldownTimer > 0) potionCooldownTimer -= deltaF;
		
		// Nếu đang nhập xác, sử dụng vật lý của enemy
		if (isPossessing && possessedEnemy is CharacterBody2D enemyBody)
		{
			// Xử lý input và vật lý cho enemy
			ProcessPossessedEnemyPhysics(enemyBody, delta);
			return;
		}

		if (isDashing)
		{
			dashTimer -= deltaF;
			if (dashTimer <= 0) EndDash();
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

		bool onFloor = _currentBody is CharacterBody2D cb && cb.IsOnFloor();
		bool onWall = _currentBody is CharacterBody2D cb2 && cb2.IsOnWall();

		if (onFloor)
		{
			coyoteTimer = COYOTE_TIME;
			if (isJumping && velocity.Y >= 0)
			{
				isJumping = false;
				jumpCooldownTimer = JUMP_COOLDOWN;
			}
			isWallSliding = false;
			wallSlideTimer = 0.0f;
			currentJumps = MaxJumps;
			currentDashes = MaxDashes;
			canAirDash = true;
		}
		else
		{
			coyoteTimer -= deltaF;
		}

		if (!onFloor && !isDashing)
		{
			velocity.Y += Gravity * (velocity.Y > 0 ? 1.5f : 1.0f) * deltaF;
		}

		if (_currentBody is CharacterBody2D) FloorSnapLength = onFloor ? 8f : 0f;

		float direction = Input.GetAxis("move_left", "move_right");

		if (!isDashing)
		{
			float speed = _currentBody.Get("Speed").AsSingle() is float s ? s : Speed;
			float desiredX = direction * speed;

			bool shouldWallSlide = false;
			if (onWall && !onFloor && velocity.Y >= 0)
			{
				Vector2 wn = (_currentBody as CharacterBody2D)?.GetWallNormal() ?? Vector2.Zero;
				bool wallLeft = wn.X > 0.5f;
				bool wallRight = wn.X < -0.5f;
				bool tryingToPressWall = (wallLeft && direction < 0) || (wallRight && direction > 0);
				if (tryingToPressWall) shouldWallSlide = true;
			}

			if (shouldWallSlide)
			{
				isWallSliding = true;
				wallSlideTimer = WALL_SLIDE_BUFFER_TIME;
				if (velocity.Y < 140f) velocity.Y = 140f;
				if (velocity.Y > 260f) velocity.Y = 260f;
				velocity.X = 0;
			}
			else
			{
				if (wallSlideTimer > 0)
				{
					wallSlideTimer -= deltaF;
					isWallSliding = wallSlideTimer > 0;
				}
				else
				{
					isWallSliding = false;
				}
				velocity.X = Mathf.MoveToward(velocity.X, desiredX, speed);
			}

			if (direction != 0)
			{
				var sprite = _currentBody.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
				if (sprite != null)
				{
					bool newFacingRight = direction > 0;
					if (newFacingRight != facingRight)
					{
						facingRight = newFacingRight;
						sprite.FlipH = !facingRight;
					}
				}
			}
		}

		if (Input.IsActionJustPressed("move_up") && coyoteTimer > 0 && !isDashing && !onWall && jumpCooldownTimer <= 0)
		{
			float jumpVel = _currentBody.Get("JumpVelocity").AsSingle() is float j ? j : JumpVelocity;
			velocity.Y = jumpVel;
			coyoteTimer = 0;
			isJumping = true;
			jumpCooldownTimer = JUMP_COOLDOWN;
			isWallSliding = false;
			wallSlideTimer = 0.0f;
		}

		if (_currentBody is CharacterBody2D body2D)
		{
			body2D.Velocity = velocity;
			body2D.MoveAndSlide();
		}
		else
		{
			Velocity = velocity;
			MoveAndSlide();
		}

		if (isPossessing && summonedMonster != null)
		{
			CurSoulHP = GetMonsterHealth(summonedMonster);
		}

		var animSprite = _currentBody.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animSprite != null && (!isPossessing || animSprite.Animation != "attack"))
		{
			string anim = GetCurrentAnimation(onFloor, velocity);
			if (animSprite.Animation != anim)
			{
				animSprite.Play(anim);
			}
		}
		UpdateSpecialAbilities(deltaF);
	}

	private string GetCurrentAnimation(bool onFloor, Vector2 velocity)
	{
		if (isDashing) return "dash";
		if (onFloor) return Mathf.Abs(velocity.X) > 50 ? "run" : "idle";
		if (isWallSliding) return "slidewall";
		if (velocity.Y < -50) return "jump";
		return "fall";
	}

	public void SummonMonster(int slot)
	{
		if (MonsterScenes[slot] == null) return;
		
		Node2D monster = MonsterScenes[slot].Instantiate() as Node2D;
		monster.Position = Position + new Vector2(facingRight ? 60 : -60, 0);
		GetParent().AddChild(monster);
		summonedMonster = monster;
		CurSoulSP -= SummonCosts[slot];
	}
	
	private void UpdateSpecialAbilities(float delta)
	{
		if (isStealth)
		{
			stealthTimer -= delta;
			if (stealthTimer <= 0)
			{
				EndStealth();
			}
		}
		
		if (stealthCooldownTimer > 0) stealthCooldownTimer -= delta;
		if (blockCooldownTimer > 0) blockCooldownTimer -= delta;
		
		if (isBlocking)
		{
			blockTimer -= delta;
			if (blockTimer <= 0)
			{
				EndBlock();
			}
		}
	}

	private Node2D FindNearestPossessTarget()
	{
		Node2D nearest = null;
		float nearestDistance = 320.0f;
		
		GD.Print("Bắt đầu tìm target để nhập xác...");
		
		// Check summoned monster first
		if (summonedMonster != null && !summonedMonster.IsQueuedForDeletion())
		{
			float distance = Position.DistanceTo(summonedMonster.Position);
			GD.Print($"Summoned monster: {summonedMonster.Name}, distance: {distance}");
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = summonedMonster;
				GD.Print($"Chọn summoned monster: {nearest.Name}");
			}
		}

		// Check ALL enemies
		foreach (Node2D enemy in GetTree().GetNodesInGroup("Enemies"))
		{
			if (enemy.IsQueuedForDeletion()) continue;
			
			float distance = Position.DistanceTo(enemy.Position);
			bool canPossess = CanPossessEnemy(enemy);
			
			GD.Print($"Enemy: {enemy.Name}, distance: {distance}, canPossess: {canPossess}");
			
			if (canPossess && distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = enemy;
				GD.Print($"Chọn enemy: {nearest.Name}");
			}
		}

		if (nearest == null)
		{
			GD.Print("Không tìm thấy target nào trong phạm vi!");
		}
		else
		{
			GD.Print($"Target được chọn: {nearest.Name}, distance: {nearestDistance}");
		}
		
		return nearest;
	}
	
	private bool CanPossessEnemy(Node2D enemy)
	{
		//test
		//return true;
		
		// Luôn cho phép nhập xác quái triệu hồi
		if (enemy == summonedMonster) return true;
		
		// Cho phép nhập xác quái đã chết
		int currentHp = GetMonsterHealth(enemy);
		if (currentHp <= 0) return true;
		
		// Kiểm tra ngưỡng possession
		if (enemy.HasMethod("GetPossessThresholdF"))
		{
			float threshold = (float)enemy.Call("GetPossessThresholdF");
			int maxHp = GetMonsterMaxHealth(enemy);
			float healthRatio = (float)currentHp / maxHp;
			
			return healthRatio <= threshold;
		}
		
		return false;
	}
	
	private void CollectMonster(Node2D monster)
	{
		for (int i = 0; i < MonsterScenes.Length; i++)
		{
			if (MonsterScenes[i] == null)
			{
				// Lấy scene từ monster
				PackedScene monsterScene = ResourceLoader.Load<PackedScene>(monster.SceneFilePath);
				MonsterScenes[i] = monsterScene;
				
				// Thiết lập chi phí triệu hồi (có thể dựa trên sức mạnh của quái)
				SummonCosts[i] = CalculateSummonCost(monster);
				
				GD.Print($"Đã thu thập {monster.Name} vào slot {i}");
				return;
			}
		}
		
		GD.Print("Kho đồ đã đầy, không thể thu thập thêm!");
	}
	
	private int CalculateSummonCost(Node2D monster)
	{
		int maxHp = GetMonsterMaxHealth(monster);
		int baseCost = maxHp / 5; // Chi phí cơ bản dựa trên máu
		
		// Có thể thêm các yếu tố khác như sát thương, phòng thủ, v.v.
		if (monster.HasMethod("GetDamage"))
		{
			int damage = (int)monster.Call("GetDamage");
			baseCost += damage / 2;
		}
		
		return Mathf.Clamp(baseCost, 10, 100);
	}

	private int GetMonsterMaxHealth(Node2D monster)
	{
		if (monster != null && monster.HasMethod("GetMaxHP"))
			return (int)monster.Call("GetMaxHP");
		return 1;
	}

	private int GetMonsterHealth(Node2D monster)
	{
		if (monster != null && monster.HasMethod("GetHP"))
			return (int)monster.Call("GetHP");
		return 0;
	}

	private void SetMonsterHealth(Node2D monster, int hp)
	{
		if (monster != null && monster.HasMethod("SetHP"))
			monster.Call("SetHP", hp);
	}

	private void PossessTarget(Node2D target)
	{
		if (target == null) return;

		if (!target.HasMethod("CanBePossessed") || !(bool)target.Call("CanBePossessed"))
		{
			GD.Print($"{target.Name} cannot be possessed right now!");
			return;
		}

		// CẬP NHẬT QUAN TRỌNG: Thiết lập hướng của enemy theo hướng của player
		if (target.HasMethod("UpdateFacing"))
		{
			float direction = facingRight ? 1f : -1f;
			target.Call("UpdateFacing", direction);
		}
		else
		{
			// Fallback: Cập nhật trực tiếp nếu enemy không có method UpdateFacing
			var sprite = target.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (sprite != null)
			{
				sprite.FlipH = !facingRight;
			}
		}

		int hostMaxHp = GetMonsterMaxHealth(target);
		float playerRatio = (float)CurSoulHP / MaxSoulHP;
		int hostHp = Mathf.CeilToInt(hostMaxHp * playerRatio);
		SetMonsterHealth(target, hostHp);

		possessedEnemy = target;
		isPossessing = true;
		_currentBody = target;

		// Chuyển đổi collision và group
		if (target is CharacterBody2D targetBody)
		{
			// Lưu lại collision layer/mask gốc của quái
			if (target.HasMethod("GetOriginalCollisionLayer"))
				target.Call("SetOriginalCollisionLayer", targetBody.CollisionLayer, targetBody.CollisionMask);
			
			// Thiết lập collision layer/mask của player cho quái
			targetBody.CollisionLayer = originalCollisionLayer;
			targetBody.CollisionMask = originalCollisionMask;
		}

		// Chuyển group từ enemy sang player
		if (target.IsInGroup("Enemies"))
		{
			target.RemoveFromGroup("Enemies");
			target.AddToGroup("Player");
		}

		// Tắt AI và bật điều khiển bởi người chơi
		if (target.HasMethod("SetIsControlledByPlayer"))
			target.Call("SetIsControlledByPlayer", true);

		// Ẩn player và tắt collision
		Visible = false;
		if (playerCollision != null) playerCollision.Disabled = true;

		GD.Print($"👹 Nhập xác {target.Name}: {hostHp}/{hostMaxHp} HP (theo {playerRatio:P0} máu Player)");
	}

	private void ReleaseTarget()
	{
		if (!isPossessing || possessedEnemy == null) return;

		int hostMaxHp = GetMonsterMaxHealth(possessedEnemy);
		int hostHp = GetMonsterHealth(possessedEnemy);
		float hostRatio = (float)hostHp / hostMaxHp;
		CurSoulHP = Mathf.Max(1, (int)(MaxSoulHP * hostRatio));

		// Khôi phục collision và group của quái
		if (possessedEnemy is CharacterBody2D targetBody)
		{
			// Khôi phục collision layer/mask gốc
			if (possessedEnemy.HasMethod("GetOriginalCollisionLayer"))
			{
				var originalLayer = (uint)possessedEnemy.Call("GetOriginalCollisionLayer");
				var originalMask = (uint)possessedEnemy.Call("GetOriginalCollisionMask");
				targetBody.CollisionLayer = originalLayer;
				targetBody.CollisionMask = originalMask;
			}
			else
			{
				// Mặc định nếu không có thông tin gốc
				targetBody.CollisionLayer = 1u << 1; // Enemy layer
				targetBody.CollisionMask = (1u << 0) | (1u << 2); // Player và World
			}
		}

		// Khôi phục group
		if (possessedEnemy.IsInGroup("Player"))
		{
			possessedEnemy.RemoveFromGroup("Player");
			possessedEnemy.AddToGroup("Enemies");
		}

		// Tắt điều khiển bởi người chơi, bật lại AI
		if (possessedEnemy.HasMethod("SetIsControlledByPlayer"))
			possessedEnemy.Call("SetIsControlledByPlayer", false);

		// Thu thập xác nếu nó chết
		if (hostHp <= 0 && possessedEnemy != summonedMonster)
		{
			CollectMonster(possessedEnemy);
		}

		// Khôi phục player
		isPossessing = false;
		_currentBody = this;

		if (playerCollision != null) playerCollision.Disabled = false;
		Visible = true;
		SetPhysicsProcess(true);
		SetProcessInput(true);

		Position = possessedEnemy.Position + new Vector2(0, -20);
		possessedEnemy = null;

		GD.Print($"👻 Thoát xác: Player còn {CurSoulHP}/{MaxSoulHP} HP");
	}

	public void OnMonsterDeath(Node2D deadMonster)
	{
		if (summonedMonster == deadMonster)
		{
			if (isPossessing)
			{
				isPossessing = false;
				_currentBody = this;
				if (playerCollision != null) playerCollision.Disabled = false;
				Visible = true;
				SetPhysicsProcess(true);
				SetProcessInput(true);
				Position = deadMonster.Position + new Vector2(0, -20);
				GD.Print("Xác chết: Người chơi thoát xác, giữ máu hiện tại");
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

		var sprite = _currentBody.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null)
		{
			facingRight = direction > 0;
			sprite.FlipH = !facingRight;
			sprite.Play("dash");
		}

		originalCollisionLayer = CollisionLayer;
		originalCollisionMask = CollisionMask;

		CollisionLayer = 1u << 3;
		CollisionMask = 1u << 2;

		if (_currentBody is CharacterBody2D body)
		{
			float dashSpd = _currentBody.Get("DashSpeed").AsSingle() is float d ? d : DashSpeed;
			body.Velocity = new Vector2(direction * dashSpd, 0);
		}
		else
		{
			Velocity = new Vector2(direction * DashSpeed, 0);
		}

		GD.Print("Dash started");
	}

	private void EndDash()
	{
		isDashing = false;
		CollisionLayer = originalCollisionLayer;
		CollisionMask = originalCollisionMask;

		if (_currentBody is CharacterBody2D body)
		{
			body.Velocity = new Vector2(0, body.Velocity.Y);
			body.MoveAndSlide();
		}
		else
		{
			Velocity = new Vector2(0, Velocity.Y);
			MoveAndSlide();
		}
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

		if (isPossessing && possessedEnemy != null)
		{
			int hostMaxHp = GetMonsterMaxHealth(possessedEnemy);
			int hostHp = GetMonsterHealth(possessedEnemy);
			hostHp = Mathf.Max(0, hostHp - damage);
			SetMonsterHealth(possessedEnemy, hostHp);

			float hostRatio = (float)hostHp / hostMaxHp;
			CurSoulHP = (int)(MaxSoulHP * hostRatio);

			if (CurSoulHP <= 0)
			{
				SetMonsterHealth(possessedEnemy, 0);
				ReleaseTarget();
				CurSoulHP = 1;
				GD.Print("⚔️ Chí tử: Xác hy sinh, Player thoát xác với 1 HP!");
			}
			else if (hostHp <= 0)
			{
				ReleaseTarget();
				GD.Print("☠️ Xác chết trước, Player thoát xác.");
			}
		}
		else
		{
			CurSoulHP = Mathf.Max(0, CurSoulHP - damage);
			if (CurSoulHP <= 0)
			{
				GD.Print("💀 Người chơi chết!");
				QueueFree();
			}
		}
	}

	public void Heal(int amount)
	{
		CurSoulHP = Mathf.Min(MaxSoulHP, CurSoulHP + amount);
	}
	
	private void ActivateStealth()
	{
		isStealth = true;
		stealthTimer = StealthDuration;
		stealthCooldownTimer = StealthCooldown;
		CurSoulMP -= 30;
		
		// Làm mờ nhân vật
		Modulate = new Color(1, 1, 1, 0.5f);
		
		// Ẩn khỏi enemy detection
		RemoveFromGroup("Player");
		AddToGroup("StealthPlayer");
		
		GD.Print("Tàng hình kích hoạt!");
	}

	private void EndStealth()
	{
		isStealth = false;
		Modulate = new Color(1, 1, 1, 1);
		
		// Hiện lại với enemy
		RemoveFromGroup("StealthPlayer");
		AddToGroup("Player");
		
		GD.Print("Tàng hình kết thúc!");
	}

	private void ActivateBlock()
	{
		isBlocking = true;
		blockTimer = BlockDuration;
		blockCooldownTimer = BlockCooldown;
		CurSoulMP -= 20;
		
		// Giảm sát thương nhận vào
		GD.Print("Đỡ đòn kích hoạt!");
	}

	private void EndBlock()
	{
		isBlocking = false;
		GD.Print("Đỡ đòn kết thúc!");
	}

	private void PerformDoubleJump()
	{
		float jumpVel = _currentBody.Get("JumpVelocity").AsSingle() is float j ? j : JumpVelocity;
		Velocity = new Vector2(Velocity.X, jumpVel * 0.8f);
		currentJumps--;
		GD.Print($"Double jump! Còn {currentJumps}/{MaxJumps} lần nhảy");
	}

	private void PerformAirDash()
	{
		dashDirection = Input.GetAxis("move_left", "move_right");
		if (dashDirection != 0)
		{
			CurSoulMP = Mathf.Max(0, CurSoulMP - 15);
			currentDashes--;
			StartDash(dashDirection);
			GD.Print($"Dash trên không! Còn {currentDashes}/{MaxDashes} lần dash");
		}
	}
	
	public bool IsPossessing() => isPossessing;
	public bool HasSummonedMonster() => summonedMonster != null;
	public int GetSelectedMonsterSlot() => selectedMonsterSlot;
	public void SetSelectedMonsterSlot(int slot) => selectedMonsterSlot = slot;
	
	public bool IsSlotUnlocked(int slotIndex)
	{
		if (slotIndex >= 0 && slotIndex < UnlockedSlots.Length)
		{
			return UnlockedSlots[slotIndex] == 1;
		}
		return false;
	}
	
	private void ProcessPossessedEnemyPhysics(CharacterBody2D enemyBody, double delta)
	{
		float deltaF = (float)delta;
		Vector2 velocity = enemyBody.Velocity;
		
		// Áp dụng trọng lực
		if (!enemyBody.IsOnFloor())
			velocity.Y += Gravity * deltaF;
		
		// Xử lý di chuyển theo input
		float direction = Input.GetAxis("move_left", "move_right");
		if (!isDashing) // Nếu không đang dash
		{
			float speed = enemyBody.Get("Speed").AsSingle() is float s ? s : Speed;
			velocity.X = Mathf.MoveToward(velocity.X, direction * speed, speed);
		}
		
		// Xử lý nhảy
		if (Input.IsActionJustPressed("move_up") && enemyBody.IsOnFloor())
		{
			float jumpVel = enemyBody.Get("JumpVelocity").AsSingle() is float j ? j : JumpVelocity;
			velocity.Y = jumpVel;
		}
		
		// Xử lý attack - QUAN TRỌNG: Kiểm tra input attack ở đây
		if (Input.IsActionJustPressed("attack") && !isDashing)
		{
			GD.Print($"Attempting attack with {_currentBody.Name}");
			
			// Đảm bảo cập nhật hướng nhìn trước khi tấn công
			if (direction != 0)
			{
				facingRight = direction > 0;
				if (_currentBody.HasMethod("UpdateFacing"))
				{
					_currentBody.Call("UpdateFacing", direction);
				}
			}
			
			if (_currentBody.HasMethod("CanAttack") && (bool)_currentBody.Call("CanAttack"))
			{
				_currentBody.Call("PerformAttack");
			}
		}
		
		// Cập nhật velocity và di chuyển
		enemyBody.Velocity = velocity;
		enemyBody.MoveAndSlide();
		
		// Cập nhật animation và hướng nhìn - QUAN TRỌNG
		var animSprite = enemyBody.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animSprite != null)
		{
			// CHỈ thay đổi animation nếu không đang attack
			string currentAnim = animSprite.Animation.ToString();
			if (!currentAnim.Contains("attack") && !currentAnim.Contains("heavy") && !currentAnim.Contains("normal"))
			{
				string anim = GetEnemyCurrentAnimation(enemyBody.IsOnFloor(), velocity);
				if (animSprite.Animation != anim)
				{
					animSprite.Play(anim);
				}
			}
			
			// Cập nhật hướng nhìn
			if (direction != 0)
			{
				bool newFacingRight = direction > 0;
				if (newFacingRight != facingRight)
				{
					facingRight = newFacingRight;
					animSprite.FlipH = !facingRight;
					
					// Cập nhật hướng tấn công
					if (enemyBody.HasMethod("UpdateAttackAreaFacing"))
					{
						enemyBody.Call("UpdateAttackAreaFacing");
					}
				}
			}
		}
	}

	private string GetEnemyCurrentAnimation(bool onFloor, Vector2 velocity)
	{
		if (isDashing) return "dash";
		
		// QUAN TRỌNG: Ưu tiên animation attack
		var animSprite = _currentBody.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animSprite != null)
		{
			string currentAnim = animSprite.Animation.ToString();
			if (currentAnim.Contains("attack") || currentAnim.Contains("heavy") || currentAnim.Contains("normal"))
			{
				return currentAnim;
			}
		}
		
		if (onFloor) return Mathf.Abs(velocity.X) > 50 ? "run" : "idle";
		if (velocity.Y < -50) return "jump";
		return "fall";
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
