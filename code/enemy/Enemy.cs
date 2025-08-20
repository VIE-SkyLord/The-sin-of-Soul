using Godot;
using System;
using System.IO;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 400.0f;
	[Export] public float JumpVelocity = -700.0f;
	[Export] public float Gravity = 980.0f;
	[Export] public float DetectionRange = 300.0f;
	[Export] public float AttackRange = 100.0f;
	[Export] public float AttackCooldown = 1.0f;
	[Export] public int AttackDamage = 10;
	[Export] public float PatrolRange = 500.0f;
	[Export] public float PatrolSpeed = 200.0f;
	[Export] public float ChaseSpeed = 400.0f;
	[Export] public float ReturnThreshold = 100.0f;
	[Export] public float DetectionDelay = 0.5f; // Giảm delay để phản ứng nhanh hơn
	[Export] public float LoseTargetDelay = 2.0f; // Tăng thời gian nhớ vị trí player
	[Export] public float PredictionDistance = 100.0f; // Khoảng cách dự đoán player
	[Export] public float StuckThreshold = 50.0f; // Ngưỡng phát hiện bị mắc kẹt
	[Export] public float JumpDetectionRange = 80.0f; // Tầm phát hiện cần nhảy

	// Enum cho trạng thái AI - Thêm trạng thái mới
	public enum EnemyState
	{
		Patrolling,
		Detected,
		Chasing,
		Predicting,     // Dự đoán vị trí player
		LostTarget,
		Returning,
		Attacking,
		Jumping,        // Đang nhảy để vượt chướng ngại vật
		Unstuck         // Đang thoát khỏi trạng thái mắc kẹt
	}

	private bool isPossessed = false;
	private float attackCooldownTimer = 0.0f;
	private Player player;
	private Vector2 spawnPosition;
	private Vector2 patrolTarget;
	private Vector2 lastPlayerPosition; // Vị trí cuối cùng thấy player
	private Vector2 predictedPlayerPosition; // Vị trí dự đoán của player
	private EnemyState currentState = EnemyState.Patrolling;
	private EnemyState previousState = EnemyState.Patrolling; // Lưu trạng thái trước đó
	private float stateTimer = 0.0f;
	private float patrolChangeInterval = 3.0f;
	private int patrolDirection = 1;
	private float delayTimer = 0.0f;
	
	// Hệ thống chống mắc kẹt
	private Vector2 lastPosition;
	private float stuckTimer = 0.0f;
	private float stuckCheckInterval = 1.0f;
	private int unstuckAttempts = 0;
	private int maxUnstuckAttempts = 3;
	
	// Hệ thống dự đoán và học hỏi
	private Vector2 playerVelocity;
	private Vector2 lastKnownPlayerPosition;
	private float playerTrackingTimer = 0.0f;
	private float memoryDuration = 10.0f; // Thời gian nhớ vị trí player
	private bool hasLineOfSight = true;
	
	// Hệ thống quyết định thông minh
	private float decisionCooldown = 2.0f; // Tần suất đánh giá quyết định
	private float lastDecisionTime = 0.0f;
	private int consecutiveFailedJumps = 0;
	private float jumpCooldown = 2.0f;
	
	private static readonly string LogFilePath = Path.Combine(OS.GetUserDataDir(), "enemy_debug.log");

	public override void _Ready()
	{
		spawnPosition = Position;
		lastPosition = Position;
		SetRandomPatrolTarget();
		
		player = GetTree().GetFirstNodeInGroup("Player") as Player;
		if (player == null)
		{
			Log("Warning: Player node not found in group 'Player'!");
		}
		else
		{
			Log($"Smart Enemy initialized at spawn position: {spawnPosition}");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		float deltaF = (float)delta;

		// Áp dụng trọng lực
		if (!IsOnFloor())
			velocity.Y += Gravity * deltaF;

		// Cập nhật các timer
		UpdateTimers(deltaF);

		// Kiểm tra mắc kẹt
		CheckStuckState(deltaF);

		// Cập nhật thông tin về player
		UpdatePlayerTracking(deltaF);

		if (isPossessed)
		{
			HandlePossessedMovement(ref velocity, delta);
		}
		else
		{
			HandleSmartAIBehavior(ref velocity, deltaF);
		}

		Velocity = velocity;
		MoveAndSlide();
		
		// Lưu vị trí để kiểm tra mắc kẹt
		lastPosition = Position;
	}

	private void UpdateTimers(float delta)
	{
		attackCooldownTimer = Mathf.Max(0, attackCooldownTimer - delta);
		stateTimer += delta;
		delayTimer += delta;
		playerTrackingTimer += delta;
		jumpCooldown = Mathf.Max(0, jumpCooldown - delta);
		lastDecisionTime += delta;
	}

	private void CheckStuckState(float delta)
	{
		// Kiểm tra nếu enemy bị mắc kẹt
		float distanceMoved = Position.DistanceTo(lastPosition);
		
		if (distanceMoved < StuckThreshold * delta && currentState != EnemyState.Attacking)
		{
			stuckTimer += delta;
			if (stuckTimer >= stuckCheckInterval && currentState != EnemyState.Unstuck)
			{
				Log($"Enemy appears stuck, attempting to unstuck. Distance moved: {distanceMoved}");
				ChangeState(EnemyState.Unstuck);
				unstuckAttempts = 0;
			}
		}
		else
		{
			stuckTimer = 0.0f;
		}
	}

	private void UpdatePlayerTracking(float delta)
	{
		if (player == null) return;

		// Tính toán line of sight (đơn giản hóa)
		hasLineOfSight = !player.isDashing && CanSeePlayer();
		
		if (hasLineOfSight)
		{
			// Cập nhật vị trí và vận tốc của player
			Vector2 newPlayerPos = player.Position;
			if (lastKnownPlayerPosition != Vector2.Zero)
			{
				playerVelocity = (newPlayerPos - lastKnownPlayerPosition) / delta;
			}
			lastKnownPlayerPosition = newPlayerPos;
			lastPlayerPosition = newPlayerPos;
			playerTrackingTimer = 0.0f;
			
			// Dự đoán vị trí player
			predictedPlayerPosition = newPlayerPos + playerVelocity * PredictionDistance / 100.0f;
		}
		else if (playerTrackingTimer < memoryDuration)
		{
			// Tiếp tục dự đoán dựa trên vận tốc cuối cùng
			predictedPlayerPosition = lastPlayerPosition + playerVelocity * playerTrackingTimer;
		}
	}

	private bool CanSeePlayer()
	{
		// Kiểm tra line of sight đơn giản (có thể mở rộng với raycast)
		float distance = Position.DistanceTo(player.Position);
		return distance <= DetectionRange * 1.2f; // Cho phép thấy xa hơn một chút
	}

	private void HandlePossessedMovement(ref Vector2 velocity, double delta)
	{
		if (Input.IsActionPressed("move_up") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
			Log($"Enemy jump input detected, velocity: {velocity}");
		}

		float direction = Input.GetAxis("move_left", "move_right");
		if (direction != 0)
		{
			velocity.X = direction * Speed;
			Log($"Enemy moving with direction: {direction}, velocity: {velocity}");
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
		}
	}

	private void HandleSmartAIBehavior(ref Vector2 velocity, float delta)
	{
		// Quyết định thông minh với cooldown để tránh thay đổi liên tục
		if (lastDecisionTime >= decisionCooldown)
		{
			MakeSmartDecision();
			lastDecisionTime = 0.0f;
		}

		// Thực hiện hành động dựa trên trạng thái
		switch (currentState)
		{
			case EnemyState.Patrolling:
				HandleSmartPatrolling(ref velocity, delta);
				break;
			case EnemyState.Detected:
				HandleSmartDetected(ref velocity, delta);
				break;
			case EnemyState.Chasing:
				HandleSmartChasing(ref velocity, delta);
				break;
			case EnemyState.Predicting:
				HandlePredicting(ref velocity, delta);
				break;
			case EnemyState.LostTarget:
				HandleSmartLostTarget(ref velocity, delta);
				break;
			case EnemyState.Returning:
				HandleSmartReturning(ref velocity, delta);
				break;
			case EnemyState.Attacking:
				HandleSmartAttacking(ref velocity, delta);
				break;
			case EnemyState.Jumping:
				HandleJumping(ref velocity, delta);
				break;
			case EnemyState.Unstuck:
				HandleUnstuck(ref velocity, delta);
				break;
		}
	}

	private void MakeSmartDecision()
	{
		if (player == null) return;

		float distanceToPlayer = Position.DistanceTo(player.Position);
		float distanceToSpawn = Position.DistanceTo(spawnPosition);
		float distanceToLastKnown = Position.DistanceTo(lastPlayerPosition);

		// Ưu tiên cao nhất: Tấn công nếu có thể
		if (hasLineOfSight && distanceToPlayer <= AttackRange && attackCooldownTimer <= 0)
		{
			ChangeState(EnemyState.Attacking);
			return;
		}

		// Xử lý trạng thái mắc kẹt
		if (currentState == EnemyState.Unstuck)
		{
			return; // Để HandleUnstuck xử lý
		}

		// Nếu có thể thấy player và trong tầm
		if (hasLineOfSight && distanceToPlayer <= DetectionRange && distanceToSpawn <= PatrolRange * 1.5f)
		{
			// Kiểm tra nếu cần nhảy để tiếp cận
			if (ShouldJump() && jumpCooldown <= 0)
			{
				ChangeState(EnemyState.Jumping);
			}
			else if (currentState != EnemyState.Chasing && currentState != EnemyState.Detected)
			{
				ChangeState(EnemyState.Detected);
			}
			else if (currentState == EnemyState.Detected && delayTimer >= DetectionDelay)
			{
				ChangeState(EnemyState.Chasing);
			}
		}
		// Nếu không thấy player nhưng nhớ vị trí cuối
		else if (!hasLineOfSight && playerTrackingTimer < memoryDuration && distanceToLastKnown <= DetectionRange * 2)
		{
			ChangeState(EnemyState.Predicting);
		}
		// Mất dấu hoàn toàn
		else if (currentState == EnemyState.Chasing || currentState == EnemyState.Predicting)
		{
			ChangeState(EnemyState.LostTarget);
		}
		// Quá xa spawn point
		else if (distanceToSpawn > PatrolRange)
		{
			ChangeState(EnemyState.Returning);
		}
		// Trạng thái mặc định
		else if (currentState == EnemyState.LostTarget && delayTimer >= LoseTargetDelay)
		{
			ChangeState(EnemyState.Patrolling);
		}
	}

	private bool ShouldJump()
	{
		if (!IsOnFloor() || consecutiveFailedJumps >= 2) return false;

		// Kiểm tra nếu có chướng ngại vật phía trước
		Vector2 direction = (player.Position - Position).Normalized();
		Vector2 checkPosition = Position + direction * JumpDetectionRange;
		
		// Sử dụng raycast để kiểm tra chướng ngại vật (đơn giản hóa)
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(Position, checkPosition);
		var result = spaceState.IntersectRay(query);
		
		return result.Count > 0;
	}

	private void HandleSmartPatrolling(ref Vector2 velocity, float delta)
	{
		// Tuần tra thông minh với kiểm tra chướng ngại vật
		if (stateTimer >= patrolChangeInterval)
		{
			patrolDirection *= -1;
			stateTimer = 0.0f;
			patrolChangeInterval = (float)GD.RandRange(2.0f, 4.0f);
		}

		float distanceFromSpawn = Mathf.Abs(Position.X - spawnPosition.X);
		if (distanceFromSpawn >= PatrolRange)
		{
			patrolDirection = Position.X > spawnPosition.X ? -1 : 1;
		}

		// Kiểm tra chướng ngại vật phía trước
		if (IsObstacleAhead() && IsOnFloor() && jumpCooldown <= 0)
		{
			velocity.Y = JumpVelocity * 0.7f; // Nhảy nhẹ hơn khi tuần tra
			jumpCooldown = 1.0f;
		}

		velocity.X = patrolDirection * PatrolSpeed;
		Log($"Smart patrolling: direction={patrolDirection}, distance_from_spawn={distanceFromSpawn:F1}");
	}

	private void HandleSmartDetected(ref Vector2 velocity, float delta)
	{
		// Dừng và chuẩn bị, nhưng vẫn theo dõi player
		velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 3);
		
		if (delayTimer >= DetectionDelay)
		{
			ChangeState(EnemyState.Chasing);
		}
		else
		{
			Log($"Smart detection: tracking player, delay {delayTimer:F2}/{DetectionDelay:F2}");
		}
	}

	private void HandleSmartChasing(ref Vector2 velocity, float delta)
	{
		Vector2 targetPosition = hasLineOfSight ? player.Position : predictedPlayerPosition;
		float direction = Mathf.Sign(targetPosition.X - Position.X);
		
		// Kiểm tra cần nhảy
		if (ShouldJump() && IsOnFloor() && jumpCooldown <= 0)
		{
			ChangeState(EnemyState.Jumping);
			return;
		}

		velocity.X = direction * ChaseSpeed;
				string targetType = hasLineOfSight ? "player" : "predicted";
		Log($"Smart chasing: direction={direction}, target={targetType}");
	}

	private void HandlePredicting(ref Vector2 velocity, float delta)
	{
		// Di chuyển về vị trí dự đoán của player
		float direction = Mathf.Sign(predictedPlayerPosition.X - Position.X);
		float distance = Position.DistanceTo(predictedPlayerPosition);
		
		if (distance < 50.0f)
		{
			// Đã đến vị trí dự đoán, tìm kiếm xung quanh
			velocity.X = Mathf.MoveToward(velocity.X, 0, ChaseSpeed);
			if (playerTrackingTimer >= memoryDuration)
			{
				ChangeState(EnemyState.LostTarget);
			}
		}
		else
		{
			velocity.X = direction * (ChaseSpeed * 0.8f); // Chậm hơn khi dự đoán
		}
		
		Log($"Predicting: moving to predicted position, distance={distance:F1}");
	}

	private void HandleSmartLostTarget(ref Vector2 velocity, float delta)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0, ChaseSpeed * 2);
		
		if (delayTimer >= LoseTargetDelay)
		{
			float distanceToSpawn = Position.DistanceTo(spawnPosition);
			ChangeState(distanceToSpawn > PatrolRange ? EnemyState.Returning : EnemyState.Patrolling);
		}
	}

	private void HandleSmartReturning(ref Vector2 velocity, float delta)
	{
		float direction = Mathf.Sign(spawnPosition.X - Position.X);
		float distanceToSpawn = Position.DistanceTo(spawnPosition);
		
		// Vượt chướng ngại vật khi về spawn
		if (IsObstacleAhead() && IsOnFloor() && jumpCooldown <= 0)
		{
			velocity.Y = JumpVelocity * 0.8f;
			jumpCooldown = 0.5f;
		}
		
		if (distanceToSpawn <= ReturnThreshold)
		{
			ChangeState(EnemyState.Patrolling);
			velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * 2);
		}
		else
		{
			velocity.X = direction * ChaseSpeed;
		}
	}

	private void HandleSmartAttacking(ref Vector2 velocity, float delta)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0, Speed * 3);
		AttackPlayer();
		
		// Quyết định trạng thái tiếp theo thông minh hơn
		float distanceToPlayer = Position.DistanceTo(player.Position);
		if (distanceToPlayer > AttackRange)
		{
			if (hasLineOfSight && distanceToPlayer <= DetectionRange)
			{
				ChangeState(EnemyState.Chasing);
			}
			else
			{
				ChangeState(EnemyState.Predicting);
			}
		}
	}

	private void HandleJumping(ref Vector2 velocity, float delta)
	{
		// Nhảy để vượt chướng ngại vật
		if (IsOnFloor())
		{
			velocity.Y = JumpVelocity;
			jumpCooldown = 1.0f;
			
			// Tiếp tục di chuyển theo hướng mục tiêu
			Vector2 targetPos = hasLineOfSight ? player.Position : predictedPlayerPosition;
			float direction = Mathf.Sign(targetPos.X - Position.X);
			velocity.X = direction * ChaseSpeed * 0.7f;
			
			Log("Jumping to overcome obstacle");
		}
		
		// Quay về trạng thái trước đó sau khi nhảy
		if (IsOnFloor() && delayTimer > 0.5f)
		{
			ChangeState(previousState == EnemyState.Jumping ? EnemyState.Chasing : previousState);
		}
	}

	private void HandleUnstuck(ref Vector2 velocity, float delta)
	{
		unstuckAttempts++;
		
		if (unstuckAttempts <= maxUnstuckAttempts)
		{
			// Thử các hành động khác nhau để thoát mắc kẹt
			switch (unstuckAttempts)
			{
				case 1:
					// Thử nhảy
					if (IsOnFloor())
					{
						velocity.Y = JumpVelocity;
						velocity.X = patrolDirection * Speed * 0.5f;
					}
					break;
				case 2:
					// Đổi hướng
					patrolDirection *= -1;
					velocity.X = patrolDirection * Speed;
					break;
				case 3:
					// Nhảy và đổi hướng
					patrolDirection *= -1;
					if (IsOnFloor())
					{
						velocity.Y = JumpVelocity * 1.2f;
						velocity.X = patrolDirection * Speed;
					}
					break;
			}
			
			Log($"Unstuck attempt {unstuckAttempts}: action taken");
		}
		else
		{
			// Reset và quay về tuần tra
			unstuckAttempts = 0;
			stuckTimer = 0.0f;
			ChangeState(EnemyState.Patrolling);
			Log("Max unstuck attempts reached, returning to patrol");
		}
	}

	private bool IsObstacleAhead()
	{
		// Kiểm tra đơn giản chướng ngại vật phía trước
		Vector2 checkDirection = new Vector2(patrolDirection, 0);
		Vector2 checkPosition = Position + checkDirection * 60.0f;
		
		var spaceState = GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(Position, checkPosition);
		var result = spaceState.IntersectRay(query);
		
		return result.Count > 0;
	}

	private void ChangeState(EnemyState newState)
	{
		if (currentState != newState)
		{
			previousState = currentState;
			currentState = newState;
			delayTimer = 0.0f;
			stateTimer = 0.0f;
			
			// Reset specific counters based on state
			if (newState == EnemyState.Jumping)
			{
				consecutiveFailedJumps = 0;
			}
			
			Log($"State changed from {previousState} to {newState}");
		}
	}

	private void SetRandomPatrolTarget()
	{
		float randomOffset = (float)GD.RandRange(-PatrolRange, PatrolRange);
		patrolTarget = new Vector2(spawnPosition.X + randomOffset, spawnPosition.Y);
	}

	private void AttackPlayer()
	{
		if (player != null)
		{
			player.TakeDamage(AttackDamage);
			attackCooldownTimer = AttackCooldown;
			Log($"Smart attack: dealt {AttackDamage} damage to player");
		}
	}

	public void SetPossessed(bool possessed)
	{
		isPossessed = possessed;
		if (!possessed)
		{
			ChangeState(EnemyState.Patrolling);
			stateTimer = 0.0f;
		}
		Log($"Enemy possession state: {isPossessed}, current state: {currentState}");
	}

	public EnemyState GetCurrentState() => currentState;
	public Vector2 GetSpawnPosition() => spawnPosition;
	public Vector2 GetPredictedPlayerPosition() => predictedPlayerPosition;
	public bool HasLineOfSight() => hasLineOfSight;

	private void Log(string message)
	{
		string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		string logEntry = $"[{timestamp}] [State: {currentState}] {message}";
		try
		{
			using (StreamWriter writer = new StreamWriter(LogFilePath, true))
			{
				writer.WriteLine(logEntry);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to write to log file: {e.Message}");
		}
	}

	public override void _Draw()
	{
		if (Engine.IsEditorHint())
		{
			// Vẽ các phạm vi
			DrawArc(Vector2.Zero, PatrolRange, 0, Mathf.Tau, 32, Colors.Blue, 2.0f);
			DrawArc(Vector2.Zero, DetectionRange, 0, Mathf.Tau, 32, Colors.Yellow, 2.0f);
			DrawArc(Vector2.Zero, AttackRange, 0, Mathf.Tau, 32, Colors.Red, 2.0f);
			
			// Vẽ vị trí dự đoán player (nếu có)
			if (predictedPlayerPosition != Vector2.Zero)
			{
				Vector2 localPredicted = predictedPlayerPosition - Position;
				DrawCircle(localPredicted, 10.0f, Colors.Orange);
			}
		}
	}
}
