using Godot;

public partial class LightArcherEnemy : BaseEnemy
{
	// Audio Settings
	[Export] public AudioStream BowReleaseSound;
	[Export] public float SoundVolume = -5.0f;
	[Export] public float PitchVariation = 0.3f;

	// Special attack variables
	private int _currentSpecialHit = 0;
	private const int MAX_SPECIAL_HITS = 3;
	private const float SPECIAL_CHARGE_TIME = 2.0f;
	private const float SPECIAL_HIT_INTERVAL = 0.6f;
	private const float DAMAGE_WINDOW = 0.15f;
	
	private bool _specialAttackCharging = false;

	// Audio Components
	private AudioStreamPlayer2D _bowReleasePlayer;

	protected override void OnEnemyReady()
	{
		base.OnEnemyReady();
		InitializeAudio();
		
		// Archer specific settings
		SpecialAttackRange = 200.0f; // Archers have longer range
		AttackRange = 150.0f;
		
		GD.Print("LightArcherEnemy initialized with audio system");
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
		}
		else
		{
			GD.PrintErr("BowReleaseSound not assigned in Inspector");
		}
	}

	#region AI Attack Methods

	protected override void ProcessNormalAttackState()
	{
		base.ProcessNormalAttackState();
		
		if (!_isAttacking)
		{
			StartArcherNormalAttack();
		}
	}

	protected override void ProcessSpecialAttackState()
	{
		base.ProcessSpecialAttackState();
		
		if (!_isAttacking)
		{
			StartArcherSpecialAttack();
		}
	}

	private void StartArcherNormalAttack()
	{
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_damageDealt = false;
		
		PlayAnimation("normal_attack");
		UpdateNormalAttackAreaFacing();
		
		// Archer normal attack has longer wind-up
		var damageTimer = GetTree().CreateTimer(1.2f);
		damageTimer.Timeout += () => {
			PlayBowReleaseSound();
			EnableNormalAttackDamage();
		};
		
		var endTimer = GetTree().CreateTimer(1.4f);
		endTimer.Timeout += () => EndAttack();
	}

	private void StartArcherSpecialAttack()
	{
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_currentSpecialHit = 0;
		_specialAttackCharging = true;
		
		PlayAnimation("special_attack");
		UpdateSpecialAttackAreaFacing();
		
		GD.Print("=== ARCHER SPECIAL ATTACK: 2s Charge + 3 Shots ===");
		
		// 2 second charge time
		var chargeTimer = GetTree().CreateTimer(SPECIAL_CHARGE_TIME);
		chargeTimer.Timeout += () => ExecuteSpecialAttackSequence();
	}

	private void ExecuteSpecialAttackSequence()
	{
		if (_currentState != EnemyState.SpecialAttack) return;
		
		_specialAttackCharging = false;
		GD.Print("Special attack sequence started");
		
		// Execute 3 shots with intervals
		for (int i = 0; i < MAX_SPECIAL_HITS; i++)
		{
			var hitTimer = GetTree().CreateTimer(i * SPECIAL_HIT_INTERVAL);
			hitTimer.Timeout += () => ExecuteSpecialHit();
		}
		
		// End attack after all hits
		var endTimer = GetTree().CreateTimer((MAX_SPECIAL_HITS - 1) * SPECIAL_HIT_INTERVAL + 0.3f);
		endTimer.Timeout += () => EndAttack();
	}

	private void ExecuteSpecialHit()
	{
		if (_currentState != EnemyState.SpecialAttack) return;
		
		_currentSpecialHit++;
		_damageDealt = false;
		
		GD.Print($"Special shot {_currentSpecialHit}/{MAX_SPECIAL_HITS}");
		PlayBowReleaseSound();
		EnableSpecialAttackDamage();
	}

	private void EnableNormalAttackDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;
		
		_normalAttackArea.SetDeferred("monitoring", true);
		
		var disableTimer = GetTree().CreateTimer(DAMAGE_WINDOW);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
				_normalAttackArea.SetDeferred("monitoring", false);
		};
	}

	private void EnableSpecialAttackDamage()
	{
		if (_damageDealt || _specialAttackArea == null) return;
		
		_specialAttackArea.SetDeferred("monitoring", true);
		
		var disableTimer = GetTree().CreateTimer(DAMAGE_WINDOW);
		disableTimer.Timeout += () => {
			if (_specialAttackArea != null)
			{
				_specialAttackArea.SetDeferred("monitoring", false);
				_damageDealt = false; // Reset for next shot in sequence
			}
		};
	}

	#endregion

	#region Player Control Attack Overrides - NEW

	protected override void StartPlayerNormalAttackSequence()
	{
		_isMovementLocked = true; // Lock movement
		
		PlayAnimation("normal_attack");
		UpdateNormalAttackAreaFacing();
		
		GD.Print("Starting archer player normal attack - movement locked");
		
		// Archer has longer wind-up for normal attack
		var damageTimer = GetTree().CreateTimer(1.2f);
		damageTimer.Timeout += () => {
			if (_isAttacking && _normalAttackArea != null)
			{
				PlayBowReleaseSound();
				_normalAttackArea.SetDeferred("monitoring", true);
				
				var disableTimer = GetTree().CreateTimer(0.15f);
				disableTimer.Timeout += () => {
					if (_normalAttackArea != null)
						_normalAttackArea.SetDeferred("monitoring", false);
				};
			}
		};
		
		var endTimer = GetTree().CreateTimer(1.4f);
		endTimer.Timeout += () => {
			_isMovementLocked = false; // Unlock movement
			EndPlayerAttack();
		};
	}

	protected override void StartPlayerSpecialAttackSequence()
	{
		PlayAnimation("special_attack");
		UpdateSpecialAttackAreaFacing();
		_currentSpecialHit = 0;
		_specialAttackCharging = true;
		
		GD.Print("=== ARCHER PLAYER SPECIAL ATTACK: 2s Charge + 3 Shots ===");
		
		// 2 second charge time
		var chargeTimer = GetTree().CreateTimer(SPECIAL_CHARGE_TIME);
		chargeTimer.Timeout += () => ExecutePlayerSpecialSequence();
	}

	private void ExecutePlayerSpecialSequence()
	{
		if (_currentState != EnemyState.SpecialAttack) return;
		
		_specialAttackCharging = false;
		GD.Print("Player special attack sequence started");
		
		// Keep movement locked during entire sequence
		_isMovementLocked = true;
		
		// Execute 3 shots with intervals
		for (int i = 0; i < MAX_SPECIAL_HITS; i++)
		{
			var hitTimer = GetTree().CreateTimer(i * SPECIAL_HIT_INTERVAL);
			hitTimer.Timeout += () => ExecutePlayerSpecialHit();
		}
		
		// End attack after all hits and unlock movement
		var endTimer = GetTree().CreateTimer((MAX_SPECIAL_HITS - 1) * SPECIAL_HIT_INTERVAL + 0.5f);
		endTimer.Timeout += () => {
			_isMovementLocked = false; // Unlock movement
			EndPlayerAttack();
		};
	}

	private void ExecutePlayerSpecialHit()
	{
		if (_currentState != EnemyState.SpecialAttack) return;
		
		_currentSpecialHit++;
		_damageDealt = false;
		
		GD.Print($"Player special shot {_currentSpecialHit}/{MAX_SPECIAL_HITS}");
		PlayBowReleaseSound();
		
		if (_specialAttackArea != null)
		{
			_specialAttackArea.SetDeferred("monitoring", true);
			
			var disableTimer = GetTree().CreateTimer(DAMAGE_WINDOW);
			disableTimer.Timeout += () => {
				if (_specialAttackArea != null)
				{
					_specialAttackArea.SetDeferred("monitoring", false);
					_damageDealt = false; // Reset for next shot
				}
			};
		}
	}

	#endregion

	private void PlayBowReleaseSound()
	{
		if (_bowReleasePlayer != null && BowReleaseSound != null)
		{
			float randomPitch = 1.0f + (GD.Randf() - 0.5f) * PitchVariation;
			_bowReleasePlayer.PitchScale = randomPitch;
			_bowReleasePlayer.Play();
		}
	}

	protected override float GetCurrentAttackRange()
	{
		// Archer can attack at longer range
		return _currentState == EnemyState.SpecialAttack ? SpecialAttackRange : AttackRange;
	}

	protected override EnemyState DecideAttackType()
	{
		// 40% special attack for archer (more than default)
		return GD.Randf() < 0.4f ? EnemyState.SpecialAttack : EnemyState.NormalAttack;
	}

	protected override void Die()
	{
		base.Die();
		GD.Print("LightArcherEnemy died!");
	}
}
