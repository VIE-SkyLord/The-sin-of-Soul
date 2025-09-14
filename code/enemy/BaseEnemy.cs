using Godot;

public partial class BaseEnemy : CharacterBody2D
{
	// Standardized state enum for all enemies
	public enum EnemyState
	{
		Idle,
		Patrol,
		Chase,
		NormalAttack,
		SpecialAttack,
		Stunned,
		Dead
	}

	// Common properties
	[Export] public float Speed = 500.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float AttackRange = 100.0f;
	[Export] public float SpecialAttackRange = 150.0f;
	[Export] public float DetectionRange = 640.0f;
	[Export] public int MaxHealth = 100;
	[Export] public int Damage = 25;
	[Export] public int SpecialDamage = 50;
	[Export] public float PossessThresholdF = 1.0f;
	[Export] public float AttackCooldownDuration = 1.5f;
	[Export] public float SpecialAttackCooldownDuration = 3.0f;

	// Common components
	protected AnimatedSprite2D _animatedSprite;
	protected CollisionShape2D _collisionShape;
	protected Area2D _normalAttackArea;
	protected Area2D _specialAttackArea;
	protected CollisionShape2D _normalAttackCollision;
	protected CollisionShape2D _specialAttackCollision;

	// State management
	protected EnemyState _currentState = EnemyState.Idle;
	protected EnemyState _previousState = EnemyState.Idle;

	// Common state variables
	protected int _currentHealth;
	protected bool _facingRight = true;
	protected bool _canAttack = true;
	protected float _attackCooldown = 0.0f;
	protected bool _damageDealt = false;
	protected bool _isStunned = false;
	protected float _stunDuration = 0.0f;
	protected Node2D _target;
	protected bool _isPlayerControlled = false;
	protected bool _isMovementLocked = false;

	// Attack state tracking
	protected bool _isAttacking = false;
	protected bool _isAttackingUninterruptible = false;

	// Original collision settings
	protected uint _originalCollisionLayer;
	protected uint _originalCollisionMask;

	// Patrol system
	protected Vector2 _patrolPointA;
	protected Vector2 _patrolPointB;
	protected bool _movingToB = true;
	protected bool _hasPatrolPoints = false;

	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	// Properties
	public int CurrentHealth { get { return _currentHealth; } set { _currentHealth = value; } }
	public bool IsAttackingUninterruptible => _isAttackingUninterruptible;
	public EnemyState CurrentState => _currentState;

	public override void _Ready()
	{
		InitializeCommonComponents();
		InitializeStats();
		InitializePatrolPoints();
		ConnectCommonSignals();
		OnEnemyReady(); // Virtual method for child classes
	}

	protected virtual void OnEnemyReady()
	{
		// Override in child classes for specific initialization
	}

	protected virtual void InitializeCommonComponents()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		_normalAttackArea = GetNodeOrNull<Area2D>("NormalAttackArea");
		_specialAttackArea = GetNodeOrNull<Area2D>("SpecialAttackArea");
		
		if (_normalAttackArea != null)
		{
			_normalAttackCollision = _normalAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_normalAttackArea.Monitoring = false;
			_normalAttackArea.BodyEntered += OnNormalAttackAreaEntered;
		}
		
