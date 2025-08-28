using Godot;

public partial class SlashSwordEnemy : BaseEnemy
{
	// Audio components
	[Export] public AudioStream LightAttackAudioStream; // Exposed in Inspector
	[Export] public AudioStream HeavyAttackAudioStream; // Exposed in Inspector
	private AudioStreamPlayer2D _lightAttackAudio;
	private AudioStreamPlayer2D _heavyAttackAudio;

	// Combo system for special attack
	private int _currentComboHit = 0;
	private const int MAX_COMBO_HITS = 3;
	private const float COMBO_HIT_INTERVAL = 1.0f;
	private const float COMBO_DAMAGE_WINDOW = 0.15f;

	protected override void OnEnemyReady()
	{
		base.OnEnemyReady();
		InitializeAudio();
		
		// Sword enemy is more aggressive
		AttackCooldownDuration = 3.5f;
		SpecialAttackCooldownDuration = 7.0f;
		
		GD.Print("SlashSwordEnemy initialized with combo system");
	}

	private void InitializeAudio()
	{
		_lightAttackAudio = new AudioStreamPlayer2D();
		_lightAttackAudio.Name = "LightAttackAudio";
		_lightAttackAudio.Stream = LightAttackAudioStream; // Use exported property
		AddChild(_lightAttackAudio);

		_heavyAttackAudio = new AudioStreamPlayer2D();
		_heavyAttackAudio.Name = "HeavyAttackAudio";
		_heavyAttackAudio.Stream = HeavyAttackAudioStream; // Use exported property
		AddChild(_heavyAttackAudio);

		if (LightAttackAudioStream == null)
			GD.PrintErr("NormalAttackAudioStream not assigned in Inspector");
		if (HeavyAttackAudioStream == null)
			GD.PrintErr("SpecialAttackAudioStream not assigned in Inspector");
	}

	#region AI Attack Methods (unchanged)
	
	protected override void ProcessNormalAttackState()
	{
		base.ProcessNormalAttackState();
		
		if (!_isAttacking)
		{
			StartSwordNormalAttack();
		}
	}

	protected override void ProcessSpecialAttackState()
	{
		base.ProcessSpecialAttackState();
		
		if (!_isAttacking)
		{
			StartSwordSpecialAttack();
		}
	}

	private void StartSwordNormalAttack()
	{
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_damageDealt = false;
		
		PlayAnimation("normal_attack");
		UpdateSpecialAttackAreaFacing(); // Normal attack uses special area
		
		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () => EnableNormalAttackDamage();
		
		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => EndAttack();
	}

	private void StartSwordSpecialAttack()
	{
		_isAttacking = true;
		_isAttackingUninterruptible = true;
		_currentComboHit = 0;
		
		PlayAnimation("special_attack");
		GD.Print("=== SWORD SPECIAL ATTACK: 3-Hit Combo ===");
		
		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () =>ExecuteComboHit();// Start combo sequence
	}

	private void ExecuteComboHit()
	{
		if (_currentState != EnemyState.SpecialAttack || _currentComboHit >= MAX_COMBO_HITS)
		{
			EndSpecialCombo();
			return;
		}

		_currentComboHit++;
		_damageDealt = false;
		
		// Alternate between normal and special areas for variety
		bool useSpecialArea = (_currentComboHit == 2); // Second hit is stronger
		
		if (useSpecialArea)
		{
			PlayAnimation("special_attack");
			UpdateSpecialAttackAreaFacing();
		}
		else
		{
			PlayAnimation("normal_attack");
			UpdateNormalAttackAreaFacing();
		}
		
		GD.Print($"Combo hit {_currentComboHit}/{MAX_COMBO_HITS} - {(useSpecialArea ? "Heavy" : "Light")} attack");
		
		// Enable damage after short delay
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => {
			if (useSpecialArea)
				EnableSpecialComboDamage();
			else
				EnableNormalComboDamage();
		};
		
		// Schedule next hit or end combo
		if (_currentComboHit < MAX_COMBO_HITS)
		{
			var nextHitTimer = GetTree().CreateTimer(COMBO_HIT_INTERVAL);
			nextHitTimer.Timeout += () => ExecuteComboHit();
		}
		else
		{
			var endTimer = GetTree().CreateTimer(COMBO_HIT_INTERVAL);
			endTimer.Timeout += () => EndSpecialCombo();
		}
	}

	private void EndSpecialCombo()
	{
		GD.Print("=== SWORD COMBO ENDED ===");
		EndAttack();
	}

	private void EnableNormalAttackDamage()
	{
		if (_damageDealt || _specialAttackArea == null) return;
		
		_specialAttackArea.SetDeferred("monitoring", true);
		
		if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
			_heavyAttackAudio.Play();
		
		var disableTimer = GetTree().CreateTimer(COMBO_DAMAGE_WINDOW);
		disableTimer.Timeout += () => {
			if (_specialAttackArea != null)
				_specialAttackArea.SetDeferred("monitoring", false);
		};
	}

	private void EnableNormalComboDamage()
	{
		if (_damageDealt || _normalAttackArea == null) return;
		
		_normalAttackArea.SetDeferred("monitoring", true);
		
		if (_lightAttackAudio != null && _lightAttackAudio.Stream != null)
			_lightAttackAudio.Play();
		
		var disableTimer = GetTree().CreateTimer(COMBO_DAMAGE_WINDOW);
		disableTimer.Timeout += () => {
			if (_normalAttackArea != null)
			{
				_normalAttackArea.SetDeferred("monitoring", false);
				_damageDealt = false; // Reset for next combo hit
			}
		};
	}

