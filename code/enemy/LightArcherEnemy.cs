using Godot;

public partial class LightArcherEnemy : CharacterBody2D
{
	[Export] public float Speed = 300.0f;
	[Export] public float JumpVelocity = -400.0f;
	[Export] public float AttackRange = 1024.0f;
	[Export] public float HeavyAttackRange = 1024.0f;
	[Export] public float DetectionRange = 640.0f;
	[Export] public int MaxHealth = 80;
	[Export] public int Damage = 30;
	[Export] public int HeavyDamage = 25;
	
	// Audio Settings - Kéo thả file audio vào đây!
	[Export] public AudioStream BowReleaseSound;
	[Export] public float SoundVolume = -5.0f;
	[Export] public float PitchVariation = 0.3f;

	// Components - Sử dụng Area2D từ scene
	private AnimatedSprite2D _animatedSprite;
	private CollisionShape2D _collisionShape;
	private Area2D _normalAttackArea;      // Area2D cho đòn thường
	private Area2D _heavyAttackArea;       // Area2D cho đòn mạnh
	private CollisionShape2D _normalAttackCollision;
	private CollisionShape2D _heavyAttackCollision;
	
	// Audio Components
	private AudioStreamPlayer2D _bowReleasePlayer;

	// State Management
	public enum EnemyState
	{
		Idle,
		Patrol,
		Chase,
		HeavyAttack,   // Heavy attack với 3s delay + 3 lần damage
		NormalAttack,  // Normal attack đơn giản
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
	private int _currentHeavyHit = 0; // Đòn hiện tại trong heavy attack (0-2)
	private bool _damageDealt = false;
	private bool _isStunned = false;
	private float _stunDuration = 0.0f;
	private bool _heavyAttackCharging = false;
	private float _heavyAttackChargeTime = 0.0f;

	// Target và Movement
	private Node2D _target;
	private Vector2 _patrolPointA;
	private Vector2 _patrolPointB;
	private bool _movingToB = true;

	// Timing cho heavy attacks
	private float _heavyAttackTimer = 0.0f;
	private float _heavyDamageWindow = 0.15f; // Thời gian sát thương có hiệu lực

	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	
	// Add to SlashSwordEnemy.cs and LightArcherEnemy.cs
	[Export] public float PossessThresholdF = 0.25f;
	public int CurrentHealth { get { return _currentHealth; } set { _currentHealth = value; } }

	public override void _Ready()
	{
		InitializeComponents();
		InitializeAudio();
		InitializeStats();
		ConnectSignals();
		
		_animatedSprite.Play("idle");
		GD.Print($"LightArcherEnemy initialized - Custom Attack Areas & Audio Ready");
	}

	private void InitializeComponents()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		// Lấy các Area2D từ scene (bạn cần tạo chúng trong editor)
		_normalAttackArea = GetNodeOrNull<Area2D>("NormalAttackArea");
		_heavyAttackArea = GetNodeOrNull<Area2D>("HeavyAttackArea");
		
		// Kiểm tra xem Area2D có tồn tại không
		if (_normalAttackArea == null)
		{
			GD.PrintErr("NormalAttackArea not found! Please add it to the scene.");
		}
		else
		{
			_normalAttackCollision = _normalAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			// Tắt monitoring ban đầu
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
			// Tắt monitoring ban đầu
			_heavyAttackArea.Monitoring = false;
			_heavyAttackArea.BodyEntered += OnHeavyAttackAreaEntered;
		}
	}

	private void InitializeAudio()
	{
		// Tìm AudioStreamPlayer2D trong scene hoặc tạo mới
		_bowReleasePlayer = GetNodeOrNull<AudioStreamPlayer2D>("BowReleasePlayer");
		
		if (_bowReleasePlayer == null)
		{
			// Tạo AudioStreamPlayer2D mới
			_bowReleasePlayer = new AudioStreamPlayer2D();
			_bowReleasePlayer.Name = "BowReleasePlayer";
			AddChild(_bowReleasePlayer);
		}
		
		// Sử dụng AudioStream đã được assign qua Inspector
		if (BowReleaseSound != null)
		{
			_bowReleasePlayer.Stream = BowReleaseSound;
			_bowReleasePlayer.VolumeDb = SoundVolume;
			_bowReleasePlayer.PitchScale = 1.0f;
			GD.Print("✅ Bow release audio loaded from Inspector!");
		}
		else
		{
			GD.PrintErr("❌ BowReleaseSound chưa được assign! Kéo thả file audio vào Inspector.");
		}
	}

