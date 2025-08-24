using Godot;

public partial class LightArcherEnemy : BaseEnemy
{
	// Audio Settings
	[Export] public AudioStream BowReleaseSound;
	[Export] public float SoundVolume = -5.0f;
	[Export] public float PitchVariation = 0.3f;

	// State Management
	public enum EnemyState
	{
		Idle,
		Patrol,
		Chase,
		HeavyAttack,
		NormalAttack,
		Stunned,
		Dead
	}

	// Combat Variables
	private EnemyState _currentState = EnemyState.Idle;
	private int _currentHeavyHit = 0;
	private bool _heavyAttackCharging = false;
	private float _heavyAttackChargeTime = 0.0f;

	// Target và Movement
	private Vector2 _patrolPointA;
	private Vector2 _patrolPointB;
	private bool _movingToB = true;

	// Timing
	private float _heavyAttackTimer = 0.0f;
	private float _heavyDamageWindow = 0.15f;

	// Audio Components
	private AudioStreamPlayer2D _bowReleasePlayer;

	public override void _Ready()
	{
		base._Ready();
		InitializeAudio();
		_patrolPointA = GlobalPosition + Vector2.Left * 150;
		_patrolPointB = GlobalPosition + Vector2.Right * 150;
		_animatedSprite.Play("idle");
		GD.Print($"LightArcherEnemy initialized - Custom Attack Areas & Audio Ready");
	}

	private void InitializeAudio()
	{
		_bowReleasePlayer = GetNodeOrNull<AudioStreamPlayer2D>("BowReleasePlayer");
		
		if (_bowReleasePlayer == null)
		{
			_bowReleasePlayer = new AudioStreamPlayer2D();
			_bowReleasePlayer.Name = "BowReleasePlayer";
			AddChild(_bowReleasePlayer);
		}
		
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

	protected override void UpdateTimers(float delta)
	{
		base.UpdateTimers(delta);

		if (_heavyAttackCharging && _heavyAttackChargeTime > 0)
		{
			_heavyAttackChargeTime -= delta;
		}

		if (_currentState == EnemyState.HeavyAttack && _heavyAttackTimer > 0)
		{
			_heavyAttackTimer -= delta;
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

	private void HandleHeavyAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void HandleNormalAttackState()
	{
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void HandleStunnedState()
	{
		Velocity = new Vector2(0, Velocity.Y);
		_animatedSprite.Play("idle");
	}

	private void StartHeavyAttack()
	{
		_currentState = EnemyState.HeavyAttack;
		_isAttackingUninterruptible = true;
		_canAttack = false;
		_currentHeavyHit = 0;
		_heavyAttackCharging = true;
		_heavyAttackChargeTime = 2.0f;

		GD.Print("=== STARTING HEAVY ATTACK (2s Charge) ===");
		_animatedSprite.Play("heavy_attack");
		UpdateHeavyAttackAreaFacing();

		var chargeTimer = GetTree().CreateTimer(2.0f);
		chargeTimer.Timeout += () => ExecuteHeavyAttackSequence();
	}

	private void ExecuteHeavyAttackSequence()
	{
		if (_currentState != EnemyState.HeavyAttack) return;

		_heavyAttackCharging = false;
		GD.Print("=== HEAVY ATTACK SEQUENCE STARTED ===");

		ExecuteHeavyHit();

		var hit2Timer = GetTree().CreateTimer(0.6f);
		hit2Timer.Timeout += () => ExecuteHeavyHit();

		var hit3Timer = GetTree().CreateTimer(1.2f);
		hit3Timer.Timeout += () => ExecuteHeavyHit();

		var endTimer = GetTree().CreateTimer(1.8f);
		endTimer.Timeout += () => EndHeavyAttack();
	}

	private void ExecuteHeavyHit()
	{
		if (_currentState != EnemyState.HeavyAttack) return;

		_currentHeavyHit++;
		_damageDealt = false;

		GD.Print($"Executing heavy hit {_currentHeavyHit}/3");
		PlayBowReleaseSound();
		EnableHeavyAttackDamage();
	}

	private void EndHeavyAttack()
	{
		_isAttackingUninterruptible = false;
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		_attackCooldown = 4.0f;
		_canAttack = false;
		_heavyAttackCharging = false;
		
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
		UpdateNormalAttackAreaFacing();

		var damageTimer = GetTree().CreateTimer(1.2f);
		damageTimer.Timeout += () => {
			PlayBowReleaseSound();
			EnableNormalAttackDamage();
		};

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

		var cooldownTimer = GetTree().CreateTimer(_attackCooldown);
		cooldownTimer.Timeout += () => { _canAttack = true; };

		_currentState = EnemyState.Chase;
		GD.Print("=== NORMAL ATTACK ENDED ===");
	}

	private void PlayBowReleaseSound()
	{
		if (_bowReleasePlayer != null && BowReleaseSound != null)
		{
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

	private void EnableNormalAttackDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;

		_normalAttackArea.SetDeferred("monitoring", true);
		GD.Print($"NORMAL ATTACK DAMAGE ENABLED - State: {_currentState}");

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

		var disableTimer = GetTree().CreateTimer(_heavyDamageWindow);
		disableTimer.Timeout += () => {
			if (_heavyAttackArea != null)
			{
				_heavyAttackArea.SetDeferred("monitoring", false);
				GD.Print($"Heavy attack hit {_currentHeavyHit} damage window closed");
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
		if (_currentState == EnemyState.HeavyAttack || _currentState == EnemyState.NormalAttack)
			return;
		
		if (_target == null)
			FindTarget();
		
		if (_target != null && GlobalPosition.DistanceTo(_target.GlobalPosition) <= AttackRange)
		{
			if (GD.Randf() < 0.4f)
			{
				StartHeavyAttack();
			}
			else
			{
				StartNormalAttack();
			}
		}
	}

	protected override void Die()
	{
		_currentState = EnemyState.Dead;
		base.Die();
		GD.Print("LightArcherEnemy died!");
	}
}
