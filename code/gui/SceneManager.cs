using Godot;

public partial class SceneManager : Node
{
	public static SceneManager Instance { get; private set; }
	
	// Events for other systems to listen to
	[Signal] public delegate void SceneChangingEventHandler(string newScene);
	[Signal] public delegate void SceneChangedEventHandler(string oldScene, string newScene);
	
	private string currentScene = "";
	private bool isChangingScene = false;
	
	public override void _Ready()
	{
		// FIXED: Remove DontDestroyOnLoad - không tồn tại trong Godot
		// Trong Godot, persistence được handle bởi AutoLoad system
		if (Instance == null)
		{
			Instance = this;
			currentScene = GetTree().CurrentScene.SceneFilePath;
			GD.Print($"SceneManager initialized. Current scene: {currentScene}");
		}
		else
		{
			GD.Print("SceneManager instance already exists, removing duplicate");
			QueueFree();
		}
	}
	
	// Method chính để load game từ checkpoint
	public void LoadGameFromCheckpoint()
	{
		if (isChangingScene)
		{
			GD.Print("Scene change already in progress, ignoring request");
			return;
		}
		
		if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
		{
			string checkpointScene = GameManager.Instance.GetCheckpointScene();
			
			if (!string.IsNullOrEmpty(checkpointScene) && FileAccess.FileExists(checkpointScene))
			{
				GD.Print($"Loading game from checkpoint in scene: {checkpointScene}");
				ChangeScene(checkpointScene, true); // true = load from checkpoint
			}
			else
			{
				GD.PrintErr($"Checkpoint scene not found: {checkpointScene}");
				// Fallback to main menu or first level
				ChangeScene("res://scenes/MainMenu.tscn");
			}
		}
		else
		{
			GD.Print("No checkpoint data found");
			ChangeScene("res://scenes/MainMenu.tscn");
		}
	}
	
	// Method để bắt đầu game mới
	public void StartNewGame(string firstLevelScene)
	{
		if (isChangingScene)
		{
			GD.Print("Scene change already in progress, ignoring request");
			return;
		}
		
		// Clear checkpoint data
		if (GameManager.Instance != null)
		{
			GameManager.Instance.ClearCheckpointData();
			
			// FIXED: Reset player stats to default for new game
			GameManager.Instance.UpdatePlayerStats(100, 100, 100, 100, 100, 100);
		}
		
		GD.Print($"Starting new game: {firstLevelScene}");
		ChangeScene(firstLevelScene, false); // false = new game
	}
	
	// Method tổng quát để chuyển scene với loading và error handling
	public void ChangeScene(string scenePath, bool loadFromCheckpoint = false)
	{
		if (isChangingScene)
		{
			GD.Print("Scene change already in progress");
			return;
		}
		
		if (!FileAccess.FileExists(scenePath))
		{
			GD.PrintErr($"Scene file not found: {scenePath}");
			return;
		}
		
		isChangingScene = true;
		string oldScene = currentScene;
		
		// Emit signal trước khi chuyển scene
		EmitSignal(SignalName.SceneChanging, scenePath);
		
		// Show loading nếu cần
		ShowSimpleLoading();
		
		// Delay nhỏ để loading hiển thị
		var timer = GetTree().CreateTimer(0.1f);
		timer.Timeout += () => {
			DoSceneChange(scenePath, oldScene, loadFromCheckpoint);
		};
	}
	
