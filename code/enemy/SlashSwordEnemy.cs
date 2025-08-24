using Godot;

public partial class SlashSwordEnemy : CharacterBody2D
{
	[Export] public float Speed = 300.0f;
	[Export] public float JumpVelocity = -400.0f;
	[Export] public float AttackRange = 158.0f;
	[Export] public float HeavyAttackRange = 200.0f;
	[Export] public float DetectionRange = 640.0f;
	[Export] public int MaxHealth = 100;
	[Export] public int Damage = 25;
	[Export] public int HeavyDamage = 50;

	// Components - Sử dụng Area2D từ scene
	private AnimatedSprite2D _animatedSprite;
	private CollisionShape2D _collisionShape;
	private Area2D _normalAttackArea;      // Area2D cho đòn thường
	private Area2D _heavyAttackArea;       // Area2D cho đòn mạnh
	private CollisionShape2D _normalAttackCollision;
	private CollisionShape2D _heavyAttackCollision;
	private AudioStreamPlayer2D _normalAttackAudio;
	private AudioStreamPlayer2D _heavyAttackAudio;

	// State Management
	public enum EnemyState
	{
		Idle,
		Patrol,
		Chase,
		TripleAttack,  // Combo 3 đòn ngang
		StrongAttack,  // Đòn bổ thẳng
		Stunned,       // Trạng thái bị khống chế
		Dead
	}

	// Combat Variables
	private EnemyState _currentState = EnemyState.Idle;
	private int _currentHealth;
	private bool _isAttackingUninterruptible = false; // Không thể bị ngắt
	private bool _facingRight = true;
	private bool _canAttack = true;
	private float _attackCooldown = 0.0f;
	private int _currentComboHit = 0; // Đòn hiện tại trong combo (0-2)
	private bool _damageDealt = false;
	private bool _isStunned = false;
	private float _stunDuration = 0.0f;

	// Target và Movement
	private Node2D _target;
	private Vector2 _patrolPointA;
	private Vector2 _patrolPointB;
	private bool _movingToB = true;

	// Timing cho combo attacks
	private float _comboTimer = 0.0f;
	private float _comboDamageWindow = 0.15f; // Thời gian sát thương có hiệu lực

	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	// Add to SlashSwordEnemy.cs and LightArcherEnemy.cs
	[Export] private float PossessThresholdF = 0.2f;
	public int CurrentHealth { get { return _currentHealth; } set { _currentHealth = value; } }

	public override void _Ready()
	{
		InitializeComponents();
		InitializeStats();
		ConnectSignals();
		InitializeAudio();
		_animatedSprite.Play("idle");
		GD.Print($"SlashSwordEnemy initialized - Custom Attack Areas and Audio Ready");
	}

	private void InitializeComponents()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		// Lấy các Area2D từ scene
		_normalAttackArea = GetNodeOrNull<Area2D>("NormalAttackArea");
		_heavyAttackArea = GetNodeOrNull<Area2D>("HeavyAttackArea");
		
		if (_normalAttackArea == null)
		{
			GD.PrintErr("NormalAttackArea not found! Please add it to the scene.");
		}
		else
		{
			_normalAttackCollision = _normalAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_normalAttackArea.Monitoring = false;
			_normalAttackArea.BodyEntered += OnNormalAttackAreaEntered;
		}
		
