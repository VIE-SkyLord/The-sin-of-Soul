using Godot;

public partial class SettingsPanel : Panel
{
	// Audio Settings
	private HSlider _masterVolumeSlider;
	private Label _masterVolumeLabel;
	private HSlider _sfxVolumeSlider;
	private Label _sfxVolumeLabel;
	private HSlider _musicVolumeSlider;
	private Label _musicVolumeLabel;

	// Video Settings
	private OptionButton _resolutionOption;
	private CheckBox _fullscreenCheck;
	private CheckBox _vsyncCheck;

	// Buttons
	private Button _applyButton;
	private Button _resetButton;
	private Button _backButton;

	// Settings file
	private const string SETTINGS_FILE = "user://settings.cfg";
	private ConfigFile _config = new ConfigFile();

	// Available resolutions
	private readonly Vector2I[] _resolutions = {
		new Vector2I(1280, 720),
		new Vector2I(1920, 1080),
		new Vector2I(2560, 1440),
		new Vector2I(3840, 2160)
	};

	public override void _Ready()
	{
		// Get node references
		_masterVolumeSlider = GetNode<HSlider>("VBoxContainer/AudioSettings/MasterVolumeSlider");
		_masterVolumeLabel = GetNode<Label>("VBoxContainer/AudioSettings/MasterVolumeLabel");
		_sfxVolumeSlider = GetNode<HSlider>("VBoxContainer/AudioSettings/SFXVolumeSlider");
		_sfxVolumeLabel = GetNode<Label>("VBoxContainer/AudioSettings/SFXVolumeLabel");
		_musicVolumeSlider = GetNode<HSlider>("VBoxContainer/AudioSettings/MusicVolumeSlider");
		_musicVolumeLabel = GetNode<Label>("VBoxContainer/AudioSettings/MusicVolumeLabel");

		_resolutionOption = GetNode<OptionButton>("VBoxContainer/VideoSettings/ResolutionOption");
		_fullscreenCheck = GetNode<CheckBox>("VBoxContainer/VideoSettings/FullscreenCheck");
		_vsyncCheck = GetNode<CheckBox>("VBoxContainer/VideoSettings/VsyncCheck");

		_applyButton = GetNode<Button>("VBoxContainer/Buttons/ApplyButton");
		_resetButton = GetNode<Button>("VBoxContainer/Buttons/ResetButton");
		_backButton = GetNode<Button>("VBoxContainer/Buttons/BackButton");

		// Connect signals
		_masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
		_sfxVolumeSlider.ValueChanged += OnSfxVolumeChanged;
		_musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;

		_resolutionOption.ItemSelected += OnResolutionSelected;
		_fullscreenCheck.Toggled += OnFullscreenToggled;
		_vsyncCheck.Toggled += OnVsyncToggled;

		_applyButton.Pressed += OnApplyPressed;
		_resetButton.Pressed += OnResetPressed;
		_backButton.Pressed += OnBackPressed;

		// Setup resolution options
		SetupResolutionOptions();

		// Load settings when panel is ready
		LoadSettings();
	}