	private void InitializeStats()
	{
		_currentHealth = MaxHealth;
		_patrolPointA = GlobalPosition + Vector2.Left * 150;
		_patrolPointB = GlobalPosition + Vector2.Right * 150;
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

		// Heavy attack charge timer
		if (_heavyAttackCharging && _heavyAttackChargeTime > 0)
		{
			_heavyAttackChargeTime -= delta;
		}

		// Heavy attack timing
		if (_currentState == EnemyState.HeavyAttack && _heavyAttackTimer > 0)
		{
			_heavyAttackTimer -= delta;
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
			case EnemyState.HeavyAttack:
				HandleHeavyAttackState();
				break;
			case EnemyState.NormalAttack:
				HandleNormalAttackState();
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

		// Check for target
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

		// Lose target if too far
		if (distanceToTarget > DetectionRange * 1.8f)
		{
			_target = null;
			_currentState = EnemyState.Idle;
			return;
		}

		// Attack decision - chỉ tấn công khi có thể và target đang di chuyển hoặc mới vào tầm
		if (distanceToTarget <= AttackRange && _canAttack && _attackCooldown <= 0 && !_isAttackingUninterruptible)
		{
			// Kiểm tra target có đang di chuyển không
			bool targetMoving = false;
			if (_target.HasMethod("get_velocity"))
			{
				Vector2 targetVelocity = _target.Call("get_velocity").AsVector2();
				targetMoving = targetVelocity.LengthSquared() > 100; // Threshold cho movement
			}

			Vector2 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			UpdateFacing(directionToTarget.X);
				
			// 40% chance for heavy attack, 60% for normal attack
			if (GD.Randf() < 0.4f)
			{
				StartHeavyAttack();
			}
			else
			{
				StartNormalAttack();
			}
			return;
		}

		// Move towards target only if not in attack range
		if (distanceToTarget > AttackRange * 0.9f) // Tạo buffer zone
		{
			Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
			Velocity = new Vector2(direction.X * Speed, Velocity.Y);
			UpdateFacing(direction.X);
			_animatedSprite.Play("run");
		}
		else
		{
			// Đã đến gần target, dừng lại và chuẩn bị tấn công
			Velocity = new Vector2(0, Velocity.Y);
			_animatedSprite.Play("idle");
		}
	}

	private void HandleHeavyAttackState()
	{
		// Lock movement during uninterruptible attack
		Velocity = new Vector2(0, Velocity.Y);

		// The heavy attack is handled by timers
		// This state prevents other behaviors while attacking
	}

	private void HandleNormalAttackState()
	{
		// Lock movement during uninterruptible attack
		Velocity = new Vector2(0, Velocity.Y);
		
		// The attack is handled by animation events
		// This state prevents other behaviors while attacking
	}

	private void HandleStunnedState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		_animatedSprite.Play("idle");
		// Stun timer is handled in UpdateTimers()
	}

	private void StartHeavyAttack()
	{
		_currentState = EnemyState.HeavyAttack;
		_isAttackingUninterruptible = true;
		_canAttack = false;
		_currentHeavyHit = 0;
		_heavyAttackCharging = true;
		_heavyAttackChargeTime = 2.0f; // 2 giây delay

		GD.Print("=== STARTING HEAVY ATTACK (2s Charge) ===");
		
		// Play charge animation
		_animatedSprite.Play("heavy_attack");

		// Update facing direction
		UpdateHeavyAttackAreaFacing();

		// Start charge timer - after 2 seconds, execute damage sequence
		var chargeTimer = GetTree().CreateTimer(2.0f);
		chargeTimer.Timeout += () => ExecuteHeavyAttackSequence();
	}