		if (_heavyAttackArea == null)
		{
			GD.PrintErr("HeavyAttackArea not found! Please add it to the scene.");
		}
		else
		{
			_heavyAttackCollision = _heavyAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_heavyAttackArea.Monitoring = false;
			_heavyAttackArea.BodyEntered += OnHeavyAttackAreaEntered;
		}
	}

	private void InitializeStats()
	{
		_currentHealth = MaxHealth;
		_patrolPointA = GlobalPosition + Vector2.Left * 150;
		_patrolPointB = GlobalPosition + Vector2.Right * 150;
	}

	private void InitializeAudio()
	{
		// Tạo AudioStreamPlayer2D cho normal attack
		_normalAttackAudio = new AudioStreamPlayer2D();
		_normalAttackAudio.Name = "NormalAttackAudio";
		_normalAttackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/normal_attack-slash_sword.mp3");
		AddChild(_normalAttackAudio);

		// Tạo AudioStreamPlayer2D cho heavy attack
		_heavyAttackAudio = new AudioStreamPlayer2D();
		_heavyAttackAudio.Name = "HeavyAttackAudio";
		_heavyAttackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/heavy_attack-slash_sword.mp3");
		AddChild(_heavyAttackAudio);

		// Kiểm tra xem âm thanh có được tải không
		if (_normalAttackAudio.Stream == null)
			GD.PrintErr("Failed to load normal_attack-slash_sword.mp3");
		if (_heavyAttackAudio.Stream == null)
			GD.PrintErr("Failed to load heavy_attack-slash_sword.mp3");
	}

	private void ConnectSignals()
	{
		if (_animatedSprite.IsConnected("animation_finished", new Callable(this, nameof(OnAnimationFinished))))
		{
			_animatedSprite.Disconnect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
		}
		_animatedSprite.Connect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_currentState == EnemyState.Dead)
			return;

		// Update timers
		UpdateTimers((float)delta);

		// Apply gravity
		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * (float)delta);

		// State machine
		ProcessState();

		// Find target if none exists
		if (_target == null)
			FindTarget();

		MoveAndSlide();
	}

	private void UpdateTimers(float delta)
	{
		if (_attackCooldown > 0)
			_attackCooldown -= delta;

		if (_isStunned && _stunDuration > 0)
		{
			_stunDuration -= delta;
			if (_stunDuration <= 0)
			{
				_isStunned = false;
				_currentState = EnemyState.Chase;
				GD.Print("Stun ended - returning to chase");
			}
		}

		// Combo timing
		if (_currentState == EnemyState.TripleAttack && _comboTimer > 0)
		{
			_comboTimer -= delta;
		}
	}

	private void ProcessState()
	{
		switch (_currentState)
		{
			case EnemyState.Idle:
				HandleIdleState();
				break;
			case EnemyState.Patrol:
				HandlePatrolState();
				break;
			case EnemyState.Chase:
				HandleChaseState();
				break;
			case EnemyState.TripleAttack:
				HandleTripleAttackState();
				break;
			case EnemyState.StrongAttack:
				HandleStrongAttackState();
				break;
			case EnemyState.Stunned:
				HandleStunnedState();
				break;
		}
	}

	private void HandleIdleState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		_animatedSprite.Play("idle");

		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= DetectionRange)
		{
			_currentState = EnemyState.Chase;
		}
		else if (_target == null)
		{
			_currentState = EnemyState.Patrol;
		}
	}

	private void HandlePatrolState()
	{
		Vector2 targetPoint = _movingToB ? _patrolPointB : _patrolPointA;
		Vector2 direction = (targetPoint - GlobalPosition).Normalized();

		if (GlobalPosition.DistanceTo(targetPoint) < 30f)
		{
			_movingToB = !_movingToB;
			direction = Vector2.Zero;
		}

		Velocity = new Vector2(direction.X * Speed * 0.6f, Velocity.Y);
		UpdateFacing(direction.X);
		_animatedSprite.Play("run");

		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= DetectionRange)
		{
			_currentState = EnemyState.Chase;
		}
	}

	private void HandleChaseState()
	{
		if (_target == null)
		{
			_currentState = EnemyState.Idle;
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_target.GlobalPosition);

		if (distanceToTarget > DetectionRange * 1.8f)
		{
			_target = null;
			_currentState = EnemyState.Idle;
			return;
		}

		if (distanceToTarget <= AttackRange && _canAttack && _attackCooldown <= 0 && !_isAttackingUninterruptible)
		{
			bool targetMoving = false;
			if (_target.HasMethod("get_velocity"))
			{
				Vector2 targetVelocity = _target.Call("get_velocity").AsVector2();
				targetMoving = targetVelocity.LengthSquared() > 100;
			}

			Vector2 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			UpdateFacing(directionToTarget.X);
				
			if (GD.Randf() < 0.3f)
			{
				StartStrongAttack();
			}
			else
			{
				StartTripleAttack();
			}
			return;
		}

		if (distanceToTarget > AttackRange * 0.9f)
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

	private void HandleTripleAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void HandleStrongAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void HandleStunnedState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		_animatedSprite.Play("idle");
	}

	private void StartTripleAttack()
	{
		_currentState = EnemyState.TripleAttack;
		_isAttackingUninterruptible = true;
		_canAttack = false;
		_currentComboHit = 0;
		_comboTimer = 0.0f;

		GD.Print("=== STARTING TRIPLE ATTACK COMBO ===");
		
		ExecuteComboHit();
	}

	private void ExecuteComboHit()
	{
		if (_currentComboHit >= 3)
		{
			EndTripleAttack();
			return;
		}

		_damageDealt = false;
		_animatedSprite.Play("triple_attack");
		
		bool isHeavyHit = (_currentComboHit == 1);
		string hitType = isHeavyHit ? "HEAVY" : "LIGHT";
		
		GD.Print($"Executing combo hit {_currentComboHit + 1}/3 - {hitType}");

		if (isHeavyHit)
		{
			UpdateHeavyAttackAreaFacing();
		}
		else
		{
			UpdateNormalAttackAreaFacing();
		}

		var damageTimer = GetTree().CreateTimer(0.3f);
		if (isHeavyHit)
		{
			damageTimer.Timeout += () => EnableComboHeavyDamage();
		}
		else
		{
			damageTimer.Timeout += () => EnableComboLightDamage();
		}

		_currentComboHit++;

		if (_currentComboHit < 3)
		{
			var nextHitTimer = GetTree().CreateTimer(1.0f);
			nextHitTimer.Timeout += () => {
				ExecuteComboHit();
			};
		}
		else
		{
			var endTimer = GetTree().CreateTimer(1.0f);
			endTimer.Timeout += () => EndTripleAttack();
		}
	}

	private void EndTripleAttack()
	{
		_isAttackingUninterruptible = false;
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		
		_attackCooldown = 2.5f;
		_canAttack = false;
		
		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };

		_currentState = EnemyState.Chase;
		GD.Print("=== TRIPLE ATTACK COMBO ENDED ===");
	}

	private void StartStrongAttack()
	{
		_currentState = EnemyState.StrongAttack;
		_isAttackingUninterruptible = true;
		_canAttack = false;
		_damageDealt = false;

		GD.Print("=== STARTING STRONG ATTACK (Downward Strike) ===");
		
		_animatedSprite.Play("strong_attack");

		UpdateHeavyAttackAreaFacing();

		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () => EnableHeavyAttackDamage();

		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => EndStrongAttack();
	}

	private void EndStrongAttack()
	{
		_isAttackingUninterruptible = false;
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		_attackCooldown = 2.0f;
		_canAttack = false;

		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };

		_currentState = EnemyState.Chase;
		GD.Print("=== STRONG ATTACK ENDED ===");
	}

	private void UpdateNormalAttackAreaFacing()
	{
		if (_normalAttackArea == null) return;
		
		Vector2 scale = _normalAttackArea.Scale;
		if (!_facingRight)
		{
			_normalAttackArea.Scale = new Vector2(-Mathf.Abs(scale.X), scale.Y);
		}
		else
		{
			_normalAttackArea.Scale = new Vector2(Mathf.Abs(scale.X), scale.Y);
		}
		
		GD.Print($"Normal attack area facing updated - Right: {_facingRight}, Scale: {_normalAttackArea.Scale}");
	}

	private void UpdateHeavyAttackAreaFacing()
	{
		if (_heavyAttackArea == null) return;
		
		Vector2 scale = _heavyAttackArea.Scale;
		if (!_facingRight)
		{
			_heavyAttackArea.Scale = new Vector2(-Mathf.Abs(scale.X), scale.Y);
		}
		else
		{
			_heavyAttackArea.Scale = new Vector2(Mathf.Abs(scale.X), scale.Y);
		}
		
		GD.Print($"Heavy attack area facing updated - Right: {_facingRight}, Scale: {_heavyAttackArea.Scale}");
	}

	private void EnableComboLightDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;

		_normalAttackArea.SetDeferred("monitoring", true);
		GD.Print($"COMBO LIGHT ATTACK DAMAGE ENABLED - Hit {_currentComboHit}/3");
		if (_normalAttackAudio != null && _normalAttackAudio.Stream != null)
			_normalAttackAudio.Play();

		var disableTimer = GetTree().CreateTimer(_comboDamageWindow);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", false);
				GD.Print("Combo light attack damage window closed");
			}
		};
	}

	private void EnableComboHeavyDamage()
	{
		if (_damageDealt || _heavyAttackArea == null) return;

		_heavyAttackArea.SetDeferred("monitoring", true);
		GD.Print($"COMBO HEAVY ATTACK DAMAGE ENABLED - Hit {_currentComboHit}/3");
		if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
			_heavyAttackAudio.Play();

		var disableTimer = GetTree().CreateTimer(_comboDamageWindow);
		disableTimer.Timeout += () => {
			if (_heavyAttackArea != null)
			{
				_heavyAttackArea.SetDeferred("monitoring", false);
				GD.Print("Combo heavy attack damage window closed");
			}
		};
	}

	private void EnableHeavyAttackDamage()
	{
		if (_damageDealt || _heavyAttackArea == null) return;

		_heavyAttackArea.SetDeferred("monitoring", true);
		GD.Print($"HEAVY ATTACK DAMAGE ENABLED - State: {_currentState}");
		if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
			_heavyAttackAudio.Play();

		var disableTimer = GetTree().CreateTimer(_comboDamageWindow);
		disableTimer.Timeout += () => {
			if (_heavyAttackArea != null)
			{
				_heavyAttackArea.SetDeferred("monitoring", false);
				GD.Print("Heavy attack damage window closed");
			}
		};
	}

	private void OnAnimationFinished()
	{
		string currentAnimation = _animatedSprite.Animation;
		GD.Print($"Animation finished: {currentAnimation}");

		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
	}

	private void OnNormalAttackAreaEntered(Node2D body)
	{
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _normalAttackArea.Monitoring)
		{
			_damageDealt = true;
			
			body.Call("TakeDamage", Damage);
			GD.Print($"*** LIGHT HIT TARGET {body.Name} for {Damage} damage! ***");

			if (body.HasMethod("AddKnockback"))
			{
				Vector2 knockbackDirection = (_target.GlobalPosition - GlobalPosition).Normalized();
				body.Call("AddKnockback", knockbackDirection * 150);
			}
			
			_normalAttackArea.SetDeferred("monitoring", false);
		}
	}

	private void OnHeavyAttackAreaEntered(Node2D body)
	{
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _heavyAttackArea.Monitoring)
		{
			_damageDealt = true;
			
			body.Call("TakeDamage", HeavyDamage);
			GD.Print($"*** HEAVY HIT TARGET {body.Name} for {HeavyDamage} damage! ***");

			if (body.HasMethod("AddKnockback"))
			{
				Vector2 knockbackDirection = (_target.GlobalPosition - GlobalPosition).Normalized();
				body.Call("AddKnockback", knockbackDirection * 500);
				GD.Print("Heavy knockback applied!");
			}
			
			_heavyAttackArea.SetDeferred("monitoring", false);
		}
	}

	private void FindTarget()
	{
		var bodies = GetTree().GetNodesInGroup("Player");
		if (bodies.Count > 0)
		{
			Node2D potentialTarget = bodies[0] as Node2D;
			if (potentialTarget != null)
			{
				float distance = GlobalPosition.DistanceTo(potentialTarget.GlobalPosition);
				if (distance <= DetectionRange)
				{
					_target = potentialTarget;
				}
			}
		}
		else
		{
			var root = GetTree().CurrentScene;
			var playerNode = FindPlayerNodeRecursive(root);
			
			if (playerNode != null)
			{
				float distance = GlobalPosition.DistanceTo(playerNode.GlobalPosition);
				if (distance <= DetectionRange)
				{
					_target = playerNode;
				}
			}
		}
	}

	private Node2D FindPlayerNodeRecursive(Node node)
	{
		if (node is CharacterBody2D body && node != this)
		{
			string nodeName = node.Name.ToString().ToLower();
			if (nodeName.Contains("player") || nodeName.Contains("character"))
			{
				return body;
			}
		}
		
		foreach (Node child in node.GetChildren())
		{
			var result = FindPlayerNodeRecursive(child);
			if (result != null)
				return result;
		}
		
		return null;
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

	public void TakeDamage(int damage)
	{
		if (_currentState == EnemyState.Dead) return;

		if (_isAttackingUninterruptible && !_isStunned)
		{
			GD.Print($"Attack cannot be interrupted! Taking {damage} damage but continuing attack.");
		}

		_currentHealth -= damage;
		GD.Print($"Enemy took {damage} damage. Health: {_currentHealth}/{MaxHealth}");

		if (_currentHealth <= 0)
		{
			Die();
		}
		else
		{
			_animatedSprite.Modulate = Colors.Red;
			var colorTimer = GetTree().CreateTimer(0.15f);
			colorTimer.Timeout += () => { 
				if (_animatedSprite != null)
					_animatedSprite.Modulate = Colors.White; 
			};
		}
	}

	public void ApplyStun(float duration)
	{
		if (_currentState == EnemyState.Dead) return;

		GD.Print($"Enemy stunned for {duration} seconds - interrupting attacks!");
		
		_isStunned = true;
		_stunDuration = duration;
		_isAttackingUninterruptible = false;
		_currentState = EnemyState.Stunned;
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		_animatedSprite.Modulate = Colors.Blue;
		var colorTimer = GetTree().CreateTimer(0.2f);
		colorTimer.Timeout += () => { 
			if (_animatedSprite != null)
				_animatedSprite.Modulate = Colors.White; 
		};
	}

	public void AddKnockback(Vector2 force)
	{
		if (_currentState == EnemyState.Dead) return;
		
		if (_isAttackingUninterruptible)
		{
			force *= 0.3f;
		}
		
		Velocity = new Vector2(Velocity.X + force.X, Velocity.Y + force.Y);
		GD.Print($"Knockback applied: {force}");
	}

	private void Die()
	{
		_currentState = EnemyState.Dead;
		_isAttackingUninterruptible = false;
		_animatedSprite.Play("idle");
		_collisionShape.SetDeferred("disabled", true);
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		GD.Print("SlashSwordEnemy died!");
		
		var deathTimer = GetTree().CreateTimer(3.0f);
		deathTimer.Timeout += () => { QueueFree(); };
	}
	
	public bool CanBePossessed()
	{
		return CurrentHealth <= 0 || (float)CurrentHealth / MaxHealth <= PossessThresholdF;
	}

	public void SetIsControlledByPlayer(bool controlled)
	{
		if (controlled)
		{
			_currentState = EnemyState.Idle;
			_isAttackingUninterruptible = false;
		}
		else
		{
			_currentState = EnemyState.Idle;
		}
	}

	public int GetMaxHP()
	{
		return MaxHealth;
	}

	public int GetHP()
	{
		return CurrentHealth;
	}

	public void SetHP(int hp)
	{
		CurrentHealth = Mathf.Clamp(hp, 0, MaxHealth);
	}

	public int GetDamage()
	{
		return Damage;
	}
	
	public float get_PossessThresholdF(){
		return PossessThresholdF;
	}

	public override void _Draw()
	{
		if (Engine.IsEditorHint()) return;
		
		if (OS.IsDebugBuild())
		{
			DrawCircle(Vector2.Zero, DetectionRange, Colors.Yellow, false, 1.0f);
		}
	}
}