	private void EnableSpecialComboDamage()
	{
		if (_damageDealt || _specialAttackArea == null) return;
		
		_specialAttackArea.SetDeferred("monitoring", true);
		
		if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
			_heavyAttackAudio.Play();
		
		var disableTimer = GetTree().CreateTimer(COMBO_DAMAGE_WINDOW);
		disableTimer.Timeout += () => {
			if (_specialAttackArea != null)
			{
				_specialAttackArea.SetDeferred("monitoring", false);
				_damageDealt = false; // Reset for next combo hit
			}
		};
	}
	
	#endregion

	#region Player Control Attack Overrides - NEW

	protected override void StartPlayerNormalAttackSequence()
	{
		_isMovementLocked = true; // Lock movement
		
		PlayAnimation("normal_attack");
		UpdateSpecialAttackAreaFacing(); // Sword normal attack uses special area
		
		GD.Print("Starting sword player normal attack - movement locked");
		
		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () => {
			if (_isAttacking && _specialAttackArea != null)
			{
				_specialAttackArea.SetDeferred("monitoring", true);
				
				if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
					_heavyAttackAudio.Play();
				
				var disableTimer = GetTree().CreateTimer(0.15f);
				disableTimer.Timeout += () => {
					if (_specialAttackArea != null)
						_specialAttackArea.SetDeferred("monitoring", false);
				};
			}
		};
		
		var endTimer = GetTree().CreateTimer(1.0f);
		endTimer.Timeout += () => {
			_isMovementLocked = false; // Unlock movement
			EndPlayerAttack();
		};
	}

	protected override void StartPlayerSpecialAttackSequence()
	{
		PlayAnimation("special_attack");
		UpdateSpecialAttackAreaFacing();
		_currentComboHit = 0;
		
		GD.Print("=== SWORD PLAYER SPECIAL ATTACK: 3-Hit Combo ===");
		
		var damageTimer = GetTree().CreateTimer(0.5f);
		damageTimer.Timeout += () => ExecutePlayerComboHit();
	}

	private void ExecutePlayerComboHit()
	{
		if (_currentState != EnemyState.SpecialAttack || _currentComboHit >= MAX_COMBO_HITS)
		{
			_isMovementLocked = false; // Unlock movement
			EndPlayerCombo();
			return;
		}

		_currentComboHit++;
		_damageDealt = false;
		
		// Keep movement locked during combo
		_isMovementLocked = true;
		
		// Alternate between normal and special areas for variety
		bool useSpecialArea = (_currentComboHit == 2); // Second hit is stronger
		
		if (useSpecialArea)
		{
			PlayAnimation("special_attack");
			UpdateSpecialAttackAreaFacing();
		}
		else
		{
			PlayAnimation("normal_attack");
			UpdateNormalAttackAreaFacing();
		}
		
		GD.Print($"Player combo hit {_currentComboHit}/{MAX_COMBO_HITS} - {(useSpecialArea ? "Heavy" : "Light")} attack - movement locked");
		
		// Enable damage after short delay
		var damageTimer = GetTree().CreateTimer(0.3f);
		damageTimer.Timeout += () => {
			if (_isAttacking)
			{
				if (useSpecialArea && _specialAttackArea != null)
				{
					_specialAttackArea.SetDeferred("monitoring", true);
					
					if (_heavyAttackAudio != null && _heavyAttackAudio.Stream != null)
						_heavyAttackAudio.Play();
					
					var disableTimer = GetTree().CreateTimer(COMBO_DAMAGE_WINDOW);
					disableTimer.Timeout += () => {
						if (_specialAttackArea != null)
						{
							_specialAttackArea.SetDeferred("monitoring", false);
							_damageDealt = false;
						}
					};
				}
				else if (!useSpecialArea && _normalAttackArea != null)
				{
					_normalAttackArea.SetDeferred("monitoring", true);
					
					if (_lightAttackAudio != null && _lightAttackAudio.Stream != null)
						_lightAttackAudio.Play();
					
					var disableTimer = GetTree().CreateTimer(COMBO_DAMAGE_WINDOW);
					disableTimer.Timeout += () => {
						if (_normalAttackArea != null)
						{
							_normalAttackArea.SetDeferred("monitoring", false);
							_damageDealt = false;
						}
					};
				}
			}
		};
		
		// Schedule next hit or end combo
		if (_currentComboHit < MAX_COMBO_HITS)
		{
			var nextHitTimer = GetTree().CreateTimer(COMBO_HIT_INTERVAL);
			nextHitTimer.Timeout += () => ExecutePlayerComboHit();
		}
		else
		{
			var endTimer = GetTree().CreateTimer(COMBO_HIT_INTERVAL);
			endTimer.Timeout += () => {
				_isMovementLocked = false; // Unlock movement
				EndPlayerCombo();
			};
		}
	}

	private void EndPlayerCombo()
	{
		GD.Print("=== SWORD PLAYER COMBO ENDED ===");
		EndPlayerAttack();
	}

	#endregion

	protected override EnemyState DecideAttackType()
	{
		// 70% normal attack, 30% combo attack
		return GD.Randf() < 0.7f ? EnemyState.NormalAttack : EnemyState.SpecialAttack;
	}

	protected override void Die()
	{
		base.Die();
		GD.Print("SlashSwordEnemy died!");
	}
}
