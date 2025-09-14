using Godot;

public partial class Checkpoint : Area2D
{
	[Export] public bool IsActivated { get; private set; } = false;
	[Export] public string CheckpointId = "";
	[Export] public bool AutoSave = true;
	
	private AnimatedSprite2D sprite;
	private AudioStreamPlayer2D soundPlayer;
	private bool playerInRange = false;
	private Player playerRef;
	
	public override void _Ready()
	{
		// Set up checkpoint ID if not provided
		if (string.IsNullOrEmpty(CheckpointId))
		{
			CheckpointId = $"checkpoint_{GetInstanceId()}";
		}
		
		sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		soundPlayer = GetNode<AudioStreamPlayer2D>("AudioStreamPlayer2D");
		
		// Connect signals
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		
		// Check if this checkpoint was already activated
		LoadCheckpointState();
		
		AddToGroup("Checkpoints");
		DebugCheckpointState();
	}
	
	public override void _Input(InputEvent @event)
	{
		// Check if player is in range and presses the interact key (E)
		// Cho phép kích hoạt lại checkpoint ngay cả khi đã activated
		if (@event.IsActionPressed("interact") && playerInRange && playerRef != null)
		{
			ActivateCheckpoint(playerRef);
		}
	}
	
	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			playerInRange = true;
			playerRef = body as Player;
			
			// Luôn hiển thị prompt tương tác, kể cả khi đã activated
			ShowInteractPrompt(true);
			
			// Phát hoạt ảnh "highlight" khi người chơi đến gần
			if (sprite != null && sprite.Animation != "activated")
			{
				sprite.Play("highlight");
			}
		}
	}
	
	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("Player"))
		{
			playerInRange = false;
			playerRef = null;
			
			// Hide UI prompt
			ShowInteractPrompt(false);
			
			// Trở về hoạt ảnh mặc định khi người chơi rời đi
			if (sprite != null)
			{
				if (IsActivated)
				{
					sprite.Play("activated");
				}
				else
				{
					sprite.Play("idle");
				}
			}
		}
	}
	
	public void ActivateCheckpoint(Player player)
	{
		IsActivated = true;
		
		// Visual and audio feedback
		if (sprite != null)
		{
			sprite.Play("activate_animation"); // Hoạt ảnh kích hoạt
		}
		
		if (soundPlayer != null)
		{
			soundPlayer.Play();
		}
		
		// Vẫn hiển thị prompt sau khi activated để người chơi biết có thể kích hoạt lại
		ShowInteractPrompt(true);
		
		// Save checkpoint data
		SaveCheckpointData(player);
		
		// Show notification
		ShowCheckpointNotification();
		
		GD.Print($"Checkpoint {CheckpointId} activated!");
	}
	
	private void SaveCheckpointData(Player player)
	{
		if (GameManager.Instance != null)
		{
			// Save checkpoint position and ID
			GameManager.Instance.UpdatePlayerData("last_checkpoint_id", CheckpointId);
			GameManager.Instance.UpdatePlayerData("last_checkpoint_x", GlobalPosition.X);
			GameManager.Instance.UpdatePlayerData("last_checkpoint_y", GlobalPosition.Y);
			GameManager.Instance.UpdatePlayerData("checkpoint_scene", GetTree().CurrentScene.SceneFilePath);
			
			// Mark this checkpoint as activated
			GameManager.Instance.UpdatePlayerData($"checkpoint_{CheckpointId}_activated", true);
			
			// Save player stats at checkpoint
			player.SavePlayerData();
			
			// Auto-save if enabled
			if (AutoSave)
			{
				GameManager.Instance.SaveGameDataToSlot(0); // Auto-save slot
			}
		}
	}
	
	private void LoadCheckpointState()
	{
		if (GameManager.Instance != null)
		{
			bool wasActivated = GameManager.Instance.GetPlayerData($"checkpoint_{CheckpointId}_activated", false).AsBool();
			
			// DEBUG: Print checkpoint state
			GD.Print($"Checkpoint {CheckpointId} load state: {wasActivated}");
			
			if (wasActivated)
			{
				IsActivated = true;
				if (sprite != null)
				{
					sprite.Play("activated");
				}
			}
			else
			{
				IsActivated = false;
				if (sprite != null)
				{
					sprite.Play("idle");
				}
			}
		}
		else
		{
			GD.PrintErr("GameManager instance is null in LoadCheckpointState");
		}
	}
	
	private void ShowInteractPrompt(bool show)
	{
		// Find or create an interact prompt
		var prompt = GetNodeOrNull<Label>("InteractPrompt");
		
		if (show)
		{
			if (prompt == null)
			{
				prompt = new Label();
				prompt.Name = "InteractPrompt";
				prompt.Text = "Press E to activate checkpoint";
				prompt.AddThemeFontSizeOverride("font_size", 16);
				prompt.Modulate = Colors.White;
				prompt.Position = new Vector2(0, -60);
				prompt.HorizontalAlignment = HorizontalAlignment.Center;
				AddChild(prompt);
			}
			prompt.Visible = true;
		}
		else if (prompt != null)
		{
			prompt.Visible = false;
		}
	}
	
	private void ShowCheckpointNotification()
	{
		// Create floating notification
		var notification = new Label();
		notification.Text = "Checkpoint Activated!";
		notification.AddThemeStyleboxOverride("normal", new StyleBoxFlat());
		notification.AddThemeFontSizeOverride("font_size", 18);
		notification.Modulate = Colors.Gold;
		
		// Position above checkpoint
		notification.Position = GlobalPosition + new Vector2(-50, -80);
		GetTree().CurrentScene.AddChild(notification);
		
		// Animate notification
		var tween = CreateTween();
		tween.TweenProperty(notification, "position", notification.Position + new Vector2(0, -30), 0.5f);
		tween.Parallel().TweenProperty(notification, "modulate:a", 0.0f, 2.0f);
		tween.TweenCallback(Callable.From(() => notification.QueueFree()));
	}
	
	public Vector2 GetSpawnPosition()
	{
		// Return a position slightly above the checkpoint
		return GlobalPosition + new Vector2(0, -20);
	}
	
	private void DebugCheckpointState()
	{
		GD.Print($"=== CHECKPOINT {CheckpointId} DEBUG ===");
		GD.Print($"IsActivated: {IsActivated}");
		GD.Print($"PlayerInRange: {playerInRange}");
		
		if (GameManager.Instance != null)
		{
			bool savedState = GameManager.Instance.GetPlayerData($"checkpoint_{CheckpointId}_activated", false).AsBool();
			GD.Print($"Saved state in GameManager: {savedState}");
			
			string lastCheckpointId = GameManager.Instance.GetPlayerData("last_checkpoint_id", "").AsString();
			GD.Print($"Last checkpoint ID: {lastCheckpointId}");
		}
	}
	
	// Phương thức để xử lý khi hoạt ảnh kích hoạt kết thúc
	private void OnActivationAnimationFinished()
	{
		if (sprite != null && IsActivated)
		{
			sprite.Play("activated"); // Chuyển sang hoạt ảnh đã kích hoạt
		}
	}
}