	private void ExecuteHeavyAttackSequence()
	{
		if (_currentState != EnemyState.HeavyAttack) return;

		_heavyAttackCharging = false;
		GD.Print("=== HEAVY ATTACK SEQUENCE STARTED ===");

		// Execute 3 hits with 0.6s intervals
		ExecuteHeavyHit(); // First hit immediately

		// Second hit after 0.6s
		var hit2Timer = GetTree().CreateTimer(0.6f);
		hit2Timer.Timeout += () => ExecuteHeavyHit();

		// Third hit after 1.2s
		var hit3Timer = GetTree().CreateTimer(1.2f);
		hit3Timer.Timeout += () => ExecuteHeavyHit();

		// End attack after all hits
		var endTimer = GetTree().CreateTimer(1.8f);
		endTimer.Timeout += () => EndHeavyAttack();
	}

	private void ExecuteHeavyHit()
	{
		if (_currentState != EnemyState.HeavyAttack) return;

		_currentHeavyHit++;
		_damageDealt = false;

		GD.Print($"Executing heavy hit {_currentHeavyHit}/3");

		// PHÁT ÂM THANH BẮN CUNG CHO HEAVY ATTACK
		PlayBowReleaseSound();

		// Enable damage immediately
		EnableHeavyAttackDamage();
	}

	private void EndHeavyAttack()
	{
		_isAttackingUninterruptible = false;
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		_attackCooldown = 4.0f; // Longer cooldown for heavy attack
		_canAttack = false;
		_heavyAttackCharging = false;
		
		// Re-enable attacking after cooldown
		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };

