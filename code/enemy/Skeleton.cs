using Godot;

public partial class Skeleton : BaseEnemy
{
	// State Management
	public enum SkeletonState
	{
		Idle,
		Follow,
		Attack,
		Attacking,
		Dead
	}
	
	private SkeletonState _currentState = SkeletonState.Idle;
	private bool _isPossessed = false;
	private bool _isAttacking = false;

	// Target system
	private Node2D _owner;
	private float _followDistance = 80.0f;

	// Audio components
	private AudioStreamPlayer2D _attackAudio;

	public override void _Ready()
	{
		base._Ready();
		InitializeAudio();
		
		_animatedSprite.Play("idle");
		
		AddToGroup("Skeletons");
		AddToGroup("Allies");
		
		CollisionLayer = 1u << 5;
		CollisionMask = (1u << 1) | (1u << 2);
		
		GD.Print($"Skeleton initialized - Health: {MaxHealth}, Damage: {Damage}");
	}

	private void InitializeAudio()
	{
		_attackAudio = new AudioStreamPlayer2D();
		_attackAudio.Name = "AttackAudio";
		_attackAudio.Stream = ResourceLoader.Load<AudioStream>("res://sound/enemies/skeleton_attack.mp3");
		AddChild(_attackAudio);
	}

	protected override void ProcessState()
	{
		if (_isPossessed) return;

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

		if (_owner != null)
		{
			float distanceToOwner = GlobalPosition.DistanceTo(_owner.GlobalPosition);
			if (distanceToOwner > _followDistance)
			{
				_currentState = SkeletonState.Follow;
			}
		}
		
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
		
		if (distanceToOwner <= _followDistance)
		{
			_currentState = SkeletonState.Idle;
			return;
		}

		Vector2 direction = (_owner.GlobalPosition - GlobalPosition).Normalized();
		Velocity = new Vector2(direction.X * Speed * 0.8f, Velocity.Y);
		UpdateFacing(direction.X);
		_animatedSprite.Play("run");
		
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
		
		if (distanceToTarget > DetectionRange * 2.0f)
		{
			_target = null;
			_currentState = SkeletonState.Follow;
			return;
		}

		if (distanceToTarget <= AttackRange && _canAttack && _attackCooldown <= 0)
		{
			Vector2 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			UpdateFacing(directionToTarget.X);
			StartAttack();
			return;
		}

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
		Velocity = new Vector2(0, Velocity.Y);
	}

	private void StartAttack()
	{
		_canAttack = false;
		_damageDealt = false;
		_isAttacking = true;
		_currentState = SkeletonState.Attacking;
		
		// QUAN TRỌNG: Đảm bảo animation được play
		_animatedSprite.Play("attack");
		
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => EnableAttackDamage();
		
		_attackCooldown = 1.5f;
		
		GD.Print("Skeleton attack started");
	}
	
	private void UpdateAttackAreaFacing()
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
	}

	private void EnableAttackDamage()
	{
		if (_damageDealt || _normalAttackArea == null || !_isAttacking) return;

		UpdateAttackAreaFacing();
		_normalAttackArea.SetDeferred("monitoring", true);
		GD.Print("Skeleton attack damage enabled");

		if (_attackAudio != null && _attackAudio.Stream != null)
			_attackAudio.Play();

		var disableTimer = GetTree().CreateTimer(0.2f);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", false);
				GD.Print("Skeleton attack damage disabled");
			}
		};
	}

	protected override void OnNormalAttackAreaEntered(Node2D body)
	{
		if (body.HasMethod("TakeDamage") && body != this && !_damageDealt && _normalAttackArea.Monitoring && _isAttacking)
		{
			if (!body.IsInGroup("Player") && !body.IsInGroup("Allies"))
			{
				_damageDealt = true;
				
				body.Call("TakeDamage", Damage);
				GD.Print($"*** Skeleton HIT {body.Name} for {Damage} damage! ***");

				if (body.HasMethod("AddKnockback"))
				{
					Vector2 knockbackDirection = (body.GlobalPosition - GlobalPosition).Normalized();
					body.Call("AddKnockback", knockbackDirection * 200);
				}
				
				_normalAttackArea.SetDeferred("monitoring", false);
			}
		}
	}

	protected override void OnAnimationFinished()
	{
		base.OnAnimationFinished();
		
		if (_animatedSprite.Animation == "attack")
		{
			_isAttacking = false;
			_canAttack = true;
			
			if (_isPossessed)
			{
				_animatedSprite.Play("idle");
			}
			else
			{
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
			SetProcessInput(true);
			GD.Print("Skeleton is now possessed by player - Input enabled");
		}
		else
		{
			_currentState = SkeletonState.Follow;
			SetProcessInput(false);
			GD.Print("Skeleton is no longer possessed - Input disabled, returning to follow mode");
		}
	}

	public override void PerformAttack()
	{
		if (_isAttacking || _currentState == SkeletonState.Attacking) return;
	
		// Cập nhật hướng tấn công trước khi attack
		UpdateAttackAreaFacing();
		StartAttack();
	}

	protected override void Die()
	{
		_currentState = SkeletonState.Dead;
		base.Die();
		
		if (_isPossessed && _owner != null && _owner.HasMethod("OnSkeletonDeath"))
		{
			_owner.Call("OnSkeletonDeath", this);
		}
		
		GD.Print("Skeleton died!");
	}
}