	private void DoSceneChange(string scenePath, string oldScene, bool loadFromCheckpoint)
	{
		try
		{
			// Save current scene state nếu có player
			SaveCurrentSceneState();
			
			// Chuyển scene
			var error = GetTree().ChangeSceneToFile(scenePath);
			
			if (error == Error.Ok)
			{
				currentScene = scenePath;
				
				// FIXED: Use CallDeferred thay vì ProcessFrame
				CallDeferred(nameof(OnSceneChangedDeferred), oldScene, scenePath, loadFromCheckpoint);
			}
			else
			{
				GD.PrintErr($"Failed to change scene: {error}");
				isChangingScene = false;
				HideSimpleLoading();
			}
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"Exception during scene change: {e.Message}");
			isChangingScene = false;
			HideSimpleLoading();
		}
	}
	
	private void OnSceneChangedDeferred(string oldScene, string newScene, bool loadFromCheckpoint)
	{
		// Setup player nếu load từ checkpoint
		if (loadFromCheckpoint)
		{
			SetupPlayerFromCheckpoint();
		}
		
		// Emit signal sau khi chuyển scene xong
		EmitSignal(SignalName.SceneChanged, oldScene, newScene);
		
		isChangingScene = false;
		HideSimpleLoading();
		
		GD.Print($"Scene changed from {oldScene} to {newScene}");
	}
	
	private void SetupPlayerFromCheckpoint()
	{
		// Chờ một frame để đảm bảo scene đã load xong
		GetTree().ProcessFrame += () => {
			var player = GetTree().GetFirstNodeInGroup("Player") as Player;
			if (player != null && GameManager.Instance != null)
			{
				// Load player data
				player.LoadPlayerData();
				
				// Move player to checkpoint position
				Vector2 checkpointPos = GameManager.Instance.GetLastCheckpointPosition();
				if (checkpointPos != Vector2.Zero)
				{
					player.Position = checkpointPos;
					GD.Print($"Player positioned at checkpoint: {checkpointPos}");
				}
			}
			else
			{
				GD.PrintErr("Player not found in scene or GameManager is null");
			}
		};
	}
	
	private void SaveCurrentSceneState()
	{
		var player = GetTree().GetFirstNodeInGroup("Player") as Player;
		if (player != null)
		{
			player.SavePlayerData();
		}
	}
	
	// Simple loading indicator
	private Control loadingIndicator = null;
	
	private void ShowSimpleLoading()
	{
		if (loadingIndicator != null) return;
		
		loadingIndicator = new Control();
		loadingIndicator.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		
		var colorRect = new ColorRect();
		colorRect.Color = new Color(0, 0, 0, 0.7f);
		colorRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		loadingIndicator.AddChild(colorRect);
		
		var label = new Label();
		label.Text = "Loading...";
		label.AddThemeFontSizeOverride("font_size", 24);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		loadingIndicator.AddChild(label);
		
		GetTree().Root.AddChild(loadingIndicator);
	}
	
	private void HideSimpleLoading()
	{
		if (loadingIndicator != null)
		{
			loadingIndicator.QueueFree();
			loadingIndicator = null;
		}
	}
	
	// Public utility methods
	public string GetCurrentScene()
	{
		return currentScene;
	}
	
	public bool IsChangingScene()
	{
		return isChangingScene;
	}
	
	public void RestartCurrentLevel()
	{
		if (!string.IsNullOrEmpty(currentScene))
		{
			ChangeScene(currentScene, false);
		}
	}
	
	public void GoToMainMenu()
	{
		ChangeScene("res://scenes/MainMenu.tscn", false);
	}
	
	public void GoToNextLevel(string nextLevelPath)
	{
		// Save progress trước khi chuyển level
		if (GameManager.Instance != null)
		{
			var player = GetTree().GetFirstNodeInGroup("Player") as Player;
			if (player != null)
			{
				// FIXED: Trigger auto save properly
				player.SavePlayerData();
				GameManager.Instance.SaveGameDataToSlot(0); // Auto-save
			}
		}
		
		ChangeScene(nextLevelPath, false);
	}
}

// UPDATED: MainMenu integration với SceneManager đã fix
public partial class UpdatedMainMenu : Control
{
	private Button continueButton;
	private Button newGameButton;
	private Button settingsButton;
	private Button exitButton;
	
	public override void _Ready()
	{
		// Get buttons - adjust paths theo structure của bạn
		continueButton = GetNode<Button>("VBoxContainer/LoadButton"); // Sử dụng LoadButton làm Continue
		newGameButton = GetNode<Button>("VBoxContainer/StartButton");
		exitButton = GetNode<Button>("VBoxContainer/ExitButton");
		
		// Update button text và functionality
		continueButton.Text = "Continue Game";
		
		// Connect signals
		continueButton.Pressed += OnContinuePressed;
		newGameButton.Pressed += OnNewGamePressed;
		
		// Update continue button state
		UpdateContinueButton();
	}
	
	private void UpdateContinueButton()
	{
		if (GameManager.Instance != null)
		{
			bool hasCheckpoint = GameManager.Instance.HasCheckpointData();
			continueButton.Disabled = !hasCheckpoint;
			continueButton.Text = hasCheckpoint ? "Continue Game" : "No Save Data";
			
			if (!hasCheckpoint)
			{
				continueButton.Modulate = new Color(0.5f, 0.5f, 0.5f, 1.0f);
			}
		}
	}
	
	private void OnContinuePressed()
	{
		// FIXED: Sử dụng SceneManager thay vì trực tiếp change scene
		if (SceneManager.Instance != null)
		{
			SceneManager.Instance.LoadGameFromCheckpoint();
		}
		else
		{
			GD.PrintErr("SceneManager.Instance is null!");
		}
	}
	
	private void OnNewGamePressed()
	{
		// FIXED: Sử dụng SceneManager để start new game
		if (SceneManager.Instance != null)
		{
			// Thay "res://scenes/map.tscn" bằng scene đầu tiên của game
			SceneManager.Instance.StartNewGame("res://scenes/map.tscn");
		}
		else
		{
			GD.PrintErr("SceneManager.Instance is null!");
		}
	}
}
