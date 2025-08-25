using Godot;

public partial class BaseEnemy : CharacterBody2D
{
	// Common properties
	[Export] public float Speed = 300.0f;
	[Export] public float JumpVelocity = -400.0f;
	[Export] public float AttackRange = 100.0f;
	[Export] public float HeavyAttackRange = 150.0f;
	[Export] public float DetectionRange = 640.0f;
	[Export] public int MaxHealth = 100;
	[Export] public int Damage = 25;
	[Export] public int HeavyDamage = 50;
	[Export] public float PossessThresholdF = 1.0f;

	// Common components
	protected AnimatedSprite2D _animatedSprite;
	protected CollisionShape2D _collisionShape;
	protected Area2D _normalAttackArea;
	protected Area2D _heavyAttackArea;
	protected CollisionShape2D _normalAttackCollision;
	protected CollisionShape2D _heavyAttackCollision;

	// Common state variables
	protected int _currentHealth;
	protected bool _facingRight = true;
	protected bool _canAttack = true;
	protected float _attackCooldown = 0.0f;
	protected bool _damageDealt = false;
	protected bool _isStunned = false;
	protected float _stunDuration = 0.0f;
	protected Node2D _target;
	protected bool _isAttackingUninterruptible = false;
	
	// THÊM BIẾN MỚI: Theo dõi trạng thái attack
	protected bool _isAttacking = false;

	// Original collision settings
	protected uint _originalCollisionLayer;
	protected uint _originalCollisionMask;

	// Properties
	public int CurrentHealth { get { return _currentHealth; } set { _currentHealth = value; } }
	public bool IsAttackingUninterruptible => _isAttackingUninterruptible;

	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	public override void _Ready()
	{
		InitializeCommonComponents();
		InitializeStats();
		ConnectCommonSignals();
	}

	protected virtual void InitializeCommonComponents()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		
		_normalAttackArea = GetNodeOrNull<Area2D>("NormalAttackArea");
		_heavyAttackArea = GetNodeOrNull<Area2D>("HeavyAttackArea");
		
		if (_normalAttackArea != null)
		{
			_normalAttackCollision = _normalAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_normalAttackArea.Monitoring = false;
			_normalAttackArea.BodyEntered += OnNormalAttackAreaEntered;
		}
		
		if (_heavyAttackArea != null)
		{
			_heavyAttackCollision = _heavyAttackArea.GetNode<CollisionShape2D>("CollisionShape2D");
			_heavyAttackArea.Monitoring = false;
			_heavyAttackArea.BodyEntered += OnHeavyAttackAreaEntered;
		}
	}

	protected virtual void InitializeStats()
	{
		_currentHealth = MaxHealth;
		_originalCollisionLayer = CollisionLayer;
		_originalCollisionMask = CollisionMask;
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

		UpdateTimers((float)delta);

		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * (float)delta);

		ProcessState();

		if (_target == null)
			FindTarget();

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
				GD.Print("Stun ended");
			}
		}
	}

	protected virtual void ProcessState()
	{
		// To be implemented by child classes
	}

	protected virtual void FindTarget()
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

	protected Node2D FindPlayerNodeRecursive(Node node)
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

	protected void UpdateFacing(float directionX)
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

	protected void UpdateNormalAttackAreaFacing()
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

	protected void UpdateHeavyAttackAreaFacing()
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
	}

	public virtual void TakeDamage(int damage)
	{
		if (IsDead()) return;

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

	public virtual void ApplyStun(float duration)
	{
		if (IsDead()) return;

		GD.Print($"Enemy stunned for {duration} seconds - interrupting attacks!");
		
		_isStunned = true;
		_stunDuration = duration;
		_isAttackingUninterruptible = false;
		
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

	public virtual void AddKnockback(Vector2 force)
	{
		if (IsDead()) return;
		
		if (_isAttackingUninterruptible)
		{
			force *= 0.3f;
		}
		
		Velocity = new Vector2(Velocity.X + force.X, Velocity.Y + force.Y);
		GD.Print($"Knockback applied: {force}");
	}

	protected virtual void Die()
	{
		_isAttackingUninterruptible = false;
		_animatedSprite.Play("idle");
		_collisionShape.SetDeferred("disabled", true);
		
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
		
		GD.Print("Enemy died!");
		
		var deathTimer = GetTree().CreateTimer(3.0f);
		deathTimer.Timeout += () => { QueueFree(); };
	}

	public bool CanBePossessed()
	{
		return CurrentHealth <= 0 || (float)CurrentHealth / MaxHealth <= PossessThresholdF;
	}

	public virtual void SetIsControlledByPlayer(bool controlled)
	{
		if (controlled)
		{
			_isAttackingUninterruptible = false;
			SetPhysicsProcess(false);
			SetProcess(false);
		}
		else
		{
			SetPhysicsProcess(true);
			SetProcess(true);
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
	
	public float GetPossessThresholdF()
	{
		return PossessThresholdF;
	}
	
	public uint GetOriginalCollisionLayer() => _originalCollisionLayer;
	public uint GetOriginalCollisionMask() => _originalCollisionMask;
	public void SetOriginalCollisionLayer(uint layer, uint mask)
	{
		_originalCollisionLayer = layer;
		_originalCollisionMask = mask;
	}

	public virtual void PerformAttack()
	{
		// To be implemented by child classes
	}

	protected virtual void OnAnimationFinished()
	{
		if (_normalAttackArea != null)
			_normalAttackArea.SetDeferred("monitoring", false);
		if (_heavyAttackArea != null)
			_heavyAttackArea.SetDeferred("monitoring", false);
	}

	protected virtual void OnNormalAttackAreaEntered(Node2D body)
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

	protected virtual void OnHeavyAttackAreaEntered(Node2D body)
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

	public bool IsDead()
	{
		return _currentHealth <= 0;
	}
	
	public void UpdateAttackAreaFacing()
	{
		UpdateNormalAttackAreaFacing();
		if (_heavyAttackArea != null)
		{
			UpdateHeavyAttackAreaFacing();
		}
	}
	
	public bool IsAttacking => _isAttacking;
	
	public bool CanAttack()
	{
		return _canAttack && _attackCooldown <= 0 && !_isAttacking && !_isAttackingUninterruptible;
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
