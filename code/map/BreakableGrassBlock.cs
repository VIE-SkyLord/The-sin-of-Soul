using Godot;

public partial class BreakableGrassBlock : StaticBody2D
{
	[Export] public float RespawnTime = 5.0f; // Thời gian khôi phục (giây)
	[Export] public PackedScene BreakEffect; // Effect khi vỡ (optional)
	
	private CollisionShape2D collisionShape;
	private Sprite2D sprite;
	private Area2D detectionArea;
	private Timer respawnTimer;
	private bool isBroken = false;
	
	// Tween để tạo hiệu ứng
	private Tween shakeTween;
	private Tween fadeTween;
	
	public override void _Ready()
	{
		// Lấy các node con
		collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		sprite = GetNode<Sprite2D>("Sprite2D");
		detectionArea = GetNode<Area2D>("DetectionArea");
		
		// Tạo timer để khôi phục
		respawnTimer = new Timer();
		respawnTimer.WaitTime = RespawnTime;
		respawnTimer.OneShot = true;
		respawnTimer.Timeout += OnRespawnTimerTimeout;
		AddChild(respawnTimer);
		
		// Tạo Tween cho hiệu ứng
		shakeTween = CreateTween();
		shakeTween.Kill(); // Dừng ngay để không chạy
		
		fadeTween = CreateTween();
		fadeTween.Kill(); // Dừng ngay để không chạy
		
		// Kết nối signal từ Area2D
		detectionArea.BodyEntered += OnBodyEntered;
	}
	
	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player") && !isBroken)
		{
			// Tạo timer 0.5s
			GetTree().CreateTimer(0.5f).Timeout += () => {
				// Kiểm tra lại: player vẫn còn trong vùng
				if (!isBroken && detectionArea.HasOverlappingBodies())
				{
					BreakBlock();
				}
			};
		}
	}
	
	private void BreakBlock()
	{
		if (isBroken) return;
		
		isBroken = true;
		
		// Tạo hiệu ứng rung lắc trước khi vỡ
		ShakeBlock();
		
		// Delay một chút rồi mới ẩn khối
		GetTree().CreateTimer(0.3f).Timeout += () => {
			// Ẩn collision và sprite
			collisionShape.Disabled = true;
			
			// Tạo hiệu ứng fade out
			FadeOut();
			
			// Spawn break effect nếu có
			SpawnBreakEffect();
			
			// Bắt đầu timer khôi phục
			respawnTimer.Start();
		};
	}
	
	private void ShakeBlock()
	{
		shakeTween = CreateTween();
		shakeTween.SetLoops(3); // Rung 3 lần (mỗi loop có 2 movement)
		
		Vector2 originalPos = Position;
		shakeTween.TweenProperty(this, "position", originalPos + new Vector2(2, 0), 0.05f);
		shakeTween.TweenProperty(this, "position", originalPos + new Vector2(-2, 0), 0.05f);
		shakeTween.TweenCallback(Callable.From(() => Position = originalPos));
	}
	
	private void FadeOut()
	{
		fadeTween = CreateTween();
		fadeTween.TweenProperty(sprite, "modulate", new Color(1, 1, 1, 0), 0.2f);
	}
	
	private void FadeIn()
	{
		fadeTween = CreateTween();
		fadeTween.TweenProperty(sprite, "modulate", new Color(1, 1, 1, 1), 0.3f);
	}
	
	private void SpawnBreakEffect()
	{
		if (BreakEffect != null)
		{
			Node2D effect = BreakEffect.Instantiate<Node2D>();
			GetParent().AddChild(effect);
			effect.GlobalPosition = GlobalPosition;
		}
		
		// Hoặc tạo hiệu ứng particles đơn giản
		CreateBreakParticles();
	}
	
	private void CreateBreakParticles()
	{
		// Tạo một số mảnh vỡ đơn giản
		for (int i = 0; i < 4; i++)
		{
			RigidBody2D particle = new RigidBody2D();
			Sprite2D particleSprite = new Sprite2D();
			CollisionShape2D particleCollision = new CollisionShape2D();
			RectangleShape2D particleShape = new RectangleShape2D();
			
			// Thiết lập mảnh vỡ
			particleShape.Size = new Vector2(4, 4);
			particleCollision.Shape = particleShape;
			particleSprite.Texture = sprite.Texture;
			particleSprite.RegionEnabled = true;
			particleSprite.RegionRect = new Rect2(i * 8, 0, 8, 8); // Chia texture thành các mảnh nhỏ
			
			particle.AddChild(particleSprite);
			particle.AddChild(particleCollision);
			particle.GlobalPosition = GlobalPosition + new Vector2(
				GD.Randf() * 16 - 8, 
				GD.Randf() * 16 - 8
			);
			
			// Thêm lực ngẫu nhiên
			Vector2 force = new Vector2(
				GD.Randf() * 200 - 100,
				-GD.Randf() * 100 - 50
			);
			
			GetParent().AddChild(particle);
			particle.ApplyImpulse(force);
			
			// Xóa mảnh vỡ sau 2 giây
			GetTree().CreateTimer(2.0f).Timeout += () => {
				if (IsInstanceValid(particle))
					particle.QueueFree();
			};
		}
	}
	
	private void OnRespawnTimerTimeout()
	{
		RespawnBlock();
	}
	
	private void RespawnBlock()
	{
		// Bật lại collision
		collisionShape.Disabled = false;
		
		// Tạo hiệu ứng fade in
		FadeIn();
		
		// Đặt lại trạng thái
		isBroken = false;
		
		// Tạo hiệu ứng respawn (optional)
		CreateRespawnEffect();
	}
	
	private void CreateRespawnEffect()
	{
		// Tạo hiệu ứng scale khi respawn
		Vector2 originalScale = sprite.Scale;
		sprite.Scale = Vector2.Zero;
		
		Tween respawnTween = CreateTween();
		respawnTween.TweenProperty(sprite, "scale", originalScale, 0.3f);
		respawnTween.TweenCallback(Callable.From(() => sprite.Scale = originalScale));
	}
	
	// Method để reset khối từ bên ngoài nếu cần
	public void ResetBlock()
	{
		if (respawnTimer.TimeLeft > 0)
		{
			respawnTimer.Stop();
		}
		
		isBroken = false;
		collisionShape.Disabled = false;
		sprite.Modulate = Colors.White;
	}
}
