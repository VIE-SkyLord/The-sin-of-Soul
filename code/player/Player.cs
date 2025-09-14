using Godot;
using System;
using System.IO;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	// Player stats - unchanged from original
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

	// Possession system
	private bool isPossessing = false;
	private BaseEnemy possessedEnemy = null; // Changed to BaseEnemy type
	public Node2D _currentBody;

	// ADDED: Store player's facing direction separately
	private bool playerFacingRight = true;

	// Summoning system
	[Export] public PackedScene[] MonsterScenes = new PackedScene[6];
	[Export] public int[] SummonCosts = new int[6];
	[Export] public int[] UnlockedSlots = new int[6] { 1, 1, 0, 0, 0, 0 };
	public int selectedMonsterSlot = 0;
	private Node2D summonedMonster = null;

	// Components and state variables (unchanged)
	private static readonly string LogFilePath = Path.Combine(OS.GetUserDataDir(), "player_debug.log");
	private CollisionShape2D playerCollision;
	private AnimatedSprite2D animatedSprite;
	private bool facingRight = true; // This tracks current body's facing

	// Movement and combat state (unchanged)
	public bool isDashing = false;
	private float dashTimer = 0.0f;
	private float dashCooldownTimer = 0.0f;
	private uint originalCollisionLayer;
	private uint originalCollisionMask;
	private float dashDirection;

	// Regeneration and abilities (unchanged)
	private float mpAccumulator = 0.0f;
	private float spAccumulator = 0.0f;
	private float potionCooldownTimer = 0.0f;
	private const float PotionCooldown = 5.0f;
	private List<TextureRect> healthPotions = new List<TextureRect>();

	// Jump system (unchanged)
	private bool isJumping = false;
	private float jumpCooldownTimer = 0.0f;
	private float coyoteTimer = 0.0f;
	private const float COYOTE_TIME = 0.1f;
	private const float JUMP_COOLDOWN = 0.3f;

	// Wall slide and special abilities (unchanged)
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
	
	private bool isDying = false;
	private bool isInvulnerable = false; // To prevent multiple death triggers
	
	private uint _playerOriginalCollisionLayer;
	private uint _playerOriginalCollisionMask;

	private readonly string fullPotionTexturePath = "res://img/GUI/Status/Healing_Potion.png";
	private readonly string emptyPotionTexturePath = "res://img/GUI/Status/Healing_Potion_Used.png";
	
	public bool IsInPossessedBody => isPossessing && possessedEnemy != null;
	public int GetCurrentMaxHP() => isPossessing && possessedEnemy != null ? possessedEnemy.GetMaxHP() : MaxSoulHP;
	public int GetCurrentHP() => isPossessing && possessedEnemy != null ? possessedEnemy.GetHP() : CurSoulHP;

	public override void _Ready()
	{
		// Initialize player stats and components (unchanged)
		CurSoulHP = MaxSoulHP;
		CurSoulMP = MaxSoulMP;
		CurSoulSP = MaxSoulSP;
		currentJumps = MaxJumps;
		currentDashes = MaxDashes;
		_currentBody = this;
		playerCollision = GetNode<CollisionShape2D>("CollisionShape2D");
		_playerOriginalCollisionLayer = CollisionLayer;
		_playerOriginalCollisionMask = CollisionMask;

		AddToGroup("Player");

		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		if (animatedSprite == null) Log("Warning: AnimatedSprite2D not found!");
		else animatedSprite.Play("idle");

		if (playerCollision == null) Log("Warning: CollisionShape2D not found!");

		// Setup collision layers
		CollisionLayer = 1u << 0;
		CollisionMask = (1u << 1) | (1u << 2);
		originalCollisionLayer = CollisionLayer;
		originalCollisionMask = CollisionMask;

		// Physics settings (unchanged)
		WallMinSlideAngle = Mathf.DegToRad(46);
		FloorBlockOnWall = false;
		SafeMargin = 0.02f;
		FloorStopOnSlope = true;

		// ADDED: Initialize player facing direction
		playerFacingRight = facingRight;

		GD.Print($"Player setup - Layer: {CollisionLayer}, Mask: {CollisionMask}");
		SetProcessInput(true);

		// Initialize health potions
		foreach (Node node in GetTree().GetNodesInGroup("HealthPotion"))
		{
			if (node is TextureRect textureRect)
			{
				healthPotions.Add(textureRect);
			}
		}

		// Setup default monster
		if (MonsterScenes[0] == null)
		{
			MonsterScenes[0] = GD.Load<PackedScene>("res://scenes/Skeleton.tscn");
			SummonCosts[0] = 30;
			if (MonsterScenes[0] == null) GD.PrintErr("Cannot load Skeleton.tscn!");
		}
		
		InitializeGameManager();
		// ADDED: Ensure player starts in a safe position
		CallDeferred(nameof(ValidateStartPosition));
	}
	
	private void InitializeGameManager()
	{
		// Khởi tạo GameManager nếu chưa có
		GameManager.Initialize(GetTree().Root);
		
		// Load player data nếu có
		if (GameManager.Instance != null)
		{
			var stats = GameManager.Instance.GetPlayerStats();
			if (stats.Count > 0)
			{
				CurSoulHP = stats["soul_hp"];
				CurSoulMP = stats["soul_mp"];
				CurSoulSP = stats["soul_sp"];
				MaxSoulHP = stats["max_soul_hp"];
				MaxSoulMP = stats["max_soul_mp"];
				MaxSoulSP = stats["max_soul_sp"];
			}

			// Load unlocked monsters
			UnlockedSlots = GameManager.Instance.GetUnlockedMonsters();
			GD.Print("Player data loaded from GameManager");
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Possession system
		if (@event.IsActionPressed("possess_release"))
		{
			GD.Print("Possess/release action pressed, isPossessing: " + isPossessing);
			if (!isPossessing)
			{
				BaseEnemy nearestTarget = FindNearestPossessTarget();
				if (nearestTarget != null) PossessTarget(nearestTarget);
			}
			else
			{
				ReleaseTarget();
			}
		}

		// Dash system (unchanged)
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

		// Potion system (unchanged)
		if (@event.IsActionPressed("use_potion") && potionCooldownTimer <= 0 && healthPotions.Count > 0)
		{
			GD.Print("Used HP potion");
			UseHealthPotion();
		}

		// Debug commands (unchanged)
		if (@event.IsActionPressed("take_damage"))
		{
			TakeDamage(20);
		}

		if (@event.IsActionPressed("heal"))
		{
			Heal(30);
		}

		// UPDATED: Attack system for possessed enemies
		if (@event.IsActionPressed("normal_attack") && isPossessing && _currentBody != null)
		{
			GD.Print($"Normal attack pressed - isPossessing: {isPossessing}, _currentBody: {_currentBody.Name}");
			HandlePossessedAttack(false);
		}

		if (@event.IsActionPressed("special_attack") && isPossessing && _currentBody != null)
		{
			GD.Print($"Special attack pressed - isPossessing: {isPossessing}, _currentBody: {_currentBody.Name}");
			HandlePossessedAttack(true);
		}
		
		// Special abilities (unchanged)
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
		if (isDying)
		{
			return;
		}	
		
		// CRITICAL: Add safety checks for possessed enemy state
		if (isPossessing && (possessedEnemy == null || possessedEnemy.IsQueuedForDeletion()))
		{
			GD.Print("PhysicsProcess: Possessed enemy is null or disposed - emergency cleanup");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
		}
		
		Vector2 velocity = Vector2.Zero;
		
		// Safe velocity access with try-catch for disposed objects
		try
		{
			velocity = _currentBody is CharacterBody2D body ? body.Velocity : Velocity;
		}
		catch (System.ObjectDisposedException)
		{
			GD.Print("PhysicsProcess: Current body is disposed - switching back to player");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
			velocity = Velocity;
		}
		
		float deltaF = (float)delta;

		// Update timers (unchanged)
		if (dashCooldownTimer > 0) dashCooldownTimer -= deltaF;
		if (jumpCooldownTimer > 0) jumpCooldownTimer -= deltaF;
		if (potionCooldownTimer > 0) potionCooldownTimer -= deltaF;
		
		// Handle possessed enemy physics
		if (isPossessing && possessedEnemy != null)
		{
			ProcessPossessedEnemyPhysics(possessedEnemy, delta);
			return;
		}

		// Regular player physics (unchanged from original)
		if (isDashing)
		{
			dashTimer -= deltaF;
			if (dashTimer <= 0) EndDash();
		}

		// Resource regeneration (unchanged)
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

		// Physics processing (unchanged from original - keeping all the movement logic)
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

			// UPDATED: Update facing direction and store player facing
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
						
						// ADDED: Store player's facing direction when controlling main body
						if (!isPossessing)
						{
							playerFacingRight = facingRight;
						}
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

		// Safe movement with try-catch
		try
		{
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
		}
		catch (System.ObjectDisposedException)
		{
			GD.Print("PhysicsProcess: Movement failed due to disposed body - switching to player");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
			Velocity = velocity;
			MoveAndSlide();
		}

		// Update health from summoned monster
		if (isPossessing && summonedMonster != null)
		{
			CurSoulHP = GetMonsterHealth(summonedMonster);
		}

		// Animation system (unchanged)
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

	#region Possession System - UPDATED for BaseEnemy

	private BaseEnemy FindNearestPossessTarget()
	{
		BaseEnemy nearest = null;
		float nearestDistance = 320.0f;
		
		GD.Print("Finding nearest possess target...");
		
		// Check summoned monster first
		if (summonedMonster is BaseEnemy summonedEnemy && !summonedMonster.IsQueuedForDeletion())
		{
			float distance = Position.DistanceTo(summonedMonster.Position);
			GD.Print($"Summoned monster: {summonedMonster.Name}, distance: {distance}");
			
			// ADDED: Bypass health check for summoned monsters
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = summonedEnemy;
				GD.Print($"Selected summoned monster: {nearest.Name}");
			}
		}

		// Check all enemies
		foreach (Node node in GetTree().GetNodesInGroup("Enemies"))
		{
			if (node is BaseEnemy enemy && !enemy.IsQueuedForDeletion())
			{
				float distance = Position.DistanceTo(enemy.Position);
				bool canPossess = CanPossessEnemy(enemy);
				
				GD.Print($"Enemy: {enemy.Name}, distance: {distance}, canPossess: {canPossess}");
				
				if (canPossess && distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = enemy;
					GD.Print($"Selected enemy: {nearest.Name}");
				}
			}
		}

		if (nearest == null)
		{
			GD.Print("No suitable target found within range!");
		}
		else
		{
			GD.Print($"Target selected: {nearest.Name}, distance: {nearestDistance}");
		}
		
		return nearest;
	}
	
	private bool CanPossessEnemy(BaseEnemy enemy)
	{
		// Always allow possession of summoned monsters - bypass all checks
		if (enemy == summonedMonster) return true;
		
		// Allow possession of dead enemies
		if (enemy.IsDead()) return true;
		
		// For regular enemies, check possession threshold
		return enemy.CanBePossessed();
	}

	private void PossessTarget(BaseEnemy target)
	{
		if (target == null || (target != summonedMonster && !target.CanBePossessed()))
		{
			GD.Print($"{target?.Name ?? "Target"} cannot be possessed right now!");
			return;
		}

		// UPDATED: Store current player facing before possessing
		playerFacingRight = facingRight;

		// Update facing direction to match player
		if (target.HasMethod("UpdateFacing"))
		{
			float direction = playerFacingRight ? 1f : -1f;
			target.Call("UpdateFacing", direction);
		}
		else
		{
			var sprite = target.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (sprite != null)
			{
				sprite.FlipH = !playerFacingRight;
			}
		}

		// Update current body facing to match
		facingRight = playerFacingRight;

		// Set enemy health based on player health ratio
		int hostMaxHp = target.GetMaxHP();
		float playerRatio = (float)CurSoulHP / MaxSoulHP;
		int hostHp = Mathf.CeilToInt(hostMaxHp * playerRatio);
		target.SetHP(hostHp);

		possessedEnemy = target;
		isPossessing = true;
		_currentBody = target;

		// FIXED: Proper collision and group management
		if (target is CharacterBody2D targetBody)
		{
			// Store original settings
			target.SetOriginalCollisionLayer(targetBody.CollisionLayer, targetBody.CollisionMask);
			
			// Set to player collision settings
			targetBody.CollisionLayer = originalCollisionLayer; // Player layer (bit 0)
			targetBody.CollisionMask = originalCollisionMask;   // Can interact with environment
		}

		// FIXED: Clear enemy from all enemy-related groups first
		if (target.IsInGroup("Enemies"))
		{
			target.RemoveFromGroup("Enemies");
		}
		
		// Add to player-controlled groups
		target.AddToGroup("Player");
		target.AddToGroup("PlayerControlled"); // New group for possessed entities
		
		// FIXED: Special handling for summoned monsters
		if (target == summonedMonster)
		{
			// Summoned monsters keep their ally status but are now player-controlled
			if (!target.IsInGroup("Allies")) target.AddToGroup("Allies");
			if (!target.IsInGroup("SummonedMonsters")) target.AddToGroup("SummonedMonsters");
		}

		// Set player control
		target.SetIsControlledByPlayer(true);

		// Hide player and disable collision
		Visible = false;
		if (playerCollision != null) playerCollision.Disabled = true;

		// NEW: Force all enemies to reassess their targets after possession
		ForceAllEnemiesRetarget();

		GD.Print($"Possessed {target.Name}: {hostHp}/{hostMaxHp} HP ({playerRatio:P0} of player health)");
		GD.Print($"Groups after possession: {string.Join(", ", target.GetGroups())}");
	}

	private void ReleaseTarget()
	{
		if (!isPossessing || possessedEnemy == null) return;

		// Store info needed for deferred release
		var releaseData = new ReleaseData
		{
			HostMaxHp = possessedEnemy.GetMaxHP(),
			HostHp = possessedEnemy.GetHP(),
			WasSummonedMonster = possessedEnemy == summonedMonster,
			EnemyPosition = possessedEnemy.Position,
			Enemy = possessedEnemy
		};

		// Calculate new player health
		float hostRatio = (float)releaseData.HostHp / releaseData.HostMaxHp;
		CurSoulHP = Mathf.Max(1, (int)(MaxSoulHP * hostRatio));
		
		CollisionLayer = _playerOriginalCollisionLayer;
		CollisionMask = _playerOriginalCollisionMask;
		if (playerCollision != null) 
		{
			playerCollision.Disabled = false;
		}
		
		Vector2 safePosition = FindSafeReleasePosition(possessedEnemy.Position);
		Position = safePosition;
		Velocity = Vector2.Zero;
		ForceUpdateTransform();

		// Clear possession state immediately
		isPossessing = false;
		_currentBody = this;
		possessedEnemy = null;

		// FIXED: Use deferred call to handle collision restoration
		CallDeferred(nameof(DeferredReleaseRestore), 
		releaseData.HostMaxHp, 
		releaseData.HostHp, 
		releaseData.WasSummonedMonster,
		releaseData.EnemyPosition,
		releaseData.Enemy);

		GD.Print($"Release initiated - will complete restoration via deferred call");
	}
	
	// NEW: Data structure for deferred release
	private struct ReleaseData
	{
		public int HostMaxHp;
		public int HostHp;
		public bool WasSummonedMonster;
		public Vector2 EnemyPosition;
		public BaseEnemy Enemy;
	}

	// NEW: Deferred release restoration
	private void DeferredReleaseRestore(int hostMaxHp, int hostHp, bool wasSummonedMonster, Vector2 enemyPosition, BaseEnemy enemy)
	{
		// Handle group and collision restoration
		if (enemy != null && !enemy.IsQueuedForDeletion())
		{
			// Remove from player groups
			if (enemy.IsInGroup("Player"))
				enemy.RemoveFromGroup("Player");
			if (enemy.IsInGroup("PlayerControlled"))
				enemy.RemoveFromGroup("PlayerControlled");

			// Restore based on enemy type
			if (enemy is CharacterBody2D targetBody)
			{
				if (!wasSummonedMonster)
				{
					// Regular enemies: restore original collision
					uint originalLayer = enemy.GetOriginalCollisionLayer();
					uint originalMask = enemy.GetOriginalCollisionMask();
					targetBody.CollisionLayer = originalLayer;
					targetBody.CollisionMask = originalMask;
					
					enemy.AddToGroup("Enemies");
					enemy.SetIsControlledByPlayer(false);
				}
				else
				{
					// Summoned monsters: keep ally settings
					targetBody.CollisionLayer = 1u << 5; // Ally layer
					targetBody.CollisionMask = (1u << 1) | (1u << 2); // enemies + world
					
					enemy.SetIsControlledByPlayer(true);
					if (!enemy.IsInGroup("Allies")) enemy.AddToGroup("Allies");
					if (!enemy.IsInGroup("SummonedMonsters")) enemy.AddToGroup("SummonedMonsters");
				}
			}

			// Collect dead enemy if not summoned
			if (hostHp <= 0 && !wasSummonedMonster)
			{
				CollectMonster(enemy);
			}

			enemy.ResetAttackState();
		}

		// Restore player state safely
		Visible = true;
		
		// FIXED: Safe collision restoration
		if (playerCollision != null && playerCollision.Disabled)
		{
			playerCollision.Disabled = false;
		}
		
		SetPhysicsProcess(true);
		SetProcessInput(true);

		// Restore facing
		facingRight = playerFacingRight;
		if (animatedSprite != null)
		{
			animatedSprite.FlipH = !facingRight;
		}

		// Find and move to safe position
		Vector2 safePosition = FindSafeReleasePosition(enemyPosition);
		Position = safePosition;
		Velocity = Vector2.Zero;

		ForceAllEnemiesRetarget();

		GD.Print($"Released possession: Player has {CurSoulHP}/{MaxSoulHP} HP at {safePosition}");
	}

	private void ProcessPossessedEnemyPhysics(BaseEnemy enemyBody, double delta)
	{
		// CRITICAL: Add null and disposal checks first
		if (!isPossessing || enemyBody == null || enemyBody.IsQueuedForDeletion())
		{
			GD.Print("ProcessPossessedEnemyPhysics: Enemy is null or disposed - ending possession");
			if (isPossessing)
			{
				// Emergency cleanup
				isPossessing = false;
				possessedEnemy = null;
				_currentBody = this;
			}
			return;
		}
		
		if (!(enemyBody is CharacterBody2D body))
		{
			GD.Print("ProcessPossessedEnemyPhysics: Enemy is not CharacterBody2D");
			return;
		}
		
		// Additional safety check - if the body is disposed, exit immediately
		try
		{
			// Test access to ensure object isn't disposed
			var testVelocity = body.Velocity;
		}
		catch (System.ObjectDisposedException)
		{
			GD.Print("ProcessPossessedEnemyPhysics: CharacterBody2D is disposed - emergency release");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
			return;
		}
		
		float deltaF = (float)delta;
		Vector2 velocity = body.Velocity;
		
		// Update timers for player-controlled enemy
		if (enemyBody.HasMethod("UpdateTimersPlayerControlled"))
		{
			enemyBody.Call("UpdateTimersPlayerControlled", deltaF);
		}
		
		// Check if movement is locked during attack
		bool isMovementLocked = enemyBody.Get("_isMovementLocked").AsBool();
		bool isAttacking = enemyBody.IsAttacking();
		
		// FIXED: Get enemy properties using proper field access
		float enemySpeed;
		float enemyJumpVelocity;
		float enemyGravity;

		// Try to get values from exported fields first
		try
		{
			enemySpeed = enemyBody.Get("Speed").AsSingle();
		}
		catch
		{
			// Fallback: try common field names or use reflection
			enemySpeed = GetEnemySpeed(enemyBody);
		}

		try
		{
			enemyJumpVelocity = enemyBody.Get("JumpVelocity").AsSingle();
		}
		catch
		{
			enemyJumpVelocity = GetEnemyJumpVelocity(enemyBody);
		}

		try
		{
			enemyGravity = enemyBody.Get("Gravity").AsSingle();
		}
		catch
		{
			enemyGravity = GetEnemyGravity(enemyBody);
		}

		GD.Print($"Using enemy properties - Speed: {enemySpeed}, Jump: {enemyJumpVelocity}, Gravity: {enemyGravity}");
			
		// Apply gravity
		if (!body.IsOnFloor())
			velocity.Y += enemyGravity * deltaF;

		// Handle movement input
		if (!isAttacking && !isDashing && !isMovementLocked)
		{
			float direction = Input.GetAxis("move_left", "move_right");
			velocity.X = Mathf.MoveToward(velocity.X, direction * enemySpeed, enemySpeed);
			
			// UPDATED: Update facing direction and store it for later restoration
			if (direction != 0)
			{
				bool newFacingRight = direction > 0;
				if (newFacingRight != facingRight)
				{
					facingRight = newFacingRight;
					playerFacingRight = facingRight; // Update player's stored facing
					
					var sprite = body.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
					if (sprite != null)
					{
						sprite.FlipH = !facingRight;
					}
					
					// Update attack area facing with enhanced method
					if (enemyBody.HasMethod("UpdateFacing"))
						enemyBody.Call("UpdateFacing", direction);
					if (enemyBody.HasMethod("UpdateNormalAttackAreaFacing"))
						enemyBody.Call("UpdateNormalAttackAreaFacing");
					if (enemyBody.HasMethod("UpdateSpecialAttackAreaFacing"))
						enemyBody.Call("UpdateSpecialAttackAreaFacing");
				}
			}
		}
		else if (isMovementLocked)
		{
			// Gradually slow down horizontal movement when locked
			velocity.X = Mathf.MoveToward(velocity.X, 0, 500 * deltaF);
		}
		
		if (Input.IsActionJustPressed("move_up") && body.IsOnFloor() && !isAttacking && !isMovementLocked)
		{
			velocity.Y = enemyJumpVelocity;
		}
		
		// Apply velocity and move - with additional safety check
		try
		{
			body.Velocity = velocity;
			body.MoveAndSlide();
		}
		catch (System.ObjectDisposedException)
		{
			GD.Print("ProcessPossessedEnemyPhysics: Body disposed during physics update");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
			return;
		}
		
		// Update health from summoned monster
		if (isPossessing && summonedMonster != null)
		{
			CurSoulHP = GetMonsterHealth(summonedMonster);
		}
		
		// Update animation for possessed enemy
		var animSprite = body.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (animSprite != null)
		{
			string anim = GetEnemyCurrentAnimation(body.IsOnFloor(), velocity, enemyBody.CurrentState);
			if (animSprite.Animation != anim && !isAttacking)
			{
				animSprite.Play(anim);
			}
		}
	}
	
	private float GetEnemySpeed(BaseEnemy enemy)
	{
		// Try different common field names
		try
		{
			if (enemy.HasMethod("GetSpeed"))
				return (float)enemy.Call("GetSpeed");
				
			// Try accessing exported properties
			var speedProp = enemy.Get("speed");
			if (speedProp.VariantType != Variant.Type.Nil)
				return speedProp.AsSingle();
				
			speedProp = enemy.Get("moveSpeed");
			if (speedProp.VariantType != Variant.Type.Nil)
				return speedProp.AsSingle();
				
			speedProp = enemy.Get("Speed");
			if (speedProp.VariantType != Variant.Type.Nil)
				return speedProp.AsSingle();
		}
		catch (Exception ex)
		{
			GD.Print($"Error getting enemy speed: {ex.Message}");
		}
		
		// If enemy is based on the same system as player, use similar values but varied
		return Speed * 0.8f; // 80% of player speed as fallback
	}
	
	private float GetEnemyJumpVelocity(BaseEnemy enemy)
	{
		try
		{
			if (enemy.HasMethod("GetJumpVelocity"))
				return (float)enemy.Call("GetJumpVelocity");
				
			var jumpProp = enemy.Get("jumpVelocity");
			if (jumpProp.VariantType != Variant.Type.Nil)
				return jumpProp.AsSingle();
				
			jumpProp = enemy.Get("JumpVelocity");
			if (jumpProp.VariantType != Variant.Type.Nil)
				return jumpProp.AsSingle();
				
			jumpProp = enemy.Get("jumpHeight");
			if (jumpProp.VariantType != Variant.Type.Nil)
				return jumpProp.AsSingle();
		}
		catch (Exception ex)
		{
			GD.Print($"Error getting enemy jump velocity: {ex.Message}");
		}
		
		return JumpVelocity * 0.9f; // 90% of player jump as fallback
	}
	
	private float GetEnemyGravity(BaseEnemy enemy)
	{
		try
		{
			if (enemy.HasMethod("GetGravity"))
				return (float)enemy.Call("GetGravity");
				
			var gravityProp = enemy.Get("gravity");
			if (gravityProp.VariantType != Variant.Type.Nil)
				return gravityProp.AsSingle();
				
			gravityProp = enemy.Get("Gravity");
			if (gravityProp.VariantType != Variant.Type.Nil)
				return gravityProp.AsSingle();
		}
		catch (Exception ex)
		{
			GD.Print($"Error getting enemy gravity: {ex.Message}");
		}
		
		return Gravity; // Same gravity as player
	}

	private string GetEnemyCurrentAnimation(bool onFloor, Vector2 velocity, BaseEnemy.EnemyState currentState)
	{
		if (isDashing) return "dash";
		
		// Check if enemy is in an attack state
		if (currentState == BaseEnemy.EnemyState.NormalAttack || 
			currentState == BaseEnemy.EnemyState.SpecialAttack)
		{
			return "attack"; // Use appropriate animation name
		}
		
		if (onFloor) return Mathf.Abs(velocity.X) > 50 ? "run" : "idle";
		if (velocity.Y < -50) return "jump";
		return "fall";
	}
	
	private void HandlePossessedAttack(bool isSpecialAttack)
	{
		if (possessedEnemy == null) 
		{
			GD.Print("No possessed enemy to attack with");
			return;
		}
		
		if (!possessedEnemy.CanAttack()) 
		{
			GD.Print($"Possessed enemy {possessedEnemy.Name} cannot attack right now - CanAttack: {possessedEnemy.CanAttack()}, IsAttacking: {possessedEnemy.IsAttacking()}");
			return;
		}
		
		GD.Print($"Attempting {(isSpecialAttack ? "special" : "normal")} attack with {possessedEnemy.Name}");

		// Update facing direction based on current input or maintain current facing
		float direction = Input.GetAxis("move_left", "move_right");
		if (direction != 0)
		{
			facingRight = direction > 0;
			playerFacingRight = facingRight; // Update stored player facing
			
			if (possessedEnemy.HasMethod("UpdateFacing"))
			{
				possessedEnemy.Call("UpdateFacing", direction);
			}
			else
			{
				// Fallback: update sprite directly
				var sprite = possessedEnemy.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
				if (sprite != null)
				{
					sprite.FlipH = !facingRight;
				}
			}
		}
		
		// Call the appropriate player attack method directly
		if (isSpecialAttack)
		{
			GD.Print("Calling PerformPlayerSpecialAttack");
			possessedEnemy.PerformPlayerSpecialAttack();
		}
		else
		{
			GD.Print("Calling PerformPlayerNormalAttack");  
			possessedEnemy.PerformPlayerNormalAttack();
		}
	}
	
	// Cập nhật OnPossessedEnemyDied để gọi debug
	public void OnPossessedEnemyDied(BaseEnemy dyingEnemy)
	{
		GD.Print("=== POSSESSED ENEMY DIED - EMERGENCY RELEASE ===");
		
		if (isPossessing && possessedEnemy == dyingEnemy)
		{
			GD.Print($"Possessed enemy {dyingEnemy.Name} died - emergency release starting");
			
			// Store critical info before enemy disposal
			Vector2 deathPosition = dyingEnemy.GlobalPosition;
			string enemyName = dyingEnemy.Name;
			bool wasOnFloor = false;
			Vector2 lastVelocity = Vector2.Zero;
			
			try 
			{
				if (dyingEnemy is CharacterBody2D enemyBody)
				{
					wasOnFloor = enemyBody.IsOnFloor();
					lastVelocity = enemyBody.Velocity;
				}
			}
			catch (System.ObjectDisposedException)
			{
				GD.Print("Enemy already disposed during death");
			}
			
			// Set critical health (10% or minimum 1)
			CurSoulHP = Mathf.Max(1, MaxSoulHP / 10);
			
			// Find safe release position using CORRECT collision detection
			Vector2 safePosition = FindCorrectSafePosition(deathPosition);
			
			// Collection and cleanup
			if (dyingEnemy != summonedMonster)
			{
				CollectMonster(dyingEnemy);
			}
			else if (summonedMonster == dyingEnemy)
			{
				summonedMonster = null;
			}
			
			// Clear possession state
			isPossessing = false;
			_currentBody = this;
			possessedEnemy = null;
			
			// FIXED: Use deferred call for physics-sensitive restore
			CallDeferred(nameof(DeferredSafeRestore), safePosition, lastVelocity);
			
			// Force enemy retargeting can be immediate
			ForceAllEnemiesRetarget();
			
			GD.Print($"Emergency release initiated - will complete via deferred call");
		}
	}
	
	private void DeferredSafeRestore(Vector2 targetPosition, Vector2 inheritedVelocity)
	{
		SafeRestorePlayer(targetPosition, inheritedVelocity);
		GD.Print($"Emergency release complete - Player at {GlobalPosition} with {CurSoulHP} HP");
	}
	
	private void SafeRestorePlayer(Vector2 targetPosition, Vector2 inheritedVelocity)
	{
		GD.Print("=== SAFE PLAYER RESTORE (NO PHYSICS INTERRUPTION) ===");
		GD.Print($"Target: {targetPosition}, InheritedVel: {inheritedVelocity}");
		
		// CRITICAL: DO NOT disable physics processing - this causes fall-through!
		// SetPhysicsProcess(false); // REMOVED - this was the main cause!
		
		// Restore player visibility first
		Visible = true;
		
		// FIXED: Use deferred call to change collision state
		if (playerCollision != null)
		{
			CallDeferred(nameof(RestorePlayerCollision));
		}
		
		// CRITICAL: Restore collision layers before position change
		CollisionLayer = originalCollisionLayer;  // Bit 0 (Player)
		CollisionMask = originalCollisionMask;    // Bits 1,2 (Enemy + World)
		
		GD.Print($"Collision restored - Layer: {CollisionLayer}, Mask: {CollisionMask}");
		
		// ENHANCED: Smart position setting with validation
		if (ValidatePositionBeforeSetting(targetPosition))
		{
			GlobalPosition = targetPosition;
			GD.Print($"Position set to validated target: {targetPosition}");
		}
		else
		{
			Vector2 fallbackPos = GetVerifiedEmergencyPosition();
			GlobalPosition = fallbackPos;
			GD.Print($"Target unsafe - using fallback: {fallbackPos}");
		}
		
		// ENHANCED: Intelligent velocity handling
		Vector2 safeVelocity = CalculateSafeVelocity(inheritedVelocity);
		Velocity = safeVelocity;
		GD.Print($"Velocity set to: {safeVelocity}");
		
		// Restore facing and animation
		facingRight = playerFacingRight;
		if (animatedSprite != null)
		{
			animatedSprite.FlipH = !facingRight;
			animatedSprite.Play("idle");
		}
		
		// Force collision system update
		ForceUpdateTransform();
		
		// Immediate movement test to verify physics
		MoveAndSlide();
		
		// Re-enable input
		SetProcessInput(true);
		
		// Safety invulnerability
		SetInvulnerable(2.5f);
		
		// Schedule safety validation - also deferred to avoid physics conflicts
		CallDeferred(nameof(ScheduleValidation));
		
		GD.Print($"Safe restore completed - Final pos: {GlobalPosition}, vel: {Velocity}");
	}
	
	private void RestorePlayerCollision()
	{
		if (playerCollision != null && playerCollision.Disabled)
		{
			playerCollision.Disabled = false;
			GD.Print("Player collision restored via deferred call");
		}
	}
	
	private void ScheduleValidation()
	{
		GetTree().CreateTimer(0.1f).Timeout += ValidatePlayerPhysicsWorking;
	}
	
	private void EmergencyTeleportToSafety()
	{
		GD.Print("EXECUTING EMERGENCY TELEPORT TO SAFETY");
		
		Vector2 emergencyPos = GetVerifiedEmergencyPosition();
		
		// Force teleport
		SetPhysicsProcess(false);
		GlobalPosition = emergencyPos;
		Velocity = Vector2.Zero;
		
		ForceUpdateTransform();
		
		// Re-enable physics after teleport
		GetTree().CreateTimer(0.1f).Timeout += () => {
			SetPhysicsProcess(true);
			GD.Print($"Emergency teleport completed to: {GlobalPosition}");
		};
	}
	
	private void ExtractPlayerFromWorld()
	{
		// Try moving player up in small increments until free
		for (int attempts = 0; attempts < 10; attempts++)
		{
			GlobalPosition += Vector2.Up * 10;
			Velocity = Vector2.Zero;
			
			ForceUpdateTransform();
			MoveAndSlide();
			
			bool stillStuck = false;
			var collisionCount = GetSlideCollisionCount();
			
			for (int i = 0; i < collisionCount; i++)
			{
				var collision = GetSlideCollision(i);
				var collider = collision.GetCollider();
				var normal = collision.GetNormal();
				
				// FIXED: Use TileMapLayer instead of TileMap
				if ((collider is StaticBody2D || collider is TileMapLayer) && normal.Y >= -0.5f)
				{
					stillStuck = true;
					break;
				}
			}
			
			if (!stillStuck)
			{
				GD.Print($"Successfully extracted player after {attempts + 1} attempts");
				return;
			}
		}
		
		// If still stuck after extraction attempts, emergency teleport
		GD.Print("Extraction failed - emergency teleport required");
		EmergencyTeleportToSafety();
	}
	
	private void ValidatePlayerPhysicsWorking()
	{
		GD.Print("=== VALIDATING PLAYER PHYSICS POST-RESTORE ===");
		
		Vector2 currentPos = GlobalPosition;
		Vector2 currentVel = Velocity;
		
		// Check for fall-through scenario
		if (currentPos.Y > 2500) // Arbitrary "fell through world" threshold
		{
			GD.Print("CRITICAL: Player fell through world after restore!");
			EmergencyTeleportToSafety();
			return;
		}
		
		// Check if player is stuck in world geometry
		bool stuckInWorld = false;
		var collisionCount = GetSlideCollisionCount();
		
		for (int i = 0; i < collisionCount; i++)
		{
			var collision = GetSlideCollision(i);
			var collider = collision.GetCollider();
			var normal = collision.GetNormal();
			
			// FIXED: Use TileMapLayer instead of TileMap
			if ((collider is StaticBody2D || collider is TileMapLayer) && normal.Y >= -0.5f)
			{
				stuckInWorld = true;
				GD.Print($"Player stuck in world - Normal: {normal}");
				break;
			}
		}
		
		if (stuckInWorld)
		{
			GD.Print("Player stuck in world geometry - attempting extraction");
			ExtractPlayerFromWorld();
		}
		else
		{
			GD.Print($"Physics validation passed - Pos: {currentPos}, Vel: {currentVel}");
		}
	}
	
	private Vector2 CalculateSafeVelocity(Vector2 inheritedVel)
	{
		// If falling very fast, reduce speed to prevent overshooting ground
		if (inheritedVel.Y > 600)
		{
			return new Vector2(0, 400); // Cap falling speed
		}
		
		// If moving upward (jumping), reset to prevent unwanted flight
		if (inheritedVel.Y < -200)
		{
			return new Vector2(0, 0); // Stop upward movement
		}
		
		// If reasonable falling speed, keep Y component but zero X
		if (inheritedVel.Y > 0 && inheritedVel.Y <= 600)
		{
			return new Vector2(0, inheritedVel.Y);
		}
		
		// Default safe state
		return Vector2.Zero;
	}
	
	private bool ValidatePositionBeforeSetting(Vector2 testPosition)
	{
		// Store current state
		Vector2 originalPos = GlobalPosition;
		bool wasVisible = Visible;
		
		// Temporarily test the position
		Visible = false; // Hide during test
		GlobalPosition = testPosition;
		
		// Force a physics update to test collision
		ForceUpdateTransform();
		
		// Check if position causes problems
		bool positionValid = true;
		
		// Test for immediate collision issues
		if (GetSlideCollisionCount() > 0)
		{
			for (int i = 0; i < GetSlideCollisionCount(); i++)
			{
				var collision = GetSlideCollision(i);
				var collider = collision.GetCollider();
				
				// FIXED: Use TileMapLayer instead of TileMap
				if (collider is StaticBody2D || collider is TileMapLayer)
				{
					Vector2 normal = collision.GetNormal();
					// If normal points mostly horizontally or down, we're probably stuck
					if (Mathf.Abs(normal.Y) < 0.7f)
					{
						positionValid = false;
						GD.Print($"Position validation failed: Stuck in world geometry");
						break;
					}
				}
			}
		}
		
		// Restore original state
		GlobalPosition = originalPos;
		Visible = wasVisible;
		
		return positionValid;
	}
	
	private Vector2 FindCorrectSafePosition(Vector2 enemyDeathPos)
	{
		GD.Print($"Finding safe position from death location: {enemyDeathPos}");
		
		const uint WORLD_LAYER_MASK = 1u << 2;  // ONLY World layer is solid terrain
		
		// Priority search offsets - prefer positions above ground
		Vector2[] searchOffsets = {
			new Vector2(0, -60),     // Directly above
			new Vector2(-50, -50),   // Left-above diagonal
			new Vector2(50, -50),    // Right-above diagonal
			new Vector2(-80, -40),   // Further left
			new Vector2(80, -40),    // Further right
			new Vector2(0, -100),    // Higher up
			new Vector2(-100, -60),  // Much further left
			new Vector2(100, -60),   // Much further right
		};

		foreach (Vector2 offset in searchOffsets)
		{
			Vector2 testPos = enemyDeathPos + offset;
			
			if (IsPositionSafeForPlayer(testPos, WORLD_LAYER_MASK))
			{
				GD.Print($"Found safe position at offset {offset}: {testPos}");
				return testPos;
			}
		}

		// If no nearby safe position, search for ground systematically
		Vector2 groundPosition = SearchForNearestGround(enemyDeathPos, WORLD_LAYER_MASK);
		if (groundPosition != Vector2.Zero)
		{
			GD.Print($"Using nearest ground position: {groundPosition}");
			return groundPosition;
		}

		// Last resort: checkpoint or emergency position
		Vector2 emergencyPos = GetVerifiedEmergencyPosition();
		GD.Print($"Using emergency fallback position: {emergencyPos}");
		return emergencyPos;
	}
	
	private Vector2 GetVerifiedEmergencyPosition()
	{
		const uint WORLD_LAYER = 1u << 2;
		
		// Try GameManager checkpoint first
		if (GameManager.Instance?.HasCheckpointData() == true)
		{
			Vector2 checkpointPos = GameManager.Instance.GetLastCheckpointPosition();
			if (checkpointPos != Vector2.Zero && IsPositionSafeForPlayer(checkpointPos, WORLD_LAYER))
			{
				return checkpointPos;
			}
		}
		
		// Try scene checkpoints
		var checkpoints = GetTree().GetNodesInGroup("Checkpoints");
		foreach (Node node in checkpoints)
		{
			if (node is Node2D checkpoint)
			{
				Vector2 checkpointPos = checkpoint.GlobalPosition + Vector2.Up * 50;
				if (IsPositionSafeForPlayer(checkpointPos, WORLD_LAYER))
				{
					return checkpointPos;
				}
			}
		}
		
		// Try spawn points
		var spawns = GetTree().GetNodesInGroup("PlayerSpawn");
		foreach (Node node in spawns)
		{
			if (node is Node2D spawn && IsPositionSafeForPlayer(spawn.GlobalPosition, WORLD_LAYER))
			{
				return spawn.GlobalPosition;
			}
		}
		
		// Final fallback: high altitude position that should be safe
		Vector2 highAltitudePos = new Vector2(0, -300); // Very high up
		GD.Print($"Using high altitude emergency position: {highAltitudePos}");
		return highAltitudePos;
	}
	
	private void DebugCollisionDetails(Godot.Collections.Array<Godot.Collections.Dictionary> pointResults)
	{
		foreach (var result in pointResults)
		{
			if (result.ContainsKey("collider"))
			{
				var collider = result["collider"].AsGodotObject();
				GD.Print($"  Colliding with: {collider?.GetType().Name ?? "Unknown"}");
				
				if (collider is CollisionObject2D colObj)
				{
					GD.Print($"    Collision Layer: {colObj.CollisionLayer} (Binary: {Convert.ToString(colObj.CollisionLayer, 2)})");
				}
			}
		}
	}
	
	private Vector2 SearchForNearestGround(Vector2 startPos, uint worldMask)
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		var excludeRids = new Godot.Collections.Array<Rid> { GetRid() };
		
		// Search in expanding rings, prioritizing upward directions
		for (int radius = 80; radius <= 600; radius += 60)
		{
			// Test angles prioritizing above-ground positions
			int[] angles = { -90, -60, -120, -45, -135, -30, -150, 0, -180, 
							30, 150, 45, 135, 60, 120, 90 };
			
			foreach (int angleDeg in angles)
			{
				float radians = Mathf.DegToRad(angleDeg);
				Vector2 searchPos = startPos + new Vector2(
					Mathf.Cos(radians) * radius,
					Mathf.Sin(radians) * radius
				);

				var rayQuery = PhysicsRayQueryParameters2D.Create(
					searchPos, 
					searchPos + Vector2.Down * 400
				);
				rayQuery.CollisionMask = worldMask;
				rayQuery.Exclude = excludeRids;
				
				var rayResult = spaceState.IntersectRay(rayQuery);
				
				if (rayResult.Count > 0)
				{
					Vector2 hitGround = rayResult["position"].AsVector2();
					Vector2 playerPos = hitGround + Vector2.Up * 40; // 40px above ground
					
					if (IsPositionSafeForPlayer(playerPos, worldMask))
					{
						GD.Print($"Found ground at radius {radius}, angle {angleDeg}: {playerPos}");
						return playerPos;
					}
				}
			}
		}

		GD.Print("No suitable ground found in radius search");
		return Vector2.Zero;
	}
	
	private bool IsPositionSafeForPlayer(Vector2 position, uint worldMask)
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		
		// Exclude player from collision tests
		var excludeRids = new Godot.Collections.Array<Rid> { GetRid() };
		
		// Test 1: Must have world ground below within reasonable distance
		var groundQuery = PhysicsRayQueryParameters2D.Create(
			position, 
			position + Vector2.Down * 250  // Max fall distance to find ground
		);
		groundQuery.CollisionMask = worldMask;  // Only world layer
		groundQuery.Exclude = excludeRids;
		
		var groundResult = spaceState.IntersectRay(groundQuery);
		
		if (groundResult.Count == 0)
		{
			GD.Print($"Position {position} UNSAFE: No world ground found within 250px");
			return false;
		}

		Vector2 groundPos = groundResult["position"].AsVector2();
		float groundDistance = position.DistanceTo(groundPos);
		
		// Ground must be reasonable distance (not too close = inside terrain, not too far = will fall)
		if (groundDistance < 25 || groundDistance > 250)
		{
			GD.Print($"Position {position} UNSAFE: Ground distance {groundDistance} out of range [25-250]");
			return false;
		}

		// Test 2: Position must not be inside world geometry
		var pointQuery = new PhysicsPointQueryParameters2D();
		pointQuery.Position = position;
		pointQuery.CollisionMask = worldMask;  // Only test world layer
		pointQuery.Exclude = excludeRids;
		
		var pointResults = spaceState.IntersectPoint(pointQuery, 5);
		if (pointResults.Count > 0)
		{
			GD.Print($"Position {position} UNSAFE: Inside world geometry");
			DebugCollisionDetails(pointResults);  // Now matches the correct signature
			return false;
		}

		// Test 3: Player clearance space (body size consideration)
		Vector2[] clearanceTests = {
			new Vector2(-18, 0),   // Left edge of player
			new Vector2(18, 0),    // Right edge of player
			new Vector2(0, -35),   // Top of player head
			new Vector2(-18, -35), // Top-left corner
			new Vector2(18, -35),  // Top-right corner
		};
		
		foreach (var clearanceOffset in clearanceTests)
		{
			var clearanceQuery = new PhysicsPointQueryParameters2D();
			clearanceQuery.Position = position + clearanceOffset;
			clearanceQuery.CollisionMask = worldMask;
			clearanceQuery.Exclude = excludeRids;
			
			var clearanceResults = spaceState.IntersectPoint(clearanceQuery, 3);
			if (clearanceResults.Count > 0)
			{
				GD.Print($"Position {position} UNSAFE: No clearance at offset {clearanceOffset}");
				return false;
			}
		}

		GD.Print($"Position {position} SAFE: Ground at {groundPos}, distance {groundDistance:F1}px");
		return true;
	}

	// NEW: Dedicated method for proper player restoration
	private void RestorePlayerAfterEmergencyRelease(Vector2 safePosition)
	{
		// CRITICAL: Disable physics temporarily to prevent falling during setup
		SetPhysicsProcess(false);
		
		// Restore player visibility and collision
		Visible = true;
		if (playerCollision != null)
		{
			playerCollision.Disabled = false;
		}
		
		// Restore player's facing direction
		facingRight = playerFacingRight;
		if (animatedSprite != null)
		{
			animatedSprite.FlipH = !facingRight;
			animatedSprite.Play("idle"); // Ensure proper animation
		}
		
		// CRITICAL: Set position and ensure player is properly grounded
		GlobalPosition = safePosition;
		
		// Reset velocity completely
		Velocity = Vector2.Zero;
		
		// ENHANCED: Force collision detection update
		ForceUpdateTransform();
		
		// CRITICAL: Use deferred call to re-enable physics after collision update
		CallDeferred(nameof(ReEnablePhysicsAfterRelease));
		
		// Re-enable input processing
		SetProcessInput(true);
		
		// Add temporary invulnerability
		SetInvulnerable(2.0f);
		
		GD.Print($"Player restored at position: {GlobalPosition} with velocity: {Velocity}");
	}

	// NEW: Re-enable physics after proper setup
	private void ReEnablePhysicsAfterRelease()
	{
		// Ensure player is still at safe position
		if (!IsSafePosition(GlobalPosition))
		{
			GD.Print("Player position became unsafe after release - correcting...");
			Vector2 emergencyPos = GetEmergencySafePosition();
			GlobalPosition = emergencyPos;
			Velocity = Vector2.Zero;
		}
		
		// CRITICAL: Test collision layers are properly set
		if (CollisionLayer != originalCollisionLayer)
		{
			GD.Print($"Restoring player collision layer from {CollisionLayer} to {originalCollisionLayer}");
			CollisionLayer = originalCollisionLayer;
		}
		
		if (CollisionMask != originalCollisionMask)
		{
			GD.Print($"Restoring player collision mask from {CollisionMask} to {originalCollisionMask}");
			CollisionMask = originalCollisionMask;
		}
		
		// Force a physics update to ensure proper collision detection
		MoveAndSlide();
		
		// Re-enable physics processing
		SetPhysicsProcess(true);
		
		// ENHANCED: Add safety check after a frame
		CallDeferred(nameof(ValidatePlayerAfterRelease));
		
		GD.Print("Physics re-enabled after emergency release");
	}

	// NEW: Final validation after release
	private void ValidatePlayerAfterRelease()
	{
		if (!IsOnFloor() && GlobalPosition.Y > 2000) // Arbitrary "fell through world" check
		{
			GD.Print("CRITICAL: Player fell through world after release - emergency teleport!");
			ForceReturnToSafePosition();
		}
		else if (!IsOnFloor())
		{
			// Player is airborne but not falling through world - apply gravity normally
			GD.Print($"Player is airborne after release at Y: {GlobalPosition.Y} - gravity will handle");
		}
		else
		{
			GD.Print($"Player successfully grounded after release at: {GlobalPosition}");
		}
	}
	
	public void ForceReturnToSafePosition()
	{
		GD.Print("=== FORCE RETURN TO SAFE POSITION ===");
		
		Vector2 currentPos = GlobalPosition;
		GD.Print($"Current position: {currentPos}");
		
		// Emergency teleport to safe position
		Vector2 safePos = GetEmergencySafePosition();
		
		// Clear possession state if needed
		if (isPossessing)
		{
			GD.Print("Clearing possession state during emergency teleport");
			isPossessing = false;
			possessedEnemy = null;
			_currentBody = this;
		}
		
		// FIXED: Use deferred call for collision changes
		CallDeferred(nameof(DeferredEmergencyRestore), safePos);
	}

	// NEW: Deferred emergency restore
	private void DeferredEmergencyRestore(Vector2 safePos)
	{
		// Disable physics during teleport
		SetPhysicsProcess(false);
		
		GlobalPosition = safePos;
		Velocity = Vector2.Zero;
		
		// Ensure player is fully restored
		Visible = true;
		if (playerCollision != null)
			playerCollision.Disabled = false;
		
		// Force collision update
		ForceUpdateTransform();
		
		// Re-enable physics after a short delay
		var timer = GetTree().CreateTimer(0.1f);
		timer.Timeout += () => {
			SetPhysicsProcess(true);
			SetProcessInput(true);
			GD.Print($"Emergency teleport completed to: {GlobalPosition}");
		};
	}

	#endregion

	#region Monster Collection and Summoning

	private void CollectMonster(Node2D monster)
	{
		// Safety check - make sure monster still exists and isn't disposed
		if (monster == null || monster.IsQueuedForDeletion())
		{
			GD.Print("Cannot collect monster - already disposed or null");
			return;
		}
		
		// Get scene file path before the monster potentially gets disposed
		string scenePath = "";
		try 
		{
			scenePath = monster.SceneFilePath;
			if (string.IsNullOrEmpty(scenePath))
			{
				// If no scene file path, try to determine from the monster type
				string monsterType = monster.GetType().Name;
				scenePath = $"res://scenes/{monsterType}.tscn";
				GD.Print($"No scene file path found, using generated path: {scenePath}");
			}
		}
		catch (System.ObjectDisposedException)
		{
			GD.Print("Monster disposed while trying to collect - cannot determine scene path");
			return;
		}
		
		for (int i = 0; i < MonsterScenes.Length; i++)
		{
			if (MonsterScenes[i] == null)
			{
				try
				{
					PackedScene monsterScene = ResourceLoader.Load<PackedScene>(scenePath);
					if (monsterScene != null)
					{
						MonsterScenes[i] = monsterScene;
						SummonCosts[i] = CalculateSummonCost(monster);
						
						GD.Print($"Collected {monster.Name} from {scenePath} in slot {i}");
						
						// Mark the monster for immediate cleanup after collection
						if (!monster.IsQueuedForDeletion())
						{
							var cleanupTimer = GetTree().CreateTimer(0.1f);
							cleanupTimer.Timeout += () => {
								if (monster != null && !monster.IsQueuedForDeletion())
								{
									monster.QueueFree();
								}
							};
						}
						return;
					}
					else
					{
						GD.PrintErr($"Failed to load scene: {scenePath}");
					}
				}
				catch (System.Exception ex)
				{
					GD.PrintErr($"Error collecting monster: {ex.Message}");
				}
			}
		}
		
		GD.Print("Inventory full, cannot collect more monsters!");
	}
	
	private int CalculateSummonCost(Node2D monster)
	{
		int maxHp = GetMonsterMaxHealth(monster);
		int baseCost = maxHp / 5;
		
		if (monster.HasMethod("GetDamage"))
		{
			int damage = (int)monster.Call("GetDamage");
			baseCost += damage / 2;
		}
		
		return Mathf.Clamp(baseCost, 10, 100);
	}

	public void SummonMonster(int slot)
	{
		if (MonsterScenes[slot] == null) return;
		
		Node2D monster = MonsterScenes[slot].Instantiate() as Node2D;
		monster.Position = Position + new Vector2(facingRight ? 60 : -60, 0);
		GetParent().AddChild(monster);
		summonedMonster = monster;
		CurSoulSP -= SummonCosts[slot];
		
		// Set up summoned monster
		if (monster.HasMethod("SetIsControlledByPlayer"))
		{
			monster.Call("SetIsControlledByPlayer", true);
		}
		
		// Set collision and groups for summoned monster
		if (monster is CharacterBody2D body)
		{
			body.CollisionLayer = 1u << 5;
			body.CollisionMask = (1u << 1) | (1u << 2);
			
			// Apply the same vertical velocity as the player to maintain momentum
			body.Velocity = new Vector2(0, Velocity.Y);
			
			// Ensure physics processing is enabled
			body.SetPhysicsProcess(true);
			
			// Force the monster to apply gravity immediately
			if (monster is BaseEnemy baseEnemy)
			{
				// This ensures the enemy's own physics processing will handle gravity
				baseEnemy.SetPhysicsProcess(true);
			}
		}
		
		if (monster.IsInGroup("Enemies"))
		{
			monster.RemoveFromGroup("Enemies");
		}
		monster.AddToGroup("Allies");
		monster.AddToGroup("SummonedMonsters");
		
		GD.Print($"Summoned {monster.Name} - Set to controlled state and added to Allies group");
		
		// Add a small delay to ensure physics is properly initialized
		var physicsTimer = GetTree().CreateTimer(0.1f);
		physicsTimer.Timeout += () => {
			if (monster is CharacterBody2D charBody && !charBody.IsOnFloor())
			{
				// Force a physics update to apply gravity
				charBody.ForceUpdateTransform();
			}
		};
	}

	public void OnMonsterDeath(Node2D deadMonster)
	{
		if (summonedMonster == deadMonster)
		{
			if (isPossessing && possessedEnemy == deadMonster)
			{
				// Force release from dead summoned monster
				ReleaseTarget();
			}
			summonedMonster = null;
			GD.Print("Summoned monster died");
		}
	}

	#endregion

	#region Utility Methods (unchanged from original)

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

	private string GetCurrentAnimation(bool onFloor, Vector2 velocity)
	{
		if (isDashing) return "dash";
		if (onFloor) return Mathf.Abs(velocity.X) > 50 ? "run" : "idle";
		if (isWallSliding) return "slidewall";
		if (velocity.Y < -50) return "jump";
		return "fall";
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
			// ADDED: Update player facing when dashing
			if (!isPossessing)
			{
				playerFacingRight = facingRight;
			}
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

	// Enhanced TakeDamage method (ALREADY CORRECT in your code)
	public void TakeDamage(int damage)
	{
		// Prevent damage during death animation or if invulnerable
		if (isDashing || isDying || isInvulnerable) return;

		GD.Print($"Player taking {damage} damage. Current HP: {(isPossessing ? possessedEnemy?.GetHP() : CurSoulHP)}");

		if (isPossessing && possessedEnemy != null)
		{
			int hostMaxHp = possessedEnemy.GetMaxHP();
			int hostHp = possessedEnemy.GetHP();
			hostHp = Mathf.Max(0, hostHp - damage);
			possessedEnemy.SetHP(hostHp);

			float hostRatio = (float)hostHp / hostMaxHp;
			CurSoulHP = (int)(MaxSoulHP * hostRatio);

			if (CurSoulHP <= 0)
			{
				possessedEnemy.SetHP(0);
				ReleaseTarget();
				CurSoulHP = 1;
				GD.Print("Critical hit: Possessed body sacrificed, Player escaped with 1 HP!");
			}
			else if (hostHp <= 0)
			{
				ReleaseTarget();
				GD.Print("Possessed body died, Player escaped.");
			}
		}
		else
		{
			CurSoulHP = Mathf.Max(0, CurSoulHP - damage);
			GD.Print($"Player HP after damage: {CurSoulHP}/{MaxSoulHP}");
			
			if (CurSoulHP <= 0 && !isDying)
			{
				GD.Print("Player died! Starting death sequence...");
				StartDeathSequence();
			}
		}
	}

	// FIXED: StartDeathSequence method 
	private void StartDeathSequence()
	{
		if (isDying) 
		{
			GD.Print("Death sequence already in progress, ignoring duplicate call");
			return; // Prevent multiple death triggers
		}
		
		isDying = true;
		isInvulnerable = true;
		
		GD.Print("=== STARTING DEATH SEQUENCE ===");
		
		// Lock input and physics
		SetProcessInput(false);
		SetPhysicsProcess(false);
		
		// Stop any current movement
		if (_currentBody is CharacterBody2D body)
		{
			body.Velocity = Vector2.Zero;
		}
		else
		{
			Velocity = Vector2.Zero;
		}
		
		// Force release from any possessed enemy
		if (isPossessing)
		{
			GD.Print("Releasing possession during death");
			ReleaseTarget();
		}
		
		// Start death animation
		PlayDieAnimation();
	}


	// FIXED: PlayDieAnimation method with corrected signal connection
	private void PlayDieAnimation()
	{
		var sprite = _currentBody?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") ?? animatedSprite;
		
		if (sprite != null)
		{
			// CRITICAL FIX: Disconnect any existing animation_finished connections first
			if (sprite.IsConnected("animation_finished", Callable.From(OnDeathAnimationFinished)))
			{
				sprite.Disconnect("animation_finished", Callable.From(OnDeathAnimationFinished));
				GD.Print("Disconnected existing animation_finished signal");
			}
			
			// Check if "die" animation exists
			if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation("die"))
			{
				sprite.Play("die");
				GD.Print("Playing death animation");
				
				// FIXED: Connect signal with one-shot connection and specific animation check
				sprite.Connect("animation_finished", Callable.From(OnAnimationFinishedDuringDeath), (uint)ConnectFlags.OneShot);
			}
			else
			{
				// Fallback: create a simple fade-out death effect
				GD.Print("Death animation 'die' not found in SpriteFrames. Using fallback effect.");
				CreateFallbackDeathEffect();
			}
		}
		else
		{
			GD.Print("No sprite found for death animation. Using immediate cleanup.");
			OnDeathAnimationFinished(); // Skip animation and go straight to cleanup
		}
	}
	
	// NEW: Specific method to handle animation finished during death sequence
	private void OnAnimationFinishedDuringDeath()
	{
		var sprite = _currentBody?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") ?? animatedSprite;
		
		// CRITICAL: Only proceed if we're actually in death state and playing death animation
		if (!isDying)
		{
			GD.Print("Animation finished but not in death state - ignoring");
			return;
		}
		
		if (sprite != null && sprite.Animation != "die")
		{
			GD.Print($"Animation finished but not death animation: {sprite.Animation} - ignoring");
			return;
		}
		
		GD.Print("Death animation completed - proceeding with death sequence");
		OnDeathAnimationFinished();
	}

	// CreateFallbackDeathEffect method (ALREADY CORRECT in your code)
	private void CreateFallbackDeathEffect()
	{
		// Double-check we're still in death state
		if (!isDying)
		{
			GD.Print("Not in death state - cancelling fallback death effect");
			return;
		}
		
		var tween = CreateTween();
		
		// Fade out effect
		tween.Parallel().TweenProperty(this, "modulate:a", 0.0f, 1.0f);
		
		// Scale down effect  
		tween.Parallel().TweenProperty(this, "scale", Vector2.Zero, 1.0f);
		
		// Rotation effect
		tween.Parallel().TweenProperty(this, "rotation", Mathf.Pi * 2, 1.0f);
		
		// FIXED: Check death state before calling completion
		tween.TweenCallback(Callable.From(() => {
			if (isDying) // Only proceed if still in death state
			{
				OnDeathAnimationFinished();
			}
		}));
		
		GD.Print("Playing fallback death effect");
	}

	// OnDeathAnimationFinished method (ALREADY CORRECT in your code)
	private void OnDeathAnimationFinished()
	{
		// CRITICAL: Validate we should actually be finishing death sequence
		if (!isDying)
		{
			GD.Print("OnDeathAnimationFinished called but not in death state - ignoring");
			return;
		}
		
		GD.Print("=== DEATH ANIMATION COMPLETED ===");
		
		// Ensure we disconnect any remaining signal connections
		var sprite = _currentBody?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") ?? animatedSprite;
		if (sprite != null && sprite.IsConnected("animation_finished", Callable.From(OnDeathAnimationFinished)))
		{
			sprite.Disconnect("animation_finished", Callable.From(OnDeathAnimationFinished));
		}
		if (sprite != null && sprite.IsConnected("animation_finished", Callable.From(OnAnimationFinishedDuringDeath)))
		{
			sprite.Disconnect("animation_finished", Callable.From(OnAnimationFinishedDuringDeath));
		}
		
		// Save final game state before player destruction
		SavePlayerData();
		
		// Handle player death
		HandlePlayerDeath();
	}

	// HandlePlayerDeath method (ALREADY CORRECT in your code)
	private void HandlePlayerDeath()
	{
		if (!isDying)
		{
			GD.Print("HandlePlayerDeath called but not in death state - ignoring");
			return;
		}
		
		GD.Print("=== HANDLING PLAYER DEATH ===");
		
		// Add a brief delay before respawn
		var timer = GetTree().CreateTimer(2.0f);
		timer.Timeout += () => {
			// Double-check we're still in death state before respawning
			if (isDying)
			{
				RespawnAtCheckpoint();
			}
			else
			{
				GD.Print("Death state changed during delay - cancelling respawn");
			}
		};
	}
	
	// Method to manually trigger checkpoint save (useful for special events)
	public void SaveAtNearestCheckpoint()
	{
		var nearestCheckpoint = FindNearestCheckpoint();
		if (nearestCheckpoint != null)
		{
			nearestCheckpoint.ActivateCheckpoint(this);
		}
	}
	
	private Checkpoint FindNearestCheckpoint()
	{
		Checkpoint nearest = null;
		float nearestDistance = float.MaxValue;
		
		foreach (Node node in GetTree().GetNodesInGroup("Checkpoints"))
		{
			if (node is Checkpoint checkpoint)
			{
				float distance = Position.DistanceTo(checkpoint.Position);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = checkpoint;
				}
			}
		}
		
		return nearest;
	}
	
	// Method to load game from checkpoint (useful for main menu "Continue" button)
	public void LoadFromLastCheckpoint()
	{
		if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
		{
			// Load player data first
			LoadPlayerData();
			
			// Get checkpoint scene
			string checkpointScene = GameManager.Instance.GetCheckpointScene();
			
			if (!string.IsNullOrEmpty(checkpointScene))
			{
				// Change to checkpoint scene
				GetTree().ChangeSceneToFile(checkpointScene);
			}
		}
		else
		{
			GD.Print("No checkpoint data found. Starting new game.");
		}
	}

	// FIXED: RespawnAtCheckpoint method without the missing GameManager method
	private void RespawnAtCheckpoint()
	{
		if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
		{
			// Reset player stats
			CurSoulHP = MaxSoulHP;
			CurSoulMP = MaxSoulMP;
			CurSoulSP = MaxSoulSP;
			
			// Reset state flags
			isDying = false;
			isInvulnerable = false;
			
			// Force release from any possessed enemy
			if (isPossessing)
			{
				ReleaseTarget();
			}
			
			// Re-enable processing
			SetProcessInput(true);
			SetPhysicsProcess(true);
			
			// Reset visual effects
			Modulate = Colors.White;
			Scale = Vector2.One;
			Rotation = 0;
			Visible = true;
			
			// Move to checkpoint position
			Vector2 checkpointPos = GameManager.Instance.GetLastCheckpointPosition();
			Position = checkpointPos;
			
			// Reset velocity
			Velocity = Vector2.Zero;
			
			// Play respawn effect
			CreateRespawnEffect();
			
			string checkpointId = GameManager.Instance.GetLastCheckpointId();
			GD.Print($"Player respawned at checkpoint: {checkpointId}");
		}
		else
		{
			// Fallback: respawn at scene start or default position
			RespawnAtSceneStart();
		}
	}
	
	private void RespawnAtSceneStart()
	{
		// Reset stats
		CurSoulHP = MaxSoulHP;
		CurSoulMP = MaxSoulMP;
		CurSoulSP = MaxSoulSP;
		
		// Reset state
		isDying = false;
		isInvulnerable = false;
		
		if (isPossessing)
		{
			ReleaseTarget();
		}
		
		// Re-enable processing
		SetProcessInput(true);
		SetPhysicsProcess(true);
		
		// Reset visual effects
		Modulate = Colors.White;
		Scale = Vector2.One;
		Rotation = 0;
		Visible = true;
		
		// Move to default spawn position (you can customize this)
		Position = new Vector2(100, 100); // Or wherever your level starts
		Velocity = Vector2.Zero;
		
		CreateRespawnEffect();
		
		GD.Print("Player respawned at scene start (no checkpoint data)");
	}

	// CreateRespawnEffect method (ALREADY CORRECT in your code)
	private void CreateRespawnEffect()
	{
		// Start invisible and scale from zero
		Modulate = new Color(1, 1, 1, 0);
		Scale = Vector2.Zero;
		
		var tween = CreateTween();
		
		// Fade in
		tween.Parallel().TweenProperty(this, "modulate:a", 1.0f, 0.5f);
		
		// Scale up with bounce
		tween.Parallel().TweenProperty(this, "scale", Vector2.One, 0.5f)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
		
		// Brief invulnerability after respawn
		isInvulnerable = true;
		var invulnTimer = GetTree().CreateTimer(2.0f);
		invulnTimer.Timeout += () => { isInvulnerable = false; };
	}

	// SetInvulnerable method (ALREADY CORRECT in your code)
	public void SetInvulnerable(float duration)
	{
		isInvulnerable = true;
		var timer = GetTree().CreateTimer(duration);
		timer.Timeout += () => { isInvulnerable = false; };
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
		
		Modulate = new Color(1, 1, 1, 0.5f);
		RemoveFromGroup("Player");
		AddToGroup("StealthPlayer");
		
		GD.Print("Stealth activated!");
	}

	private void EndStealth()
	{
		isStealth = false;
		Modulate = new Color(1, 1, 1, 1);
		
		RemoveFromGroup("StealthPlayer");
		AddToGroup("Player");
		
		GD.Print("Stealth ended!");
	}

	private void ActivateBlock()
	{
		isBlocking = true;
		blockTimer = BlockDuration;
		blockCooldownTimer = BlockCooldown;
		CurSoulMP -= 20;
		
		GD.Print("Block activated!");
	}

	private void EndBlock()
	{
		isBlocking = false;
		GD.Print("Block ended!");
	}

	private void PerformDoubleJump()
	{
		float jumpVel = _currentBody.Get("JumpVelocity").AsSingle() is float j ? j : JumpVelocity;
		Velocity = new Vector2(Velocity.X, jumpVel * 0.8f);
		currentJumps--;
		GD.Print($"Double jump! {currentJumps}/{MaxJumps} jumps remaining");
	}

	private void PerformAirDash()
	{
		dashDirection = Input.GetAxis("move_left", "move_right");
		if (dashDirection != 0)
		{
			CurSoulMP = Mathf.Max(0, CurSoulMP - 15);
			currentDashes--;
			StartDash(dashDirection);
			GD.Print($"Air dash! {currentDashes}/{MaxDashes} dashes remaining");
		}
	}
	
	// Public API methods
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
	
	// Method để save player state
	public void SavePlayerData()
	{
		if (GameManager.Instance != null)
		{
			// Save player stats
			GameManager.Instance.UpdatePlayerStats(
				CurSoulHP, CurSoulMP, CurSoulSP,
				MaxSoulHP, MaxSoulMP, MaxSoulSP
			);
			
			// Save monster collection
			GameManager.Instance.SaveUnlockedMonsters(UnlockedSlots);
			
			// Save possession state
			GameManager.Instance.UpdatePlayerData("is_possessing", isPossessing);
			if (possessedEnemy != null)
			{
				GameManager.Instance.UpdatePlayerData("possessed_enemy_type", possessedEnemy.GetType().Name);
				GameManager.Instance.UpdatePlayerData("possessed_enemy_health", possessedEnemy.GetHP());
			}
			
			// Save current level
			GameManager.Instance.UpdatePlayerData("current_level", GetTree().CurrentScene.SceneFilePath);
			
			// Save player position
			GameManager.Instance.UpdatePlayerData("player_position_x", Position.X);
			GameManager.Instance.UpdatePlayerData("player_position_y", Position.Y);
			
			GD.Print("Player data saved to GameManager");
		}
	}

	// Method để load player state
	public void LoadPlayerData()
	{
		if (GameManager.Instance != null)
		{
			var stats = GameManager.Instance.GetPlayerStats();
			if (stats.Count > 0)
			{
				CurSoulHP = stats["soul_hp"];
				CurSoulMP = stats["soul_mp"];
				CurSoulSP = stats["soul_sp"];
				MaxSoulHP = stats["max_soul_hp"];
				MaxSoulMP = stats["max_soul_mp"];
				MaxSoulSP = stats["max_soul_sp"];
			}

			// Load unlocked monsters
			UnlockedSlots = GameManager.Instance.GetUnlockedMonsters();
			
			// Load player position
			float posX = GameManager.Instance.GetPlayerData("player_position_x", Position.X).AsSingle();
			float posY = GameManager.Instance.GetPlayerData("player_position_y", Position.Y).AsSingle();
			Position = new Vector2(posX, posY);
			
			GD.Print("Player data loaded from GameManager");
		}
	}

	// Gọi method này khi cần auto-save (ví dụ: tại checkpoints)
	public void TriggerAutoSave()
	{
		SavePlayerData();
		if (GameManager.Instance.SaveGameDataToSlot(0)) // Auto-save slot
		{
			ShowSaveNotification("Game Auto-Saved!");
		}
	}

	// Method để hiển thị thông báo save
	private void ShowSaveNotification(string message)
	{
		// Tạo notification UI
		var notification = new Label();
		notification.Text = message;
		notification.AddThemeFontSizeOverride("font_size", 20);
		notification.Modulate = Colors.Green;
		notification.Position = new Vector2(50, 50);
		
		// Thêm vào scene tree
		GetTree().CurrentScene.AddChild(notification);
		
		// FIXED: Use TweenInterval instead of TweenDelay
		var tween = CreateTween();
		tween.TweenInterval(2.0f); // Wait 2 seconds
		tween.TweenProperty(notification, "modulate:a", 0.0f, 1.0f);
		tween.TweenCallback(Callable.From(() => notification.QueueFree()));
	}
	
	#region Force Enemy Retargeting System

	// Method to force all enemies to reassess their targets
	private void ForceAllEnemiesRetarget()
	{
		GD.Print("Forcing all enemies to reassess targets...");
		
		// Get all enemies in the scene
		var enemies = GetTree().GetNodesInGroup("Enemies");
		var playerControlledEntities = GetTree().GetNodesInGroup("PlayerControlled");
		var allies = GetTree().GetNodesInGroup("Allies");
		var summonedMonsters = GetTree().GetNodesInGroup("SummonedMonsters");
		
		// Combine all potential attacking entities
		var allAttackers = new System.Collections.Generic.List<Node>();
		allAttackers.AddRange(enemies);
		allAttackers.AddRange(playerControlledEntities);
		allAttackers.AddRange(allies);
		allAttackers.AddRange(summonedMonsters);
		
		foreach (Node node in allAttackers)
		{
			if (node is BaseEnemy enemy && !enemy.IsQueuedForDeletion())
			{
				// Force the enemy to clear its current target and find a new one
				ForceEnemyRetarget(enemy);
			}
		}
		
		GD.Print($"Forced retargeting for {allAttackers.Count} potential attackers");
	}

	// Method to force a specific enemy to retarget
	private void ForceEnemyRetarget(BaseEnemy enemy)
	{
		if (enemy == null || !IsInstanceValid(enemy) || enemy.IsQueuedForDeletion()) return;
		if (enemy == null || enemy.IsQueuedForDeletion()) return;
		
		// Use reflection or direct method call to clear target
		if (enemy.HasMethod("ForceTargetClear"))
		{
			enemy.Call("ForceTargetClear");
		}
		else
		{
			// Fallback: use reflection to clear the private _target field
			var targetField = enemy.GetType().GetField("_target", 
				System.Reflection.BindingFlags.NonPublic | 
				System.Reflection.BindingFlags.Instance);
			
			if (targetField != null)
			{
				targetField.SetValue(enemy, null);
				GD.Print($"Cleared target for {enemy.Name} via reflection");
			}
		}
		
		// If the enemy is currently attacking, we need to handle this carefully
		if (enemy.IsAttacking() && !enemy.IsAttackingUninterruptible)
		{
			// For interruptible attacks, we can force them back to idle
			if (enemy.HasMethod("TransitionToState"))
			{
				enemy.Call("TransitionToState", (int)BaseEnemy.EnemyState.Idle);
			}
		}
		
		GD.Print($"Forced retargeting for enemy: {enemy.Name}");
	}
	
	public void ResetDeathState()
	{
		if (isDying)
		{
			GD.Print("=== MANUALLY RESETTING DEATH STATE ===");
			
			isDying = false;
			isInvulnerable = false;
			
			// Re-enable processing
			SetProcessInput(true);
			SetPhysicsProcess(true);
			
			// Reset visual effects
			Modulate = Colors.White;
			Scale = Vector2.One;
			Rotation = 0;
			Visible = true;
			
			// Ensure HP is not zero
			if (CurSoulHP <= 0)
			{
				CurSoulHP = 1;
			}
			
			GD.Print("Death state manually reset");
		}
	}

	#endregion
	
	#region Safe Position System - FIXED

	private Vector2 FindSafeReleasePosition(Vector2 enemyPosition)
	{
		GD.Print($"Finding safe release position from: {enemyPosition}");
		
		// List of offsets to try (in priority order)
		Vector2[] offsets = {
			new Vector2(0, -30),    // Above (highest priority)
			new Vector2(-40, -20),  // Left-above
			new Vector2(40, -20),   // Right-above
			new Vector2(-60, 0),    // Left
			new Vector2(60, 0),     // Right
			new Vector2(-40, 20),   // Left-below (if enemy is on platform)
			new Vector2(40, 20),    // Right-below
			new Vector2(0, -60),    // Higher up
			new Vector2(-80, -30),  // Further left
			new Vector2(80, -30),   // Further right
			new Vector2(0, -100),   // Much higher
			new Vector2(-120, -50), // Much further left
			new Vector2(120, -50),  // Much further right
		};

		foreach (Vector2 offset in offsets)
		{
			Vector2 testPosition = enemyPosition + offset;
			
			if (IsSafePosition(testPosition))
			{
				GD.Print($"Found safe position at offset {offset}: {testPosition}");
				return testPosition;
			}
		}

		GD.Print("No safe position found nearby, trying ground search...");
		
		// If no safe position found, try to find nearest ground
		Vector2 groundPosition = FindNearestGroundPosition(enemyPosition);
		if (groundPosition != Vector2.Zero)
		{
			GD.Print($"Using nearest ground position: {groundPosition}");
			return groundPosition;
		}

		GD.Print("No ground found, using checkpoint fallback...");
		
		// Finally, return to nearest checkpoint
		Vector2 checkpointPos = GetLastSafeCheckpointPosition();
		GD.Print($"Final fallback to safe checkpoint: {checkpointPos}");
		return checkpointPos;
	}

	private bool IsSafePosition(Vector2 position)
	{
		// Get physics space
		var spaceState = GetWorld2D().DirectSpaceState;
		
		// FIXED: Use comprehensive collision mask that includes all solid layers
		// Check layers 3 (world)
		uint solidLayerMask = (1u << 2); // Add more layers as needed
		
		// Test 1: Check if there's ground below (within reasonable distance)
		var groundQuery = PhysicsRayQueryParameters2D.Create(position, position + Vector2.Down * 200);
		groundQuery.CollisionMask = solidLayerMask;
		
		// Create proper exclusion array
		var excludeArray = new Godot.Collections.Array<Rid>();
		excludeArray.Add(GetRid()); // Exclude player
		groundQuery.Exclude = excludeArray;
		
		var groundResult = spaceState.IntersectRay(groundQuery);
		
		// Must have ground below within 200 pixels
		if (groundResult.Count == 0)
		{
			GD.Print($"Position {position} rejected: No ground below within 200 pixels");
			return false;
		}

		Vector2 groundPos = groundResult["position"].AsVector2();
		float groundDistance = position.DistanceTo(groundPos);
		
		// Ground should be reasonably close but not too close (to avoid being inside it)
		if (groundDistance < 20 || groundDistance > 200)
		{
			GD.Print($"Position {position} rejected: Ground distance {groundDistance} is invalid");
			return false;
		}

		// Test 2: Check if position is inside any solid objects
		var bodyQuery = new PhysicsPointQueryParameters2D();
		bodyQuery.Position = position;
		bodyQuery.CollisionMask = solidLayerMask; // Same comprehensive mask
		bodyQuery.Exclude = excludeArray;
		
		var bodyResults = spaceState.IntersectPoint(bodyQuery, 5);
		
		if (bodyResults.Count > 0)
		{
			GD.Print($"Position {position} rejected: Inside solid object");
			// Debug what we're colliding with
			foreach (var result in bodyResults)
			{
				if (result.ContainsKey("collider"))
				{
					var collider = result["collider"].AsGodotObject();
					GD.Print($"  - Colliding with: {collider?.GetType().Name ?? "Unknown"}");
				}
			}
			return false;
		}

		// Test 3: Additional clearance check - make sure there's space around the player
		Vector2[] clearanceOffsets = {
			new Vector2(-16, 0),  // Left side
			new Vector2(16, 0),   // Right side
			new Vector2(0, -32),  // Above head
		};
		
		foreach (var clearanceOffset in clearanceOffsets)
		{
			var clearanceQuery = new PhysicsPointQueryParameters2D();
			clearanceQuery.Position = position + clearanceOffset;
			clearanceQuery.CollisionMask = solidLayerMask;
			clearanceQuery.Exclude = excludeArray;
			
			var clearanceResults = spaceState.IntersectPoint(clearanceQuery, 2);
			if (clearanceResults.Count > 0)
			{
				GD.Print($"Position {position} rejected: No clearance at offset {clearanceOffset}");
				return false;
			}
		}

		GD.Print($"Position {position} is SAFE - ground at {groundPos} (distance: {groundDistance})");
		return true;
	}

	private Vector2 FindNearestGroundPosition(Vector2 startPosition)
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		uint solidLayerMask = (1u << 2); // Comprehensive solid layers
		
		// Create exclude array once for reuse
		var excludeArray = new Godot.Collections.Array<Rid>();
		excludeArray.Add(GetRid());
		
		// Try to find ground in expanding circles around the start position
		for (int radius = 50; radius <= 400; radius += 50)
		{
			for (int angle = 0; angle < 360; angle += 30)
			{
				float radians = Mathf.DegToRad(angle);
				Vector2 testPos = startPosition + new Vector2(
					Mathf.Cos(radians) * radius,
					Mathf.Sin(radians) * radius
				);

				var query = PhysicsRayQueryParameters2D.Create(testPos, testPos + Vector2.Down * 300);
				query.CollisionMask = solidLayerMask;
				query.Exclude = excludeArray;
				
				var result = spaceState.IntersectRay(query);
				
				if (result.Count > 0)
				{
					Vector2 groundPos = result["position"].AsVector2();
					Vector2 safePos = groundPos + Vector2.Up * 40; // 40 pixels above ground for safety
					
					if (IsSafePosition(safePos))
					{
						GD.Print($"Found safe ground position at radius {radius}, angle {angle}: {safePos}");
						return safePos;
					}
				}
			}
		}

		GD.Print("No safe ground position found in expanded search");
		return Vector2.Zero; // Not found
	}

	// ADDED: Method to get collision layer information for debugging
	private void DebugCollisionLayers()
	{
		GD.Print("=== COLLISION LAYER DEBUG ===");
		
		// Check what's in the scene on different layers
		var spaceState = GetWorld2D().DirectSpaceState;
		Vector2 testPos = Position;
		
		for (uint layer = 0; layer < 10; layer++)
		{
			uint layerMask = 1u << (int)layer;
			
			var query = PhysicsRayQueryParameters2D.Create(testPos, testPos + Vector2.Down * 100);
			query.CollisionMask = layerMask;
			
			var result = spaceState.IntersectRay(query);
			if (result.Count > 0)
			{
				var collider = result["collider"].AsGodotObject();
				GD.Print($"Layer {layer}: Found {collider?.GetType().Name ?? "Unknown"}");
			}
		}
	}

	// IMPROVED: Enhanced debug method
	private void DebugSafePositionSystem(Vector2 enemyPosition)
	{
		GD.Print($"=== ENHANCED DEBUG SAFE POSITION SYSTEM ===");
		GD.Print($"Enemy position: {enemyPosition}");
		GD.Print($"Player collision layer: {CollisionLayer}");
		GD.Print($"Player collision mask: {CollisionMask}");
		
		// Debug collision layers
		DebugCollisionLayers();
		
		// Test comprehensive raycast from enemy position
		var spaceState = GetWorld2D().DirectSpaceState;
		uint solidLayerMask = (1u << 2);
		
		var testQuery = PhysicsRayQueryParameters2D.Create(enemyPosition, enemyPosition + Vector2.Down * 300);
		testQuery.CollisionMask = solidLayerMask;
		
		var result = spaceState.IntersectRay(testQuery);
		
		GD.Print($"Comprehensive raycast from enemy pos - Hit count: {result.Count}");
		if (result.Count > 0)
		{
			Vector2 hitPos = result["position"].AsVector2();
			var collider = result["collider"].AsGodotObject();
			GD.Print($"Ground found at: {hitPos}, Distance: {enemyPosition.DistanceTo(hitPos)}");
			GD.Print($"Ground object: {collider?.GetType().Name ?? "Unknown"}");
			
			// Test multiple safe positions above the ground
			for (int height = 20; height <= 100; height += 20)
			{
				Vector2 testSafePos = hitPos + Vector2.Up * height;
				bool isSafe = IsSafePosition(testSafePos);
				GD.Print($"Safe position test at height {height}: {testSafePos} - Safe: {isSafe}");
			}
		}
		else
		{
			GD.Print("NO GROUND FOUND with comprehensive mask - checking individual layers:");
			
			for (uint layer = 0; layer < 10; layer++)
			{
				uint layerMask = 1u << (int)layer;
				var layerQuery = PhysicsRayQueryParameters2D.Create(enemyPosition, enemyPosition + Vector2.Down * 300);
				layerQuery.CollisionMask = layerMask;
				
				var layerResult = spaceState.IntersectRay(layerQuery);
				if (layerResult.Count > 0)
				{
					var collider = layerResult["collider"].AsGodotObject();
					Vector2 hitPos = layerResult["position"].AsVector2();
					GD.Print($"  Layer {layer}: {collider?.GetType().Name ?? "Unknown"} at {hitPos}");
				}
			}
		}
	}

	// ADDED: Emergency safe position that always works
	private Vector2 GetEmergencySafePosition()
	{
		// Try to find any checkpoint in the scene
		var checkpoints = GetTree().GetNodesInGroup("Checkpoints");
		if (checkpoints.Count > 0)
		{
			var checkpoint = checkpoints[0] as Node2D;
			if (checkpoint != null)
			{
				Vector2 checkpointPos = checkpoint.GlobalPosition + Vector2.Up * 50;
				GD.Print($"Using emergency checkpoint position: {checkpointPos}");
				return checkpointPos;
			}
		}
		
		// Try to find player spawn markers
		var spawns = GetTree().GetNodesInGroup("PlayerSpawn");
		if (spawns.Count > 0)
		{
			var spawn = spawns[0] as Node2D;
			if (spawn != null)
			{
				GD.Print($"Using emergency spawn position: {spawn.GlobalPosition}");
				return spawn.GlobalPosition;
			}
		}
		
		// Try starting position from GameManager
		if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
		{
			Vector2 checkpointPos = GameManager.Instance.GetLastCheckpointPosition();
			if (checkpointPos != Vector2.Zero)
			{
				GD.Print($"Using GameManager checkpoint: {checkpointPos}");
				return checkpointPos;
			}
		}
		
		// Last resort: scene center elevated (based on your map layout)
		// Since your map seems to have ground around Y=-42 to Y=21, place player well above
		Vector2 emergencyPos = new Vector2(0, -200); // High above the ground level
		GD.Print($"Using emergency high position: {emergencyPos}");
		return emergencyPos;
	}

	private Vector2 GetLastSafeCheckpointPosition()
	{
		// Try GameManager first
		if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
		{
			return GameManager.Instance.GetLastCheckpointPosition();
		}

		// Find nearest checkpoint in scene
		var checkpoints = GetTree().GetNodesInGroup("Checkpoints");
		if (checkpoints.Count > 0)
		{
			Vector2 nearestCheckpointPos = Vector2.Zero;
			float nearestDistance = float.MaxValue;
			
			foreach (Node node in checkpoints)
			{
				if (node is Node2D checkpoint)
				{
					float distance = Position.DistanceTo(checkpoint.Position);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestCheckpointPos = checkpoint.Position;
					}
				}
			}
			
			if (nearestCheckpointPos != Vector2.Zero)
			{
				GD.Print($"Using nearest checkpoint at: {nearestCheckpointPos}");
				return nearestCheckpointPos + Vector2.Up * 30; // Elevated for safety
			}
		}

		// Use emergency safe position
		return GetEmergencySafePosition();
	}
	
	private void ValidateStartPosition()
	{
		// Delay má»™t chÃºt Ä‘á»ƒ scene hoÃ n toÃ n load xong
		var timer = GetTree().CreateTimer(0.1f);
		timer.Timeout += () => {
			if (!IsSafePosition(Position))
			{
				GD.Print("Player started in unsafe position - correcting...");
				ForceReturnToSafePosition();
			}
		};
	}

	#endregion

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
			GD.PrintErr($"Cannot write to log file: {e.Message}");
		}
	}

	#endregion
}
