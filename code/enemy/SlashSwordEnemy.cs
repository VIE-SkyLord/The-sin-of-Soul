using Godot;

public partial class SlashSwordEnemy : BaseEnemy
{
	// Audio components
	private AudioStreamPlayer2D _normalAttackAudio;
	private AudioStreamPlayer2D _heavyAttackAudio;

	// State Management
	public enum EnemyState
	{
		Idle,
		Patrol,
		Chase,
		TripleAttack,
		StrongAttack,
		Stunned,
		Dead
	}

	private EnemyState _currentState = EnemyState.Idle;

	// Combat Variables
	private int _currentComboHit = 0;
	private Vector2 _patrolPointA;
	private Vector2 _patrolPointB;
	private bool _movingToB = true;

	// Timing
	private float _comboTimer = 0.0f;
	private float _comboDamageWindow = 0.15f;

	public override void _Ready()
	{
		base._Ready();
		InitializeAudio();
		_patrolPointA = GlobalPosition + Vector2.Left * 150;
		_patrolPointB = GlobalPosition + Vector2.Right * 150;
		_animatedSprite.Play("idle");
		GD.Print($"SlashSwordEnemy initialized - Custom Attack Areas and Audio Ready");
	}

	private void InitializeAudio()
	{
		_normalAttackAudio = new AudioStreamPlayer2D();
		_normalAttackAudio.Name = "NormalAttackAudio";
		_normalAttackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/normal_attack-slash_sword.mp3");
		AddChild(_normalAttackAudio);

		_heavyAttackAudio = new AudioStreamPlayer2D();
		_heavyAttackAudio.Name = "HeavyAttackAudio";
		_heavyAttackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/heavy_attack-slash_sword.mp3");
		AddChild(_heavyAttackAudio);

		if (_normalAttackAudio.Stream == null)
			GD.PrintErr("Failed to load normal_attack-slash_sword.mp3");
		if (_heavyAttackAudio.Stream == null)
			GD.PrintErr("Failed to load heavy_attack-slash_sword.mp3");
	}

	protected override void UpdateTimers(float delta)
	{
		base.UpdateTimers(delta);

		if (_currentState == EnemyState.TripleAttack && _comboTimer > 0)
		{
			_comboTimer -= delta;
		}
	}

	protected override void ProcessState()
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
			nextHitTimer.Timeout += () => ExecuteComboHit();
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

	protected override void OnAnimationFinished()
	{
		base.OnAnimationFinished();
		string currentAnimation = _animatedSprite.Animation;
		GD.Print($"Animation finished: {currentAnimation}");
	}

	public override void PerformAttack()
	{
		if (_currentState == EnemyState.TripleAttack || _currentState == EnemyState.StrongAttack)
			return;
		
		if (_target == null)
			FindTarget();
		
		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= AttackRange)
		{
			if (GlobalPosition.DistanceTo(_target.GlobalPosition) <= HeavyAttackRange && GD.Randf() < 0.3f)
			{
				StartStrongAttack();
			}
			else
			{
				StartTripleAttack();
			}
		}
	}

	protected override void Die()
	{
		_currentState = EnemyState.Dead;
		base.Die();
		GD.Print("SlashSwordEnemy died!");
	}
}
