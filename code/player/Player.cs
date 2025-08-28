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
	private bool facingRight = true;

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

	private readonly string fullPotionTexturePath = "res://img/GUI/Status/Healing_Potion.png";
	private readonly string emptyPotionTexturePath = "res://img/GUI/Status/Healing_Potion_Used.png";

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
		Vector2 velocity = _currentBody is CharacterBody2D body ? body.Velocity : Velocity;
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
		// Always allow possession of summoned monsters
		if (enemy == summonedMonster) return true;
		
		// Allow possession of dead enemies
		if (enemy.IsDead()) return true;
		
		// Check possession threshold
		return enemy.CanBePossessed();
	}

	private void PossessTarget(BaseEnemy target)
	{
		if (target == null || !target.CanBePossessed())
		{
			GD.Print($"{target?.Name ?? "Target"} cannot be possessed right now!");
			return;
		}

		// Update facing direction to match player
		if (target.HasMethod("UpdateFacing"))
		{
			float direction = facingRight ? 1f : -1f;
			target.Call("UpdateFacing", direction);
		}
		else
		{
			var sprite = target.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (sprite != null)
			{
				sprite.FlipH = !facingRight;
			}
		}

		// Set enemy health based on player health ratio
		int hostMaxHp = target.GetMaxHP();
		float playerRatio = (float)CurSoulHP / MaxSoulHP;
		int hostHp = Mathf.CeilToInt(hostMaxHp * playerRatio);
		target.SetHP(hostHp);

		possessedEnemy = target;
		isPossessing = true;
		_currentBody = target;

		// Handle collision settings
		if (target is CharacterBody2D targetBody)
		{
			target.SetOriginalCollisionLayer(targetBody.CollisionLayer, targetBody.CollisionMask);
			targetBody.CollisionLayer = originalCollisionLayer;
			targetBody.CollisionMask = originalCollisionMask;
		}

		// Update groups
		if (target.IsInGroup("Enemies"))
		{
			target.RemoveFromGroup("Enemies");
			target.AddToGroup("Player");
		}

		// Set player control
		target.SetIsControlledByPlayer(true);

		// Hide player and disable collision
		Visible = false;
		if (playerCollision != null) playerCollision.Disabled = true;

		GD.Print($"Possessed {target.Name}: {hostHp}/{hostMaxHp} HP ({playerRatio:P0} of player health)");
	}

	private void ReleaseTarget()
	{
		if (!isPossessing || possessedEnemy == null) return;

		// Update player health based on possessed enemy health
		int hostMaxHp = possessedEnemy.GetMaxHP();
		int hostHp = possessedEnemy.GetHP();
		float hostRatio = (float)hostHp / hostMaxHp;
		CurSoulHP = Mathf.Max(1, (int)(MaxSoulHP * hostRatio));

		// Restore enemy collision and groups
		if (possessedEnemy is CharacterBody2D targetBody)
		{
			if (possessedEnemy != summonedMonster)
			{
				uint originalLayer = possessedEnemy.GetOriginalCollisionLayer();
				uint originalMask = possessedEnemy.GetOriginalCollisionMask();
				targetBody.CollisionLayer = originalLayer;
				targetBody.CollisionMask = originalMask;
			}
		}

		// Update groups - don't add summoned monsters back to enemies
		if (possessedEnemy.IsInGroup("Player"))
		{
			possessedEnemy.RemoveFromGroup("Player");
			
			if (possessedEnemy != summonedMonster)
			{
				possessedEnemy.AddToGroup("Enemies");
			}
		}

		// Disable player control
		possessedEnemy.SetIsControlledByPlayer(false);

		// Collect dead enemy if not a summoned monster
		if (hostHp <= 0 && possessedEnemy != summonedMonster)
		{
			CollectMonster(possessedEnemy);
		}

		// Restore player
		isPossessing = false;
		_currentBody = this;

		if (playerCollision != null) playerCollision.Disabled = false;
		Visible = true;
		SetPhysicsProcess(true);
		SetProcessInput(true);

		Position = possessedEnemy.Position + new Vector2(0, -20);
		
		// Reset enemy attack state
		possessedEnemy.ResetAttackState();

		var tempEnemy = possessedEnemy;
		possessedEnemy = null;

		GD.Print($"Released possession: Player has {CurSoulHP}/{MaxSoulHP} HP");

		// Re-enable summoned monster control
		if (tempEnemy == summonedMonster)
		{
			tempEnemy.SetIsControlledByPlayer(true);
			GD.Print("Summoned monster returned to controlled state");
		}
	}

	private void ProcessPossessedEnemyPhysics(BaseEnemy enemyBody, double delta)
	{
		if (!isPossessing || !(enemyBody is CharacterBody2D body)) return;
		
		float deltaF = (float)delta;
		Vector2 velocity = body.Velocity;
		
		// Update timers for player-controlled enemy
		if (enemyBody.HasMethod("UpdateTimersPlayerControlled"))
		{
			enemyBody.Call("UpdateTimersPlayerControlled", deltaF);
		}
		
		// Apply gravity
		if (!body.IsOnFloor())
			velocity.Y += Gravity * deltaF;
		
		// Check if movement is locked during attack
		bool isMovementLocked = enemyBody.Get("_isMovementLocked").AsBool();
		bool isAttacking = enemyBody.IsAttacking();
		
		// Handle movement input - only when not attacking and movement not locked
		if (!isAttacking && !isDashing && !isMovementLocked)
		{
			float direction = Input.GetAxis("move_left", "move_right");
			float speed = enemyBody.Get("Speed").AsSingle() is float s ? s : Speed;
			velocity.X = Mathf.MoveToward(velocity.X, direction * speed, speed);
			
			// Update facing direction and attack areas
			if (direction != 0)
			{
				bool newFacingRight = direction > 0;
				if (newFacingRight != facingRight)
				{
					facingRight = newFacingRight;
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
		
		// Handle jump input - only when not attacking and movement not locked
		if (Input.IsActionJustPressed("move_up") && body.IsOnFloor() && !isAttacking && !isMovementLocked)
		{
			float jumpVel = enemyBody.Get("JumpVelocity").AsSingle() is float j ? j : JumpVelocity;
			velocity.Y = jumpVel;
		}
		
		// Apply velocity and move
		body.Velocity = velocity;
		body.MoveAndSlide();
		
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

	#endregion

	#region Monster Collection and Summoning

	private void CollectMonster(Node2D monster)
	{
		for (int i = 0; i < MonsterScenes.Length; i++)
		{
			if (MonsterScenes[i] == null)
			{
				PackedScene monsterScene = ResourceLoader.Load<PackedScene>(monster.SceneFilePath);
				MonsterScenes[i] = monsterScene;
				SummonCosts[i] = CalculateSummonCost(monster);
				
				GD.Print($"Collected {monster.Name} in slot {i}");
				return;
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

	public void TakeDamage(int damage)
	{
		if (isDashing) return;

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
			if (CurSoulHP <= 0)
			{
				GD.Print("Player died!");
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
