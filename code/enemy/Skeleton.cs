using Godot;

public partial class Skeleton : BaseEnemy
{
	// Audio components
	private AudioStreamPlayer2D _attackAudio;

	protected override void OnEnemyReady()
	{
		base.OnEnemyReady();
		InitializeAudio();
		
		// Skeleton-specific setup
		AddToGroup("Skeletons");
		AddToGroup("Allies");
		CollisionLayer = 1u << 5;
		CollisionMask = (1u << 1) | (1u << 2);
		
		// Skeleton doesn't patrol by default
		_hasPatrolPoints = false;
		
		GD.Print($"Skeleton initialized - Health: {MaxHealth}, Damage: {Damage}");
	}

	private void InitializeAudio()
	{
		_attackAudio = new AudioStreamPlayer2D();
		_attackAudio.Name = "AttackAudio";
		_attackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/skeleton_attack.mp3");
		AddChild(_attackAudio);
	}

	#region AI Attack Methods

	protected override void ProcessNormalAttackState()
	{
		base.ProcessNormalAttackState();
		
		if (!_isAttacking)
		{
			StartSkeletonNormalAttack();
		}
	}

	private void StartSkeletonNormalAttack()
	{
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_damageDealt = false;
		
		PlayAnimation("normal_attack");
		UpdateNormalAttackAreaFacing();
		
		// Enable damage after 0.3s
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => EnableAttackDamage();
		
		// End attack after 1.0s
		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => EndSkeletonAttack();
	}

	private void EnableAttackDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;

		_normalAttackArea.SetDeferred("monitoring", true);
		
		if (_attackAudio != null && _attackAudio.Stream != null)
			_attackAudio.Play();

		// Disable after 0.2s
		var disableTimer = GetTree().CreateTimer(0.2f);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
				_normalAttackArea.SetDeferred("monitoring", false);
		};
	}

	private void EndSkeletonAttack()
	{
		EndAttack(); // Call base method
	}

	#endregion

	#region Player Control Attack Overrides - NEW

	protected override void StartPlayerNormalAttackSequence()
	{
		_isMovementLocked = true; // Lock movement
		
		PlayAnimation("normal_attack");
		UpdateNormalAttackAreaFacing();
		
		GD.Print("Starting skeleton player normal attack - movement locked");
		
		// Enable damage after 0.3s
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => {
			if (_isAttacking && _normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", true);
				
				if (_attackAudio != null && _attackAudio.Stream != null)
					_attackAudio.Play();
				
				// Disable after damage window
				var disableTimer = GetTree().CreateTimer(0.2f);
				disableTimer.Timeout += () => {
					if (_normalAttackArea != null)
						_normalAttackArea.SetDeferred("monitoring", false);
				};
			}
		};
		
		// End attack after 1.0s and unlock movement
		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => {
			_isMovementLocked = false;
			EndPlayerAttack();
		};
	}

	protected override void StartPlayerSpecialAttackSequence()
	{
		// Skeleton uses normal attack for both normal and special
		StartPlayerNormalAttackSequence();
	}

	#endregion

	protected override void OnAnimationFinished()
	{
		base.OnAnimationFinished();
		
		if (_animatedSprite.Animation == "normal_attack")
		{
			_isAttacking = false;
			_canAttack = true;
			
			if (!_isPlayerControlled && _target != null && 
				GlobalPosition.DistanceTo(_target.GlobalPosition) <= AttackRange && 
				_attackCooldown <= 0)
			{
				// Continue attacking if target is still in range
				TransitionToState(EnemyState.NormalAttack);
			}
			else if (!_isPlayerControlled)
			{
				TransitionToState(EnemyState.Chase);
			}
			else
			{
				TransitionToState(EnemyState.Idle);
			}
		}
	}

	// Skeleton doesn't have special attack
	public override void PerformSpecialAttack()
	{
		// Skeleton only has normal attack
		PerformNormalAttack();
	}

	protected override EnemyState DecideAttackType()
	{
		// Skeleton only uses normal attack
		return EnemyState.NormalAttack;
	}

	protected override void Die()
	{
		base.Die();
		
		// Notify player if this was a possessed skeleton
		if (_isPlayerControlled)
		{
			var players = GetTree().GetNodesInGroup("Player");
			if (players.Count > 0 && players[0].HasMethod("OnMonsterDeath"))
			{
				players[0].Call("OnMonsterDeath", this);
			}
		}
		
		GD.Print("Skeleton died!");
	}
}