		_currentState = EnemyState.Chase;
		GD.Print("=== HEAVY ATTACK ENDED ===");
	}

	private void StartNormalAttack()
	{
		_currentState = EnemyState.NormalAttack;
		_isAttackingUninterruptible = true;
		_canAttack = false;
		_damageDealt = false;

		GD.Print("=== STARTING NORMAL ATTACK ===");
		
		_animatedSprite.Play("normal_attack");

		// Cập nhật hướng Area2D theo hướng nhìn
		UpdateNormalAttackAreaFacing();

		// Enable damage at the peak of the attack
		var damageTimer = GetTree().CreateTimer(1.2f);
		damageTimer.Timeout += () => {
			// PHÁT ÂM THANH BẮN CUNG CHO NORMAL ATTACK
			PlayBowReleaseSound();
			EnableNormalAttackDamage();
		};

		// End attack after animation duration
		var endTimer = GetTree().CreateTimer(1.4f);
		endTimer.Timeout += () => EndNormalAttack();
	}

	private void EndNormalAttack()
	{
		_isAttackingUninterruptible = false;
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		
		_attackCooldown = 1.5f;
		_canAttack = false;

		// Re-enable attacking after cooldown
		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };

		_currentState = EnemyState.Chase;
		GD.Print("=== NORMAL ATTACK ENDED ===");
	}

	// PHƯƠNG THỨC PHÁT ÂM THANH BẮN CUNG
	private void PlayBowReleaseSound()
	{
		if (_bowReleasePlayer != null && BowReleaseSound != null)
		{
			// Thêm pitch variation để không bị lặp
			float randomPitch = 1.0f + (GD.Randf() - 0.5f) * PitchVariation;
			_bowReleasePlayer.PitchScale = randomPitch;
			_bowReleasePlayer.Play();
			GD.Print($"🏹 Bow release sound played! (Pitch: {randomPitch:F2})");
		}
		else
		{
			GD.PrintErr("❌ Bow release sound not available! Check Inspector settings.");
		}
	}

	// Cập nhật hướng của Normal Attack Area
	private void UpdateNormalAttackAreaFacing()
	{
		if (_normalAttackArea == null) return;
		
		// Flip toàn bộ Area2D theo hướng nhìn
		Vector2 scale = _normalAttackArea.Scale;
		if (!_facingRight)
		{
			// Hướng trái: flip X scale
			_normalAttackArea.Scale = new Vector2(-Mathf.Abs(scale.X), scale.Y);
		}
		else
		{
			// Hướng phải: scale bình thường
			_normalAttackArea.Scale = new Vector2(Mathf.Abs(scale.X), scale.Y);
		}
		
		GD.Print($"Normal attack area facing updated - Right: {_facingRight}, Scale: {_normalAttackArea.Scale}");
	}

	// Cập nhật hướng của Heavy Attack Area
	private void UpdateHeavyAttackAreaFacing()
	{
		if (_heavyAttackArea == null) return;
		
		// Flip toàn bộ Area2D theo hướng nhìn
		Vector2 scale = _heavyAttackArea.Scale;
		if (!_facingRight)
		{
			// Hướng trái: flip X scale
			_heavyAttackArea.Scale = new Vector2(-Mathf.Abs(scale.X), scale.Y);
		}
		else
		{
			// Hướng phải: scale bình thường
			_heavyAttackArea.Scale = new Vector2(Mathf.Abs(scale.X), scale.Y);
		}
		
		GD.Print($"Heavy attack area facing updated - Right: {_facingRight}, Scale: {_heavyAttackArea.Scale}");
	}

	private void EnableNormalAttackDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;

		_normalAttackArea.SetDeferred("monitoring", true);
		GD.Print($"NORMAL ATTACK DAMAGE ENABLED - State: {_currentState}");

		// Auto-disable damage after brief window
		var disableTimer = GetTree().CreateTimer(_heavyDamageWindow);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", false);
				GD.Print("Normal attack damage window closed");
			}
		};
	}

	private void EnableHeavyAttackDamage()
	{
		if (_damageDealt || _heavyAttackArea == null) return;

		_heavyAttackArea.SetDeferred("monitoring", true);
		GD.Print($"HEAVY ATTACK DAMAGE ENABLED - Hit {_currentHeavyHit}/3");

		// Auto-disable damage after brief window
		var disableTimer = GetTree().CreateTimer(_heavyDamageWindow);
		disableTimer.Timeout += () => {
			if (_heavyAttackArea != null)
			{
				_heavyAttackArea.SetDeferred("monitoring", false);
				GD.Print($"Heavy attack hit {_currentHeavyHit} damage window closed");
			}
		};
	}

	private void OnAnimationFinished()
	{
		string currentAnimation = _animatedSprite.Animation;
		GD.Print($"Animation finished: {currentAnimation}");

		// Ensure damage areas are disabled when animation ends
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
	}

	// Handler cho Normal Attack Area (đòn thường)
	private void OnNormalAttackAreaEntered(Node2D body)
	{
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _normalAttackArea.Monitoring)
		{
			_damageDealt = true;
			
			body.Call("TakeDamage", Damage);
			GD.Print($"*** NORMAL HIT TARGET {body.Name} for {Damage} damage! ***");

			// Light knockback for normal attacks
			if (body.HasMethod("AddKnockback"))
			{
				Vector2 knockbackDirection = (_target.GlobalPosition - GlobalPosition).Normalized();
				body.Call("AddKnockback", knockbackDirection * 150);
			}
			
			_normalAttackArea.SetDeferred("monitoring", false);
		}
	}

	// Handler cho Heavy Attack Area (đòn mạnh - 3 hits liên tiếp)
	private void OnHeavyAttackAreaEntered(Node2D body)
	{
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _heavyAttackArea.Monitoring)
		{
			_damageDealt = true;
			
			body.Call("TakeDamage", HeavyDamage);
			GD.Print($"*** HEAVY HIT TARGET {body.Name} for {HeavyDamage} damage! (Hit {_currentHeavyHit}/3) ***");

			// Strong knockback for heavy attacks
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
			// Fallback search
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

	// Public methods for external interaction
	public void TakeDamage(int damage)
	{
		if (_currentState == EnemyState.Dead) return;

		// Attacks can only be interrupted by special stun effects
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
			// Visual damage feedback
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
		_heavyAttackCharging = false;
		_currentState = EnemyState.Stunned;
		
		// Tắt tất cả attack areas
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		// Visual stun effect
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
		
		// Reduce knockback during uninterruptible attacks
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
		_heavyAttackCharging = false;
		_animatedSprite.Play("idle");
		_collisionShape.SetDeferred("disabled", true);
		
		// Tắt tất cả attack areas
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		GD.Print("LightArcherEnemy died!");
		
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

	// Debug information
	public override void _Draw()
	{
		if (Engine.IsEditorHint()) return;
		
		// Draw detection range in debug mode
		if (OS.IsDebugBuild())
		{
			DrawCircle(Vector2.Zero, DetectionRange, Colors.Yellow, false, 1.0f);
		}
	}
}