	// FIXED: This method is called whenever the panel becomes visible
	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged && Visible)
		{
			LoadSettingsFromGameManager();
		}
	}

	private void SetupResolutionOptions()
	{
		_resolutionOption.Clear();
		foreach (var res in _resolutions)
		{
			_resolutionOption.AddItem($"{res.X}x{res.Y}");
		}
	}

	// FIXED: Load settings from GameManager to ensure consistency
	private void LoadSettingsFromGameManager()
	{
		var gameManager = GameManager.Instance;
		if (gameManager == null) return;

		// Get current settings from GameManager
		var settings = gameManager.GetCurrentSettings();

		// Audio Settings - use TryGetValue or direct access with defaults
		float masterVol = settings.ContainsKey("master_volume") ? settings["master_volume"].AsSingle() : 100.0f;
		float sfxVol = settings.ContainsKey("sfx_volume") ? settings["sfx_volume"].AsSingle() : 100.0f;
		float musicVol = settings.ContainsKey("music_volume") ? settings["music_volume"].AsSingle() : 100.0f;

		_masterVolumeSlider.Value = masterVol;
		_sfxVolumeSlider.Value = sfxVol;
		_musicVolumeSlider.Value = musicVol;

		// Update labels
		UpdateVolumeLabels();

		// Video Settings
		bool isFullscreen = settings.ContainsKey("fullscreen") ? settings["fullscreen"].AsBool() : false;
		bool vsyncEnabled = settings.ContainsKey("vsync") ? settings["vsync"].AsBool() : true;
		int resolutionIndex = settings.ContainsKey("resolution") ? settings["resolution"].AsInt32() : 1;

		_fullscreenCheck.ButtonPressed = isFullscreen;
		_vsyncCheck.ButtonPressed = vsyncEnabled;
		
		// Ensure resolution index is valid
		if (resolutionIndex >= 0 && resolutionIndex < _resolutions.Length)
		{
			_resolutionOption.Selected = resolutionIndex;
		}

		// Update resolution dropdown state based on fullscreen
		_resolutionOption.Disabled = isFullscreen;
	}

	private void LoadSettings()
	{
		var err = _config.Load(SETTINGS_FILE);

		// Audio Settings - use default values if config doesn't exist
		float masterVol = (float)_config.GetValue("audio", "master_volume", 100.0);
		float sfxVol = (float)_config.GetValue("audio", "sfx_volume", 100.0);
		float musicVol = (float)_config.GetValue("audio", "music_volume", 100.0);

		_masterVolumeSlider.Value = masterVol;
		_sfxVolumeSlider.Value = sfxVol;
		_musicVolumeSlider.Value = musicVol;

		// Video Settings - use default values if config doesn't exist
		bool isFullscreen = (bool)_config.GetValue("video", "fullscreen", false);
		bool vsyncEnabled = (bool)_config.GetValue("video", "vsync", true);
		int resolutionIndex = (int)_config.GetValue("video", "resolution", 1);

		_fullscreenCheck.ButtonPressed = isFullscreen;
		_vsyncCheck.ButtonPressed = vsyncEnabled;
		
		// Ensure resolution index is valid
		if (resolutionIndex >= 0 && resolutionIndex < _resolutions.Length)
		{
			_resolutionOption.Selected = resolutionIndex;
		}

		// Update labels and apply settings
		UpdateVolumeLabels();
		_resolutionOption.Disabled = isFullscreen;
	}

	private void SaveSettings()
	{
		// Audio Settings
		_config.SetValue("audio", "master_volume", _masterVolumeSlider.Value);
		_config.SetValue("audio", "sfx_volume", _sfxVolumeSlider.Value);
		_config.SetValue("audio", "music_volume", _musicVolumeSlider.Value);

		// Video Settings
		_config.SetValue("video", "fullscreen", _fullscreenCheck.ButtonPressed);
		_config.SetValue("video", "vsync", _vsyncCheck.ButtonPressed);
		_config.SetValue("video", "resolution", _resolutionOption.Selected);

		// Save to file
		_config.Save(SETTINGS_FILE);

		// FIXED: Also update GameManager settings
		UpdateGameManagerSettings();
	}

	// FIXED: Update GameManager with current settings
	private void UpdateGameManagerSettings()
	{
		var gameManager = GameManager.Instance;
		if (gameManager == null) return;

		gameManager.UpdateSetting("master_volume", _masterVolumeSlider.Value);
		gameManager.UpdateSetting("sfx_volume", _sfxVolumeSlider.Value);
		gameManager.UpdateSetting("music_volume", _musicVolumeSlider.Value);
		gameManager.UpdateSetting("fullscreen", _fullscreenCheck.ButtonPressed);
		gameManager.UpdateSetting("vsync", _vsyncCheck.ButtonPressed);
		gameManager.UpdateSetting("resolution", _resolutionOption.Selected);
	}

	private void ApplyVideoSettings()
	{
		// FIXED: Apply fullscreen/windowed mode first
		if (_fullscreenCheck.ButtonPressed)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			_resolutionOption.Disabled = true;
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			_resolutionOption.Disabled = false;
			
			// Apply resolution for windowed mode
			if (_resolutionOption.Selected >= 0 && _resolutionOption.Selected < _resolutions.Length)
			{
				var selectedRes = _resolutions[_resolutionOption.Selected];
				DisplayServer.WindowSetSize(selectedRes);
				
				// Center the window
				var screenSize = DisplayServer.ScreenGetSize();
				var windowPos = new Vector2I(
					(screenSize.X - selectedRes.X) / 2,
					(screenSize.Y - selectedRes.Y) / 2
				);
				DisplayServer.WindowSetPosition(windowPos);
			}
		}

		// Apply VSync setting
		if (_vsyncCheck.ButtonPressed)
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
		}
		else
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		}
	}

	// FIXED: Update all volume labels
	private void UpdateVolumeLabels()
	{
		OnMasterVolumeChanged(_masterVolumeSlider.Value);
		OnSfxVolumeChanged(_sfxVolumeSlider.Value);
		OnMusicVolumeChanged(_musicVolumeSlider.Value);
	}

	private void OnMasterVolumeChanged(double value)
	{
		_masterVolumeLabel.Text = $"Âm lượng chính: {(int)value}%";
		// Set master audio bus volume
		float dbVolume = Mathf.LinearToDb((float)value / 100.0f);
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), dbVolume);
	}

	private void OnSfxVolumeChanged(double value)
	{
		_sfxVolumeLabel.Text = $"Hiệu ứng âm thanh: {(int)value}%";
		// Set SFX audio bus volume
		float dbVolume = Mathf.LinearToDb((float)value / 100.0f);
		int sfxBus = AudioServer.GetBusIndex("SFX");
		if (sfxBus != -1)
		{
			AudioServer.SetBusVolumeDb(sfxBus, dbVolume);
		}
	}

	private void OnMusicVolumeChanged(double value)
	{
		_musicVolumeLabel.Text = $"Nhạc nền: {(int)value}%";
		// Set Music audio bus volume
		float dbVolume = Mathf.LinearToDb((float)value / 100.0f);
		int musicBus = AudioServer.GetBusIndex("Music");
		if (musicBus != -1)
		{
			AudioServer.SetBusVolumeDb(musicBus, dbVolume);
		}
	}

	private void OnResolutionSelected(long index)
	{
		// Only apply resolution immediately if not in fullscreen
		if (!_fullscreenCheck.ButtonPressed && index >= 0 && index < _resolutions.Length)
		{
			var selectedRes = _resolutions[index];
			DisplayServer.WindowSetSize(selectedRes);
			
			// Center the window
			var screenSize = DisplayServer.ScreenGetSize();
			var windowPos = new Vector2I(
				(screenSize.X - selectedRes.X) / 2,
				(screenSize.Y - selectedRes.Y) / 2
			);
			DisplayServer.WindowSetPosition(windowPos);
		}
	}

	private void OnFullscreenToggled(bool buttonPressed)
	{
		if (buttonPressed)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			_resolutionOption.Disabled = true;
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			_resolutionOption.Disabled = false;
			// Apply current resolution selection
			OnResolutionSelected(_resolutionOption.Selected);
		}
	}

	private void OnVsyncToggled(bool buttonPressed)
	{
		if (buttonPressed)
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
		}
		else
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		}
	}

	private void OnApplyPressed()
	{
		SaveSettings();
		ApplyVideoSettings();
		ShowNotification("Cài đặt đã được áp dụng!", Colors.Green);
	}

	private void OnResetPressed()
	{
		// Reset to default values
		_masterVolumeSlider.Value = 100;
		_sfxVolumeSlider.Value = 100;
		_musicVolumeSlider.Value = 100;
		_fullscreenCheck.ButtonPressed = false;
		_vsyncCheck.ButtonPressed = true;
		_resolutionOption.Selected = 1; // Default to 1920x1080

		UpdateVolumeLabels();
		ApplyVideoSettings();
		ShowNotification("Cài đặt đã được đặt lại!", Colors.Orange);
	}

	private void OnBackPressed()
	{
		var mainMenu = GetParent() as MainMenu;
		mainMenu?.HideSettings();
	}

	private void ShowNotification(string message, Color color)
	{
		var notification = new Label();
		notification.Text = message;
		notification.AddThemeFontSizeOverride("font_size", 16);
		notification.Modulate = color;
		notification.Position = new Vector2(Size.X / 2 - 50, 10);
		AddChild(notification);

		var tween = CreateTween();
		tween.TweenInterval(1.5f);
		tween.TweenProperty(notification, "modulate:a", 0.0f, 0.5f);
		tween.TweenCallback(Callable.From(() => notification.QueueFree()));
	}
}
