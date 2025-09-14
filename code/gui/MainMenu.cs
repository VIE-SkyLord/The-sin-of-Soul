using Godot;

public partial class MainMenu : Control
{
	// UI References
	private Button _startButton;
	private Button _loadButton;
	private Button _settingsButton;
	private Button _exitButton;
	private Panel _settingsPanel;
	private Panel _loadPanel;

	// Audio
	private AudioStreamPlayer _buttonHoverSound;
	private AudioStreamPlayer _buttonClickSound;
	private AudioStreamPlayer _backgroundMusic;

	// Animation
	private AnimationPlayer _animationPlayer;
	private Tween _tween;

	// Game scene paths
	private const string GAME_SCENE = "res://scenes/map.tscn"; // Adjust to your main game scene path
	private const string SAVE_FILE_PATH = "user://savegame.save";

	public override void _Ready()
	{
		// FIXED: Use CallDeferred to initialize GameManager to avoid blocking issues
		CallDeferred(nameof(InitializeGameManager));

		// Get node references
		_startButton = GetNode<Button>("VBoxContainer/StartButton");
		_loadButton = GetNode<Button>("VBoxContainer/LoadButton");
		_settingsButton = GetNode<Button>("VBoxContainer/SettingsButton");
		_exitButton = GetNode<Button>("VBoxContainer/ExitButton");
		_settingsPanel = GetNode<Panel>("SettingsPanel");
		_loadPanel = GetNodeOrNull<Panel>("LoadPanel");

		// Get audio components
		_buttonHoverSound = GetNode<AudioStreamPlayer>("ButtonHoverSound");
		_buttonClickSound = GetNode<AudioStreamPlayer>("ButtonClickSound");
		_backgroundMusic = GetNode<AudioStreamPlayer>("BackgroundMusic");

		// Get animation player
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		// Connect button signals
		_startButton.Pressed += OnStartPressed;
		_loadButton.Pressed += OnLoadPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_exitButton.Pressed += OnExitPressed;

		// Connect hover sounds
		_startButton.MouseEntered += OnButtonHover;
		_loadButton.MouseEntered += OnButtonHover;
		_settingsButton.MouseEntered += OnButtonHover;
		_exitButton.MouseEntered += OnButtonHover;

		// Check if save file exists - check for any save slots
		bool hasSaves = false;
		for (int i = 0; i < 3; i++)
		{
			if (FileAccess.FileExists($"user://savegame_slot_{i}.save"))
			{
				hasSaves = true;
				break;
			}
		}

		if (!hasSaves)
		{
			_loadButton.Disabled = true;
			_loadButton.Modulate = new Color(0.5f, 0.5f, 0.5f, 1.0f);
		}

		// Start background music
		if (_backgroundMusic != null)
		{
			_backgroundMusic.Play();
		}

		// Play entrance animation
		PlayEntranceAnimation();
	}
	
	private void InitializeGameManager()
	{
		// Initialize GameManager manually if not already done
		GameManager.Initialize(GetTree().Root);
		
		// Force settings application on startup
		var gameManager = GameManager.Instance;
		if (gameManager != null)
		{
			// This ensures video settings are properly applied
			CallDeferred(nameof(ApplyStartupSettings));
		}
	}
	
	private void ApplyStartupSettings()
	{
		var gameManager = GameManager.Instance;
		gameManager?.LoadSettings(); // This will apply all settings
	}

	private void PlayEntranceAnimation()
	{
		// Animate menu buttons sliding in
		var buttons = new Button[] { _startButton, _loadButton, _settingsButton, _exitButton };

		for (int i = 0; i < buttons.Length; i++)
		{
			var button = buttons[i];
			var originalX = button.Position.X;
			button.Modulate = new Color(1, 1, 1, 0);
			button.Position = new Vector2(button.Position.X + 300, button.Position.Y);

			_tween = CreateTween();
			_tween.SetParallel(true);
			_tween.TweenProperty(button, "modulate:a", 1.0f, 0.5f).SetDelay(i * 0.1f);
			_tween.TweenProperty(button, "position:x", originalX, 0.5f).SetDelay(i * 0.1f);
		}
	}

	private void OnStartPressed()
	{
		PlayButtonSound();
		StartNewGame();
	}

	private void OnLoadPressed()
	{
		PlayButtonSound();
		LoadGame();
	}

	private void OnSettingsPressed()
	{
		PlayButtonSound();
		ShowSettings();
	}

	private void OnExitPressed()
	{
		PlayButtonSound();
		ExitGame();
	}

