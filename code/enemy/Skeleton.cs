using Godot;

public partial class Skeleton : CharacterBody2D
{
	[Export] public float Speed = 250.0f;
	[Export] public float JumpVelocity = -400.0f;
	[Export] public float AttackRange = 100.0f;
	[Export] public int MaxHealth = 50;
	[Export] public int Damage = 10;
	[Export] public float DetectionRange = 300.0f;
	
	// Components
	private AnimatedSprite2D _animatedSprite;
	private CollisionShape2D _collisionShape;
	private Area2D _attackArea;
	private CollisionShape2D _attackCollision;
	
	// State Management
	public enum SkeletonState
	{
		Idle,
		Follow,     // Theo player
		Attack,     // Tấn công enemy
		Attacking,  // Đang trong animation attack
		Dead
	}
	
	// Combat và AI Variables
	private SkeletonState _currentState = SkeletonState.Idle;
	private int _currentHealth;
	private bool _facingRight = true;
	private bool _canAttack = true;
	private float _attackCooldown = 0.0f;
	private bool _damageDealt = false;
	private bool _isPossessed = false; // Được player điều khiển
	private bool _isAttacking = false; // Flag để track attack animation
	
	// Target system
	private Node2D _owner; // Player sở hữu
	private Node2D _target; // Enemy target
	private float _followDistance = 80.0f; // Khoảng cách theo player
	
	// Physics
	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	// Possession properties
	[Export] public float PossessThresholdF = 1.0f; // Dễ nhập hơn enemy
	public int CurrentHealth { get { return _currentHealth; } set { _currentHealth = value; } }

	public override void _Ready()
	{
		InitializeComponents();
		InitializeStats();
		ConnectSignals();
		
		_animatedSprite.Play("idle");
		
		// Thêm vào group để có thể tìm thấy
		AddToGroup("Skeletons");
		AddToGroup("Allies"); // Đồng minh của player
		
		// Collision settings - khác với enemy
		CollisionLayer = 1u << 5; // Skeleton layer (bit 5)
		CollisionMask = (1u << 1) | (1u << 2); // Va chạm enemy + world
		
		GD.Print($"Skeleton initialized - Health: {MaxHealth}, Damage: {Damage}");
	}
	
	public override void _Input(InputEvent @event)
	{
		// Chỉ xử lý input khi được possessed
		if (!_isPossessed) return;
		
		// Không cho phép input khác khi đang attack
		if (_isAttacking) return;
	}