		if (_specialAttackArea != null)
		{
			_specialAttackCollision = _specialAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_specialAttackArea.Monitoring = false;
			_specialAttackArea.BodyEntered += OnSpecialAttackAreaEntered;
		}
	}

	protected virtual void InitializeStats()
	{
		_currentHealth = MaxHealth;
		_originalCollisionLayer = CollisionLayer;
		_originalCollisionMask = CollisionMask;
	}

	protected virtual void InitializePatrolPoints()
	{
		_patrolPointA = GlobalPosition + Vector2.Left * 150;
		_patrolPointB = GlobalPosition + Vector2.Right * 150;
		_hasPatrolPoints = true;
	}

	protected virtual void ConnectCommonSignals()
	{
		if (_animatedSprite != null && !_animatedSprite.IsConnected("animation_finished", new Callable(this, nameof(OnAnimationFinished))))
		{
			_animatedSprite.Connect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead()) return;
		
		if (_target != null && !IsInstanceValid(_target))
		{
			_target = null;
			TransitionToState(EnemyState.Idle);
			return;
		}
		
		float deltaF = (float)delta;
		UpdateTimers(deltaF);
		UpdatePhysics(deltaF);
		
		// Always process physics, even when player controlled
		if (!_isPlayerControlled)
		{
			ProcessStateLogic();
		}
		
		MoveAndSlide();
	}

	protected virtual void UpdateTimers(float delta)
	{
		if (_attackCooldown > 0)
			_attackCooldown -= delta;

		if (_isStunned && _stunDuration > 0)
		{
			_stunDuration -= delta;
			if (_stunDuration <= 0)
			{
				_isStunned = false;
				TransitionToState(EnemyState.Idle);
			}
		}
	}

	protected virtual void UpdatePhysics(float delta)
	{
		// Always apply gravity regardless of control state
		if (!IsOnFloor())
		{
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * delta);
		}
		
		// If player-controlled, we might need to handle additional physics
		if (_isPlayerControlled)
		{
			// Ensure we're still affected by gravity even when player-controlled
			if (!IsOnFloor())
			{
				Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * delta);
			}
		}
	}

	protected virtual void ProcessStateLogic()
	{
		// Find target if we don't have one
		if (_target == null)
			FindTarget();

		// Process current state
		switch (_currentState)
		{
			case EnemyState.Idle:
				ProcessIdleState();
				break;
			case EnemyState.Patrol:
				ProcessPatrolState();
				break;
			case EnemyState.Chase:
				ProcessChaseState();
				break;
			case EnemyState.NormalAttack:
				ProcessNormalAttackState();
				break;
			case EnemyState.SpecialAttack:
				ProcessSpecialAttackState();
				break;
			case EnemyState.Stunned:
				ProcessStunnedState();
				break;
			case EnemyState.Dead:
				ProcessDeadState();
				break;
		}
	}

	#region State Processing Methods

	protected virtual void ProcessIdleState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		PlayAnimation("idle");

		if (_target != null && CanSeeTarget())
		{
			TransitionToState(EnemyState.Chase);
		}
		else if (_hasPatrolPoints && _target == null)
		{
			TransitionToState(EnemyState.Patrol);
		}
	}

	protected virtual void ProcessPatrolState()
	{
		if (!_hasPatrolPoints)
		{
			TransitionToState(EnemyState.Idle);
			return;
		}

		Vector2 targetPoint = _movingToB ? _patrolPointB : _patrolPointA;
		Vector2 direction = (targetPoint - GlobalPosition).Normalized();

		if (GlobalPosition.DistanceTo(targetPoint) < 30f)
		{
			_movingToB = !_movingToB;
			direction = Vector2.Zero;
		}

		Velocity = new Vector2(direction.X * Speed * 0.6f, Velocity.Y);
		UpdateFacing(direction.X);
		PlayAnimation("run");

		if (_target != null && CanSeeTarget())
		{
			TransitionToState(EnemyState.Chase);
		}
	}

	protected virtual void ProcessChaseState()
	{
		if (_target == null || !CanSeeTarget())
		{
			_target = null;
			TransitionToState(EnemyState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_target.GlobalPosition);

		// Check if we can attack
		if (CanPerformAttack() && distanceToTarget <= GetCurrentAttackRange())
		{
			Vector2 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			UpdateFacing(directionToTarget.X);
			
			EnemyState attackType = DecideAttackType();
			TransitionToState(attackType);
			return;
		}

		// Move towards target or idle if close enough
		if (distanceToTarget > AttackRange * 0.9f)
		{
			Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
			Velocity = new Vector2(direction.X * Speed, Velocity.Y);
			UpdateFacing(direction.X);
			PlayAnimation("run");
		}
		else
		{
			Velocity = new Vector2(0, Velocity.Y);
			PlayAnimation("idle");
		}
	}

	protected virtual void ProcessNormalAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		// Attack logic handled by child classes
	}

	protected virtual void ProcessSpecialAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		// Special attack logic handled by child classes
	}

	protected virtual void ProcessStunnedState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		PlayAnimation("idle");
	}

	protected virtual void ProcessDeadState()
	{
		Velocity = new Vector2(0, Velocity.Y);
	}

	#endregion

	#region State Management

	protected virtual void TransitionToState(EnemyState newState)
	{
		if (_currentState == newState) return;

		_previousState = _currentState;
		_currentState = newState;
		OnStateChanged(_previousState, _currentState);
	}

	protected virtual void OnStateChanged(EnemyState fromState, EnemyState toState)
	{
		GD.Print($"{Name} state changed: {fromState} -> {toState}");
	}

	#endregion

	#region Attack System

	protected virtual bool CanPerformAttack()
	{
		return _canAttack && _attackCooldown <= 0 && !_isAttacking && !_isStunned;
	}

	protected virtual float GetCurrentAttackRange()
	{
		return AttackRange;
	}

	protected virtual EnemyState DecideAttackType()
	{
		// Default: 70% normal, 30% special
		return GD.Randf() < 0.7f ? EnemyState.NormalAttack : EnemyState.SpecialAttack;
	}

	protected virtual void StartAttack(EnemyState attackType)
	{
		_canAttack = false;
		_isAttacking = true;
		_damageDealt = false;
		
		TransitionToState(attackType);
		
		if (attackType == EnemyState.NormalAttack)
		{
			PlayAnimation("normal_attack");
			_attackCooldown = AttackCooldownDuration;
		}
		else if (attackType == EnemyState.SpecialAttack)
		{
			PlayAnimation("special_attack");
			_attackCooldown = SpecialAttackCooldownDuration;
		}
	}

	protected virtual void EndAttack()
	{
		_isAttacking = false;
		_isAttackingUninterruptible = false;
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
		
		// Cooldown timer to re-enable attacking
		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };
		
		TransitionToState(EnemyState.Chase);
	}

	public virtual void PerformNormalAttack()
	{
		if (!CanPerformAttack()) return;
		
		FindTarget();
		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= AttackRange)
		{
			StartAttack(EnemyState.NormalAttack);
		}
	}

	public virtual void PerformSpecialAttack()
	{
		if (!CanPerformAttack()) return;
		//_isPlayerControlled
		FindTarget();
		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= SpecialAttackRange)
		{
			StartAttack(EnemyState.SpecialAttack);
		}
	}

	#endregion

	#region Player Control Attack System - FIXED

	public virtual void PerformPlayerNormalAttack()
	{
		GD.Print($"PerformPlayerNormalAttack called - CanAttack: {CanAttack()}, Attacking: {_isAttacking}");
		
		if (!CanAttack()) 
		{
			GD.Print("Cannot attack: conditions not met");
			return;
		}
		
		_canAttack = false;
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_damageDealt = false;
		_attackCooldown = AttackCooldownDuration;
		
		// Transition to attack state
		TransitionToState(EnemyState.NormalAttack);
		
		// Start the attack animation and damage sequence
		StartPlayerNormalAttackSequence();
	}

	public virtual void PerformPlayerSpecialAttack()
	{
		GD.Print($"PerformPlayerSpecialAttack called - CanAttack: {CanAttack()}, Attacking: {_isAttacking}");
		
		if (!CanAttack()) 
		{
			GD.Print("Cannot perform special attack: conditions not met");
			return;
		}
		
		_canAttack = false;
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_damageDealt = false;
		_attackCooldown = SpecialAttackCooldownDuration;
		
		// Transition to special attack state
		TransitionToState(EnemyState.SpecialAttack);
		
		// Start the special attack animation and damage sequence
		StartPlayerSpecialAttackSequence();
	}

	protected virtual void StartPlayerNormalAttackSequence()
	{
		// Lock movement during attack
		_isMovementLocked = true;
		
		PlayAnimation("normal_attack");
		UpdateNormalAttackAreaFacing();
		
		GD.Print("Starting player normal attack sequence - movement locked");
		
		// Enable damage after a short delay
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => {
			GD.Print("Sword normal attack - enabling damage area");
			DebugAttackAreas();
			if (_isAttacking && _normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", true);
				GD.Print("Normal attack damage enabled");
				
				// Disable after damage window
				var disableTimer = GetTree().CreateTimer(0.2f);
				disableTimer.Timeout += () => {
					if (_normalAttackArea != null)
					{
						_normalAttackArea.SetDeferred("monitoring", false);
						GD.Print("Normal attack damage disabled");
					}
				};
			}
		};
		
		// End attack after total duration
		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => {
			_isMovementLocked = false; // Unlock movement
			EndPlayerAttack();
		};
	}

	protected virtual void StartPlayerSpecialAttackSequence()
	{
		// Lock movement during attack
		_isMovementLocked = true;
		
		PlayAnimation("special_attack");
		UpdateSpecialAttackAreaFacing();
		
		GD.Print("Starting player special attack sequence - movement locked");
		
		// Enable damage after a short delay
		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () => {
			if (_isAttacking && _specialAttackArea != null)
			{
				_specialAttackArea.SetDeferred("monitoring", true);
				GD.Print("Special attack damage enabled");
				
				// Disable after damage window
				var disableTimer = GetTree().CreateTimer(0.3f);
				disableTimer.Timeout += () => {
					if (_specialAttackArea != null)
					{
						_specialAttackArea.SetDeferred("monitoring", false);
						GD.Print("Special attack damage disabled");
					}
				};
			}
		};
		
		// End attack after total duration
		var endTimer = GetTree().CreateTimer(1.5f);
		endTimer.Timeout += () => {
			_isMovementLocked = false; // Unlock movement
			EndPlayerAttack();
		};
	}

	protected virtual void EndPlayerAttack()
	{
		GD.Print("Ending player attack and unlocking movement");
		
		_isAttacking = false;
		_isAttackingUninterruptible = false;
		_isMovementLocked = false; // Ensure movement is unlocked
		_damageDealt = false;
		
		// Ensure attack areas are disabled
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
		
		// Return to idle state for player control
		TransitionToState(EnemyState.Idle);
		
		// Re-enable attacking after cooldown
		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { 
			_canAttack = true; 
			GD.Print("Attack cooldown finished - can attack again");
		};
	}

	#endregion

	#region Utility Methods

	protected virtual bool CanSeeTarget()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			return false;
		}
		return GlobalPosition.DistanceTo(_target.GlobalPosition) <= DetectionRange;
	}

	protected virtual void PlayAnimation(string animationName)
	{
		if (_animatedSprite != null && _animatedSprite.Animation != animationName)
		{
			_animatedSprite.Play(animationName);
		}
	}

	protected virtual void FindTarget()
	{
		// FIXED: Clear current target first to reassess
		_target = null;
		
		// Find appropriate targets based on current group membership
		if (IsInGroup("Enemies"))
		{
			// Regular enemies target: Player, PlayerControlled, Allies, SummonedMonsters
			FindTargetInGroups(new[] { "Player", "PlayerControlled", "Allies", "SummonedMonsters" });
		}
		else if (IsInGroup("PlayerControlled") || IsInGroup("Allies") || IsInGroup("SummonedMonsters"))
		{
			// Player-controlled entities target: Enemies only
			FindTargetInGroups(new[] { "Enemies" });
		}
		
		// If no target found through groups, try direct search
		if (_target == null)
		{
			FindNearestValidTarget();
		}
	}

	protected void FindTargetInGroups(string[] targetGroups)
	{
		Node2D nearestTarget = null;
		float nearestDistance = DetectionRange;
		
		foreach (string group in targetGroups)
		{
			var targets = GetTree().GetNodesInGroup(group);
			foreach (Node node in targets)
			{
				if (node is Node2D target && IsValidTarget(target))
				{
					float distance = GlobalPosition.DistanceTo(target.GlobalPosition);
					if (distance < nearestDistance)
					{
						nearestDistance = distance;
						nearestTarget = target;
					}
				}
			}
		}
		
		_target = nearestTarget;
		
		if (_target != null)
		{
			GD.Print($"{Name} found target: {_target.Name} in groups: {string.Join(", ", targetGroups)}");
		}
	}

	protected virtual void FindNearestValidTarget()
	{
		// Fallback method to find any valid target
		var allBodies = GetTree().GetNodesInGroup("Player");
		allBodies.AddRange(GetTree().GetNodesInGroup("Enemies"));
		allBodies.AddRange(GetTree().GetNodesInGroup("Allies"));
		allBodies.AddRange(GetTree().GetNodesInGroup("SummonedMonsters"));
		allBodies.AddRange(GetTree().GetNodesInGroup("PlayerControlled"));
		
		Node2D nearestTarget = null;
		float nearestDistance = DetectionRange;
		
		foreach (Node node in allBodies)
		{
			if (node is Node2D target && IsValidTarget(target) && CanTargetBasedOnGroups(target))
			{
				float distance = GlobalPosition.DistanceTo(target.GlobalPosition);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearestTarget = target;
				}
			}
		}
		
		_target = nearestTarget;
	}

	protected virtual bool CanTargetBasedOnGroups(Node2D potentialTarget)
	{
		if (IsInGroup("Enemies"))
		{
			// Enemies can target player-controlled entities and allies
			return potentialTarget.IsInGroup("Player") || 
				   potentialTarget.IsInGroup("PlayerControlled") || 
				   potentialTarget.IsInGroup("Allies") || 
				   potentialTarget.IsInGroup("SummonedMonsters");
		}
		else if (IsInGroup("PlayerControlled") || IsInGroup("Allies") || IsInGroup("SummonedMonsters"))
		{
			// Player-controlled entities target enemies
			return potentialTarget.IsInGroup("Enemies");
		}
		
		return false;
	}

	protected virtual bool IsValidTarget(Node2D potentialTarget)
	{
		return potentialTarget != null && 
			   potentialTarget != this && 
			   !potentialTarget.IsQueuedForDeletion() &&
			   potentialTarget.HasMethod("TakeDamage") &&
			   !IsSameControllerAs(potentialTarget);
	}

	protected virtual bool IsSameControllerAs(Node2D other)
	{
		// Check if both are player-controlled
		bool thisPlayerControlled = IsInGroup("Player") || IsInGroup("PlayerControlled") || 
									IsInGroup("Allies") || IsInGroup("SummonedMonsters");
		bool otherPlayerControlled = other.IsInGroup("Player") || other.IsInGroup("PlayerControlled") || 
									 other.IsInGroup("Allies") || other.IsInGroup("SummonedMonsters");
		
		// Check if both are enemies
		bool thisEnemy = IsInGroup("Enemies");
		bool otherEnemy = other.IsInGroup("Enemies");
		
		// Same controller if both player-controlled or both enemies
		return (thisPlayerControlled && otherPlayerControlled) || (thisEnemy && otherEnemy);
	}

	public virtual void UpdateFacing(float directionX)
	{
		if (Mathf.Abs(directionX) < 0.1f) return;

		bool newFacingRight = directionX > 0;
		if (newFacingRight != _facingRight)
		{
			_facingRight = newFacingRight;
			if (_animatedSprite != null)
				_animatedSprite.FlipH = !_facingRight;
			
			// Update attack areas immediately
			UpdateNormalAttackAreaFacing();
			UpdateSpecialAttackAreaFacing();
		}
	}

	public void UpdateAttackAreaFacing(Area2D area)
	{
		if (area == null) return;
		
		// Store original transform data if not already stored
		if (!area.HasMeta("original_position"))
		{
			area.SetMeta("original_position", area.Position);
		}
		if (!area.HasMeta("original_scale"))
		{
			area.SetMeta("original_scale", area.Scale);
		}
		
		Vector2 originalPos = area.GetMeta("original_position").AsVector2();
		Vector2 originalScale = area.GetMeta("original_scale").AsVector2();
		
		if (_facingRight)
		{
			// Facing right - use original position and scale
			area.Position = originalPos;
			area.Scale = originalScale;
		}
		else
		{
			// Facing left - flip both position and scale
			area.Position = new Vector2(-originalPos.X, originalPos.Y);
			area.Scale = new Vector2(-originalScale.X, originalScale.Y);
		}
		
		// Force update collision shape
		var collisionShape = area.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collisionShape != null)
		{
			collisionShape.SetDeferred("disabled", false);
		}
		
		GD.Print($"Updated {area.Name} facing - Position: {area.Position}, Scale: {area.Scale}, FacingRight: {_facingRight}");
	}

	protected void UpdateNormalAttackAreaFacing()
	{
		UpdateAttackAreaFacing(_normalAttackArea);
	}

	protected void UpdateSpecialAttackAreaFacing()
	{
		UpdateAttackAreaFacing(_specialAttackArea);
	}

	#endregion

	#region Damage and Combat

	public virtual void TakeDamage(int damage)
	{
		if (IsDead()) return;

		_currentHealth -= damage;
		GD.Print($"{Name} took {damage} damage. Health: {_currentHealth}/{MaxHealth}");

		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			// Flash red
			_animatedSprite.Modulate = Colors.Red;
			var colorTimer = GetTree().CreateTimer(0.5f);
			colorTimer.Timeout += () => { 
				if (_animatedSprite != null)
					_animatedSprite.Modulate = Colors.White; 
			};
		}
	}

	public virtual void ApplyStun(float duration)
	{
		if (IsDead()) return;

		_isStunned = true;
		_stunDuration = duration;
		_isAttackingUninterruptible = false;
		
		// Disable attack areas
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
		
		TransitionToState(EnemyState.Stunned);
		
		// Flash blue
		_animatedSprite.Modulate = Colors.Blue;
		var colorTimer = GetTree().CreateTimer(0.2f);
		colorTimer.Timeout += () => { 
			if (_animatedSprite != null)
				_animatedSprite.Modulate = Colors.White; 
		};
	}

	public virtual void AddKnockback(Vector2 force)
	{
		if (IsDead()) return;
		
		if (_isAttackingUninterruptible)
			force *= 0.3f;
		
		Velocity = new Vector2(Velocity.X + force.X, Velocity.Y + force.Y);
	}

	protected virtual void DealDamageToTarget(Node2D target, int damage)
	{
		if (target == null || _damageDealt) return;

		_damageDealt = true;
		target.Call("TakeDamage", damage);
		GD.Print($"{Name} hit {target.Name} for {damage} damage!");

		if (target.HasMethod("AddKnockback"))
		{
			Vector2 knockbackDirection = (target.GlobalPosition - GlobalPosition).Normalized();
			target.Call("AddKnockback", knockbackDirection * 150);
		}
	}

	protected virtual void Die()
	{
		// CRITICAL: Check if this enemy is currently possessed and force release
		if (_isPlayerControlled)
		{
			GD.Print($"{Name} is possessed and dying - forcing player release");
			
			// Find the player and force them to release this enemy
			var players = GetTree().GetNodesInGroup("Player");
			if (players.Count > 0)
			{
				var player = players[0];
				if (player.HasMethod("OnPossessedEnemyDied"))
				{
					// Call special method to handle possessed enemy death
					player.Call("OnPossessedEnemyDied", this);
				}
				else if (player.HasMethod("ReleaseTarget"))
				{
					// Fallback to regular release
					player.Call("ReleaseTarget");
				}
			}
		}
		
		TransitionToState(EnemyState.Dead);
		_isAttackingUninterruptible = false;
		_collisionShape.SetDeferred("disabled", true);
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
		
		PlayAnimation("idle");
		
		// IMPORTANT: Delay the cleanup to allow collection to complete
		// If possessed, give more time for collection
		float deathDelay = _isPlayerControlled ? 2.0f : 1.0f;
		var deathTimer = GetTree().CreateTimer(deathDelay);
		deathTimer.Timeout += () => { 
			if (!IsQueuedForDeletion())
			{
				GD.Print($"{Name} cleanup timer triggered - removing from scene");
				QueueFree(); 
			}
		};
	}

	#endregion

	#region Player Control System

	public virtual void SetIsControlledByPlayer(bool controlled)
	{
		_isPlayerControlled = controlled;
		
		if (controlled)
		{
			// Reset attack state and target when taking control
			ResetAttackState();
			_target = null; // Clear current target
			TransitionToState(EnemyState.Idle);
			SetProcess(false); // Disable AI processing
			GD.Print($"{Name} now controlled by player - AI disabled, target cleared");
		}
		else
		{
			// Re-enable AI and force target search
			SetProcess(true);
			_target = null; // Clear target to force new search
			TransitionToState(EnemyState.Idle); // Will transition to chase when target found
			GD.Print($"{Name} AI enabled - will search for new targets");
		}
	}

	public virtual void ResetAttackState()
	{
		_isAttacking = false;
		_isAttackingUninterruptible = false;
		_canAttack = true;
		_damageDealt = false;
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
	}

	public virtual void UpdateTimersPlayerControlled(float delta)
	{
		if (_attackCooldown > 0)
		{
			_attackCooldown -= delta;
			// Don't automatically re-enable attacking here for player control
		}

		if (_isStunned && _stunDuration > 0)
		{
			_stunDuration -= delta;
			if (_stunDuration <= 0)
			{
				_isStunned = false;
			}
		}
	}

	#endregion

	#region Event Handlers

	protected virtual void OnAnimationFinished()
	{
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_specialAttackArea != null)
			_specialAttackArea.SetDeferred("monitoring", false);
	}

	protected virtual void OnNormalAttackAreaEntered(Node2D body)
	{
		if (ShouldDamageTarget(body))
		{
			DealDamageToTarget(body, Damage);
			_normalAttackArea.SetDeferred("monitoring", false);
		}
	}

	protected virtual void OnSpecialAttackAreaEntered(Node2D body)
	{
		if (ShouldDamageTarget(body))
		{
			DealDamageToTarget(body, SpecialDamage);
			_specialAttackArea.SetDeferred("monitoring", false);
		}
	}

	protected virtual bool ShouldDamageTarget(Node2D body)
	{
		if (body == null || body == this || _damageDealt) return false;
		if (!body.HasMethod("TakeDamage")) return false;

		// FIXED: Simplified and more reliable group-based targeting logic
		bool canDamage = false;
		
		// Get this entity's primary allegiance
		bool thisIsEnemy = IsInGroup("Enemies");
		bool thisIsPlayerSide = IsInGroup("Player") || IsInGroup("PlayerControlled") || 
								IsInGroup("Allies") || IsInGroup("SummonedMonsters");
		
		// Get target's primary allegiance  
		bool targetIsEnemy = body.IsInGroup("Enemies");
		bool targetIsPlayerSide = body.IsInGroup("Player") || body.IsInGroup("PlayerControlled") || 
								  body.IsInGroup("Allies") || body.IsInGroup("SummonedMonsters");
		
		// Damage rules: enemies vs player-side, player-side vs enemies
		if (thisIsEnemy && targetIsPlayerSide)
		{
			canDamage = true;
		}
		else if (thisIsPlayerSide && targetIsEnemy)
		{
			canDamage = true;
		}
		
		// Additional safety check: never damage same entity type
		if (canDamage)
		{
			// Don't damage if both are the same specific type
			bool bothEnemies = thisIsEnemy && targetIsEnemy;
			bool bothPlayerSide = thisIsPlayerSide && targetIsPlayerSide;
			
			if (bothEnemies || bothPlayerSide)
			{
				canDamage = false;
			}
		}
		
		GD.Print($"{Name} damage check on {body.Name}: CanDamage={canDamage}, " +
				$"ThisEnemy={thisIsEnemy}, ThisPlayerSide={thisIsPlayerSide}, " +
				$"TargetEnemy={targetIsEnemy}, TargetPlayerSide={targetIsPlayerSide}");
		
		return canDamage;
	}
	
	public virtual void ForceTargetClear()
	{
		if (_target != null)
		{
			GD.Print($"{Name} forced to clear target: {_target.Name}");
			_target = null;
			
			// If currently in chase state, transition to idle to force new target search
			if (_currentState == EnemyState.Chase)
			{
				TransitionToState(EnemyState.Idle);
			}
			
			// If attacking and interruptible, stop the attack
			if (_isAttacking && !_isAttackingUninterruptible)
			{
				EndAttack();
			}
		}
	}

	// Add this debug method to check collision settings
	protected void DebugAttackAreas()
	{
		GD.Print($"=== ATTACK AREA DEBUG for {Name} ===");
		
		if (_normalAttackArea != null)
		{
			GD.Print($"Normal Attack Area - Monitoring: {_normalAttackArea.Monitoring}, " +
					$"Layer: {_normalAttackArea.CollisionLayer}, Mask: {_normalAttackArea.CollisionMask}");
			
			var collision = _normalAttackArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			GD.Print($"Normal Attack Collision - Exists: {collision != null}, " +
					$"Disabled: {collision?.Disabled}, Shape: {collision?.Shape}");
		}
		
		if (_specialAttackArea != null)
		{
			GD.Print($"Special Attack Area - Monitoring: {_specialAttackArea.Monitoring}, " +
					$"Layer: {_specialAttackArea.CollisionLayer}, Mask: {_specialAttackArea.CollisionMask}");
					
			var collision = _specialAttackArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			GD.Print($"Special Attack Collision - Exists: {collision != null}, " +
					$"Disabled: {collision?.Disabled}, Shape: {collision?.Shape}");
		}
		
		// Also debug this enemy's collision
		GD.Print($"Enemy Body - Layer: {CollisionLayer}, Mask: {CollisionMask}");
	}

	#endregion

	#region Public API

	public bool IsDead() => _currentHealth <= 0;
	public bool CanBePossessed() => CurrentHealth <= 0 || (float)CurrentHealth / MaxHealth <= PossessThresholdF;
	public bool CanAttack() => _canAttack && _attackCooldown <= 0 && !_isAttacking && !_isAttackingUninterruptible && !_isStunned;
	public bool IsAttacking() => _isAttacking || _isAttackingUninterruptible;

	// Getters for Player class
	public int GetMaxHP() => MaxHealth;
	public int GetHP() => CurrentHealth;
	public void SetHP(int hp) => CurrentHealth = Mathf.Clamp(hp, 0, MaxHealth);
	public int GetDamage() => Damage;
	public float GetPossessThresholdF() => PossessThresholdF;
	public uint GetOriginalCollisionLayer() => _originalCollisionLayer;
	public uint GetOriginalCollisionMask() => _originalCollisionMask;
	
	public float GetSpeed() => Speed;
	public float GetJumpVelocity() => JumpVelocity;
	public float GetGravity() => gravity;
	
	public void SetOriginalCollisionLayer(uint layer, uint mask)
	{
		_originalCollisionLayer = layer;
		_originalCollisionMask = mask;
	}

	#endregion

	public override void _Draw()
	{
		if (Engine.IsEditorHint()) return;
		
		if (OS.IsDebugBuild())
		{
			DrawCircle(Vector2.Zero, DetectionRange, Colors.Yellow, false, 1.0f);
		}
	}
}