	private void OnButtonHover()
	{
		if (_buttonHoverSound != null)
		{
			_buttonHoverSound.Play();
		}
	}

	private void PlayButtonSound()
	{
		if (_buttonClickSound != null)
		{
			_buttonClickSound.Play();
		}
	}

	private void StartNewGame()
	{
		// Sử dụng SceneManager thay vì trực tiếp ChangeScene
		if (SceneManager.Instance != null)
		{
			_tween = CreateTween();
			_tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
			_tween.TweenCallback(Callable.From(() => 
			{
				SceneManager.Instance.StartNewGame(GAME_SCENE);
			}));
		}
	}

	private void LoadGame()
	{
		// Update Continue button để sử dụng checkpoint system
		if (SceneManager.Instance != null)
		{
			if (GameManager.Instance != null && GameManager.Instance.HasCheckpointData())
			{
				_tween = CreateTween();
				_tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
				_tween.TweenCallback(Callable.From(() => 
				{
					SceneManager.Instance.LoadGameFromCheckpoint();
				}));
			}
			else
			{
				ShowNotification("No checkpoint data found!");
			}
		}
	}

	private void ShowSettings()
	{
		if (_settingsPanel == null) return;

		_settingsPanel.Visible = true;
		
		// Animate settings panel in
		_settingsPanel.Modulate = new Color(1, 1, 1, 0);
		_settingsPanel.Scale = new Vector2(0.8f, 0.8f);

		_tween = CreateTween();
		_tween.SetParallel(true);
		_tween.TweenProperty(_settingsPanel, "modulate:a", 1.0f, 0.3f);
		_tween.TweenProperty(_settingsPanel, "scale", Vector2.One, 0.3f);
	}

	public void HideSettings()
	{
		if (_settingsPanel == null) return;

		_tween = CreateTween();
		_tween.SetParallel(true);
		_tween.TweenProperty(_settingsPanel, "modulate:a", 0.0f, 0.3f);
		_tween.TweenProperty(_settingsPanel, "scale", new Vector2(0.8f, 0.8f), 0.3f);
		_tween.TweenCallback(Callable.From(() => _settingsPanel.Visible = false));
	}

	public void ShowLoadPanel()
	{
		if (_loadPanel == null) return;

		_loadPanel.Visible = true;
		if (_loadPanel.HasMethod("RefreshSaveSlots"))
		{
			_loadPanel.Call("RefreshSaveSlots");
		}

		// Animate load panel in
		_loadPanel.Modulate = new Color(1, 1, 1, 0);
		_loadPanel.Scale = new Vector2(0.8f, 0.8f);

		_tween = CreateTween();
		_tween.SetParallel(true);
		_tween.TweenProperty(_loadPanel, "modulate:a", 1.0f, 0.3f);
		_tween.TweenProperty(_loadPanel, "scale", Vector2.One, 0.3f);
	}

	public void HideLoadPanel()
	{
		if (_loadPanel == null) return;

		_tween = CreateTween();
		_tween.SetParallel(true);
		_tween.TweenProperty(_loadPanel, "modulate:a", 0.0f, 0.3f);
		_tween.TweenProperty(_loadPanel, "scale", new Vector2(0.8f, 0.8f), 0.3f);
		_tween.TweenCallback(Callable.From(() => _loadPanel.Visible = false));
	}

	private void ExitGame()
	{
		// Fade out and quit
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
		_tween.TweenCallback(Callable.From(() => GetTree().Quit()));
	}

	private void ShowNotification(string message)
	{
		// Create a simple notification popup
		var notification = new Label();
		notification.Text = message;
		notification.AddThemeStyleboxOverride("normal", new StyleBoxFlat());
		notification.AddThemeFontSizeOverride("font_size", 24);
		notification.Modulate = Colors.Red;
		notification.Position = new Vector2(GetViewport().GetVisibleRect().Size.X / 2 - 100, 100);
		AddChild(notification);

		_tween = CreateTween();
		_tween.TweenInterval(2.0f); // Wait 2 seconds
		_tween.TweenProperty(notification, "modulate:a", 0.0f, 1.0f);
		_tween.TweenCallback(Callable.From(() => notification.QueueFree()));
	}

	// Handle input for keyboard navigation
	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_settingsPanel != null && _settingsPanel.Visible)
			{
				HideSettings();
			}
			else if (_loadPanel != null && _loadPanel.Visible)
			{
				HideLoadPanel();
			}
		}
	}
}