	private void InitializeComponents()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		// Attack area
		_attackArea = GetNodeOrNull<Area2D>("AttackArea");
		if (_attackArea == null)
		{
			GD.PrintErr("AttackArea not found! Please add it to the scene.");
		}
		else
		{
			_attackCollision = _attackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_attackArea.Monitoring = false;
			_attackArea.BodyEntered += OnAttackAreaEntered;
		}
	}

	private void InitializeStats()
	{
		_currentHealth = MaxHealth;
	}

	private void ConnectSignals()
	{
		if (_animatedSprite.IsConnected("animation_finished", new Callable(this, nameof(OnAnimationFinished))))
		{
			_animatedSprite.Disconnect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
		}
		_animatedSprite.Connect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
	}
	
	public void SetOwner(Node2D owner)
	{
		_owner = owner;
		GD.Print($"Skeleton owner set to: {owner.Name}");
	}
	
	public void SetPossessed(bool possessed)
	{
		_isPossessed = possessed;
		if (possessed)
		{
			_currentState = SkeletonState.Idle;
			SetProcessInput(true); // Bật input processing
			GD.Print("Skeleton is now possessed by player - Input enabled");
		}
		else
		{
			_currentState = SkeletonState.Follow;
			SetProcessInput(false); // Tắt input processing
			GD.Print("Skeleton is no longer possessed - Input disabled, returning to follow mode");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_currentState == SkeletonState.Dead)
			return;

		// Update timers
		UpdateTimers((float)delta);

		// Apply gravity
		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * (float)delta);

		// Xử lý input nếu được possessed
		if (_isPossessed)
		{
			HandlePossessedInput();
		}
		else
		{
			// AI tự động
			ProcessAI();
		}

		MoveAndSlide();
	}
	
	private void HandlePossessedInput()
	{
		// Không cho phép input khác khi đang attack
		if (_isAttacking) return;
		
		Vector2 velocity = Velocity;
		
		// Movement input từ player
		float direction = Input.GetAxis("move_left", "move_right");
		if (direction != 0)
		{
			velocity.X = direction * Speed;
			UpdateFacing(direction);
			_animatedSprite.Play("run");
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
			if (IsOnFloor())
				_animatedSprite.Play("idle");
		}
		
		// Jump input
		if (Input.IsActionPressed("move_up") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}
		
		// Attack input
		if (Input.IsActionJustPressed("attack") && _canAttack && _attackCooldown <= 0)
		{
			StartAttack();
		}
		
		Velocity = velocity;
	}

	private void UpdateTimers(float delta)
	{
		if (_attackCooldown > 0)
			_attackCooldown -= delta;
	}

	private void ProcessAI()
	{
		switch (_currentState)
		{
			case SkeletonState.Idle:
				HandleIdleState();
				break;
			case SkeletonState.Follow:
				HandleFollowState();
				break;
			case SkeletonState.Attack:
				HandleAttackState();
				break;
			case SkeletonState.Attacking:
				HandleAttackingState();
				break;
		}
	}

	private void HandleIdleState()
	{
		if (_isAttacking) return;
		
		Velocity = new Vector2(0, Velocity.Y);
		_animatedSprite.Play("idle");

		// Tìm owner để follow
		if (_owner != null)
		{
			float distanceToOwner = GlobalPosition.DistanceTo(_owner.GlobalPosition);
			if (distanceToOwner > _followDistance)
			{
				_currentState = SkeletonState.Follow;
			}
		}
		
		// Tìm enemy để attack
		FindNearestEnemy();
		if (_target != null)
		{
			_currentState = SkeletonState.Attack;
		}
	}

	private void HandleFollowState()
	{
		if (_isAttacking) return;
		
		if (_owner == null)
		{
			_currentState = SkeletonState.Idle;
			return;
		}

		float distanceToOwner = GlobalPosition.DistanceTo(_owner.GlobalPosition);
		
		// Nếu đã gần owner thì dừng lại
		if (distanceToOwner <= _followDistance)
		{
			_currentState = SkeletonState.Idle;
			return;
		}

		// Di chuyển về phía owner
		Vector2 direction = (_owner.GlobalPosition - GlobalPosition).Normalized();
		Velocity = new Vector2(direction.X * Speed * 0.8f, Velocity.Y);
		UpdateFacing(direction.X);
		_animatedSprite.Play("run");
		
		// Kiểm tra enemy gần
		FindNearestEnemy();
		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= DetectionRange)
		{
			_currentState = SkeletonState.Attack;
		}
	}

	private void HandleAttackState()
	{
		if (_isAttacking) return;
		
		if (_target == null || _target.IsQueuedForDeletion())
		{
			_target = null;
			_currentState = SkeletonState.Follow;
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_target.GlobalPosition);
		
		// Mất target nếu quá xa
		if (distanceToTarget > DetectionRange * 2.0f)
		{
			_target = null;
			_currentState = SkeletonState.Follow;
			return;
		}

		// Attack nếu trong range
		if (distanceToTarget <= AttackRange && _canAttack && _attackCooldown <= 0)
		{
			Vector2 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			UpdateFacing(directionToTarget.X);
			StartAttack();
			return;
		}

		// Di chuyển về phía target
		if (distanceToTarget > AttackRange)
		{
			Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
			Velocity = new Vector2(direction.X * Speed, Velocity.Y);
			UpdateFacing(direction.X);
			_animatedSprite.Play("run");
		}
		else
		{
			Velocity = new Vector2(0, Velocity.Y);
			_animatedSprite.Play("idle");
		}
	}
	
	private void HandleAttackingState()
	{
		// Dừng di chuyển khi đang attack
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void StartAttack()
	{
		_canAttack = false;
		_damageDealt = false;
		_isAttacking = true;
		_currentState = SkeletonState.Attacking;
		_animatedSprite.Play("attack");
		
		// Enable damage sau delay ngắn
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => EnableAttackDamage();
		
		// Cooldown
		_attackCooldown = 1.5f;
		
		GD.Print("Skeleton attack started");
	}
	
	private void UpdateAttackAreaFacing()
	{
		if (_attackArea == null) return;
		
		Vector2 scale = _attackArea.Scale;
		if (!_facingRight)
		{
			_attackArea.Scale = new Vector2(-Mathf.Abs(scale.X), scale.Y);
		}
		else
		{
			_attackArea.Scale = new Vector2(Mathf.Abs(scale.X), scale.Y);
		}
	}

	private void EnableAttackDamage()
	{
		if (_damageDealt || _attackArea == null || !_isAttacking) return;

		UpdateAttackAreaFacing();
		_attackArea.SetDeferred("monitoring", true);
		GD.Print("Skeleton attack damage enabled");

		// Auto-disable sau thời gian ngắn
		var disableTimer = GetTree().CreateTimer(0.2f);
		disableTimer.Timeout += () => {
			if (_attackArea != null)
			{
				_attackArea.SetDeferred("monitoring", false);
				GD.Print("Skeleton attack damage disabled");
			}
		};
	}

	private void OnAttackAreaEntered(Node2D body)
	{
		// Chỉ tấn công enemy, không tấn công player hoặc skeleton khác
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _attackArea.Monitoring && _isAttacking)
		{
			// Kiểm tra không phải player hoặc ally
			if (!body.IsInGroup("Player") && !body.IsInGroup("Allies"))
			{
				_damageDealt = true;
				
				body.Call("TakeDamage", Damage);
				GD.Print($"*** Skeleton HIT {body.Name} for {Damage} damage! ***");

				// Light knockback
				if (body.HasMethod("AddKnockback"))
				{
					Vector2 knockbackDirection = (body.GlobalPosition - GlobalPosition).Normalized();
					body.Call("AddKnockback", knockbackDirection * 200);
				}
				
				_attackArea.SetDeferred("monitoring", false);
			}
		}
	}

	private void OnAnimationFinished()
	{
		string currentAnimation = _animatedSprite.Animation;
		
		if (_attackArea != null)
			_attackArea.SetDeferred("monitoring", false);
			
		// Reset về idle sau attack
		if (currentAnimation == "attack")
		{
			_isAttacking = false;
			_canAttack = true;
			
			if (_isPossessed)
			{
				_animatedSprite.Play("idle");
			}
			else
			{
				// Quay về AI state phù hợp
				if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= AttackRange && _attackCooldown <= 0)
				{
					_currentState = SkeletonState.Attack;
				}
				else if (_target != null)
				{
					_currentState = SkeletonState.Attack;
				}
				else
				{
					_currentState = SkeletonState.Follow;
				}
				_animatedSprite.Play("idle");
			}
		}
	}

	private void FindNearestEnemy()
	{
		Node2D nearest = null;
		float nearestDistance = DetectionRange;
		
		// Tìm enemy gần nhất
		foreach (Node2D enemy in GetTree().GetNodesInGroup("Enemies"))
		{
			if (enemy.IsQueuedForDeletion()) continue;
			
			float distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = enemy;
			}
		}
		
		_target = nearest;
		if (_target != null)
		{
			GD.Print($"Skeleton found target: {_target.Name} at distance: {nearestDistance}");
		}
	}

	private void UpdateFacing(float directionX)
	{
		if (Mathf.Abs(directionX) < 0.1f) return;

		if (directionX > 0 && !_facingRight)
		{
			_facingRight = true;
			_animatedSprite.FlipH = false;
		}
		else if (directionX < 0 && _facingRight)
		{
			_facingRight = false;
			_animatedSprite.FlipH = true;
		}
	}

	// Public methods cho interaction
	public void TakeDamage(int damage)
	{
		if (_currentState == SkeletonState.Dead) return;

		_currentHealth -= damage;
		GD.Print($"Skeleton took {damage} damage. Health: {_currentHealth}/{MaxHealth}");

		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			// Visual damage feedback
			_animatedSprite.Modulate = Colors.Red;
			var colorTimer = GetTree().CreateTimer(0.15f);
			colorTimer.Timeout += () => { 
				if (_animatedSprite != null)
					_animatedSprite.Modulate = Colors.White; 
			};
		}
	}

	public void AddKnockback(Vector2 force)
	{
		if (_currentState == SkeletonState.Dead) return;
		
		Velocity = new Vector2(Velocity.X + force.X, Velocity.Y + force.Y);
		GD.Print($"Skeleton knockback applied: {force}");
	}

	private void Die()
	{
		_currentState = SkeletonState.Dead;
		_isAttacking = false;
		_animatedSprite.Play("die"); // hoặc "death" nếu có animation
		_collisionShape.SetDeferred("disabled", true);
		
		if (_attackArea != null)
			_attackArea.SetDeferred("monitoring", false);
		
		GD.Print("Skeleton died!");
		
		// Thông báo cho player nếu đang possessed
		if (_isPossessed && _owner != null && _owner.HasMethod("OnSkeletonDeath"))
		{
			_owner.Call("OnSkeletonDeath", this);
		}
		
		// Xóa sau 3 giây
		var deathTimer = GetTree().CreateTimer(3.0f);
		deathTimer.Timeout += () => { QueueFree(); };
	}

	// Debug visualization
	public override void _Draw()
	{
		if (Engine.IsEditorHint()) return;
		
		if (OS.IsDebugBuild())
		{
			// Vẽ detection range
			DrawCircle(Vector2.Zero, DetectionRange, Colors.Green, false, 1.0f);
			// Vẽ attack range
			DrawCircle(Vector2.Zero, AttackRange, Colors.Red, false, 2.0f);
		}
	}
}
