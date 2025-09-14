using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class GameManager : Node
{
	// Singleton instance
	private static GameManager _instance;
	public static GameManager Instance 
	{ 
		get 
		{
			if (_instance == null)
			{
				_instance = new GameManager();
			}
			return _instance;
		}
	}

	// Game state data
	private Godot.Collections.Dictionary<string, Variant> _playerData = new Godot.Collections.Dictionary<string, Variant>();
	private Godot.Collections.Dictionary<string, Variant> _gameSettings = new Godot.Collections.Dictionary<string, Variant>();
	private string _currentLevel = "";
	private Godot.Collections.Dictionary<string, Variant> _gameProgress = new Godot.Collections.Dictionary<string, Variant>();

	// Save file paths
	private const string SAVE_FILE_PATH = "user://savegame.save";
	private const string SETTINGS_FILE_PATH = "user://settings.cfg";
	private const string SAVE_FILE_PREFIX = "user://savegame_slot_";

	// Available resolutions
	private readonly Vector2I[] _resolutions = {
		new Vector2I(1280, 720),
		new Vector2I(1920, 1080),
		new Vector2I(2560, 1440),
		new Vector2I(3840, 2160)
	};

	// Signals for state changes
	[Signal] public delegate void GameDataLoadedEventHandler();
	[Signal] public delegate void SettingsChangedEventHandler();

	// Initialize GameManager manually
	public static void Initialize(Node parent)
	{
		if (_instance == null)
		{
			_instance = new GameManager();
			_instance.Name = "GameManager";
			parent.AddChild(_instance);
			_instance.LoadSettings();
			GD.Print("GameManager initialized manually");
		}
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		LoadSettings();
	}

	// FIXED: Add method to get current settings
	public Godot.Collections.Dictionary<string, Variant> GetCurrentSettings()
	{
		return _gameSettings;
	}

	// FIXED: Add method to update individual settings
	public void UpdateSetting(string key, Variant value)
	{
		_gameSettings[key] = value;
	}

	// Save game data to specific slot
	public bool SaveGameDataToSlot(int slotIndex)
	{
		var saveData = new Godot.Collections.Dictionary<string, Variant>
		{
			["player"] = _playerData,
			["level"] = _currentLevel,
			["progress"] = _gameProgress,
			["timestamp"] = Time.GetUnixTimeFromSystem()
		};

		string slotPath = $"user://savegame_slot_{slotIndex}.save";
		var saveFile = FileAccess.Open(slotPath, FileAccess.ModeFlags.Write);
		if (saveFile != null)
		{
			saveFile.StoreString(Json.Stringify(saveData));
			saveFile.Close();
			GD.Print($"Game saved to slot {slotIndex} successfully");
			return true;
		}
		else
		{
			GD.Print($"Failed to save game to slot {slotIndex}");
			return false;
		}
	}

	// Load game data from specific slot
	public bool LoadGameDataFromSlot(int slotIndex)
	{
		string slotPath = $"user://savegame_slot_{slotIndex}.save";

		if (!FileAccess.FileExists(slotPath))
		{
			GD.Print($"No save file found in slot {slotIndex}");
			return false;
		}

		var saveFile = FileAccess.Open(slotPath, FileAccess.ModeFlags.Read);
		if (saveFile == null)
		{
			GD.Print($"Failed to open save file in slot {slotIndex}");
			return false;
		}

		string jsonString = saveFile.GetAsText();
		saveFile.Close();

		var json = new Json();
		var parseResult = json.Parse(jsonString);

		if (parseResult != Error.Ok)
		{
			GD.Print($"Failed to parse save file in slot {slotIndex}");
			return false;
		}

		return LoadGameData(json.Data.AsGodotDictionary());
	}

	public bool LoadGameData(Godot.Collections.Dictionary customData = null)
	{
		Godot.Collections.Dictionary dataToLoad = customData;

		if (dataToLoad == null)
		{
			if (!FileAccess.FileExists(SAVE_FILE_PATH))
			{
				GD.Print("No save file found");
				return false;
			}

			var saveFile = FileAccess.Open(SAVE_FILE_PATH, FileAccess.ModeFlags.Read);
			if (saveFile == null)
			{
				GD.Print("Failed to open save file");
				return false;
			}

			string jsonString = saveFile.GetAsText();
			saveFile.Close();

			var json = new Json();
			var parseResult = json.Parse(jsonString);

			if (parseResult != Error.Ok)
			{
				GD.Print("Failed to parse save file");
				return false;
			}

			dataToLoad = json.Data.AsGodotDictionary();
		}

		_playerData = dataToLoad.GetValueOrDefault("player", new Godot.Collections.Dictionary<string, Variant>()).As<Godot.Collections.Dictionary<string, Variant>>();
		_currentLevel = dataToLoad.GetValueOrDefault("level", "").AsString();
		_gameProgress = dataToLoad.GetValueOrDefault("progress", new Godot.Collections.Dictionary<string, Variant>()).As<Godot.Collections.Dictionary<string, Variant>>();

		EmitSignal(SignalName.GameDataLoaded);
		GD.Print("Game loaded successfully");
		return true;
	}

	// Settings management
	public void SaveSettings()
	{
		var config = new ConfigFile();

		// Audio settings
		config.SetValue("audio", "master_volume", _gameSettings.GetValueOrDefault("master_volume", 100));
		config.SetValue("audio", "sfx_volume", _gameSettings.GetValueOrDefault("sfx_volume", 100));
		config.SetValue("audio", "music_volume", _gameSettings.GetValueOrDefault("music_volume", 100));

		// Video settings
		config.SetValue("video", "fullscreen", _gameSettings.GetValueOrDefault("fullscreen", false));
		config.SetValue("video", "vsync", _gameSettings.GetValueOrDefault("vsync", true));
		config.SetValue("video", "resolution", _gameSettings.GetValueOrDefault("resolution", 1));

		config.Save(SETTINGS_FILE_PATH);
		EmitSignal(SignalName.SettingsChanged);
		GD.Print("Settings saved successfully");
	}

	public void LoadSettings()
	{
		var config = new ConfigFile();
		var err = config.Load(SETTINGS_FILE_PATH);

		if (err == Error.Ok)
		{
			GD.Print("Settings loaded from file");
			_gameSettings["master_volume"] = config.GetValue("audio", "master_volume", 100);
			_gameSettings["sfx_volume"] = config.GetValue("audio", "sfx_volume", 100);
			_gameSettings["music_volume"] = config.GetValue("audio", "music_volume", 100);
			_gameSettings["fullscreen"] = config.GetValue("video", "fullscreen", false);
			_gameSettings["vsync"] = config.GetValue("video", "vsync", true);
			_gameSettings["resolution"] = config.GetValue("video", "resolution", 1);
		}
		else
		{
			GD.Print("No settings file found, using defaults");
			ResetSettingsToDefault();
		}

		// FIXED: Always apply settings after loading
		ApplyAllSettings();
	}

	// FIXED: Apply all settings (both audio and video)
	private void ApplyAllSettings()
	{
		ApplyAudioSettings();
		ApplyVideoSettings();
	}

	private void ApplyAudioSettings()
	{
		// Apply master volume
		float masterDb = Mathf.LinearToDb(_gameSettings["master_volume"].AsSingle() / 100.0f);
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), masterDb);

		// Apply SFX volume
		int sfxBus = AudioServer.GetBusIndex("SFX");
		if (sfxBus != -1)
		{
			float sfxDb = Mathf.LinearToDb(_gameSettings["sfx_volume"].AsSingle() / 100.0f);
			AudioServer.SetBusVolumeDb(sfxBus, sfxDb);
		}

		// Apply Music volume
		int musicBus = AudioServer.GetBusIndex("Music");
		if (musicBus != -1)
		{
			float musicDb = Mathf.LinearToDb(_gameSettings["music_volume"].AsSingle() / 100.0f);
			AudioServer.SetBusVolumeDb(musicBus, musicDb);
		}
	}

	// FIXED: Improved video settings application
	private void ApplyVideoSettings()
	{
		bool isFullscreen = _gameSettings["fullscreen"].AsBool();
		bool vsyncEnabled = _gameSettings["vsync"].AsBool();
		int resolutionIndex = _gameSettings.GetValueOrDefault("resolution", 1).AsInt32();

		// Ensure resolution index is valid
		if (resolutionIndex < 0 || resolutionIndex >= _resolutions.Length)
		{
			resolutionIndex = 1; // Default to 1920x1080
			_gameSettings["resolution"] = resolutionIndex;
		}

		// Apply fullscreen/windowed mode
		if (isFullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			GD.Print("Applied fullscreen mode");
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			
			// Apply resolution for windowed mode
			var selectedRes = _resolutions[resolutionIndex];
			DisplayServer.WindowSetSize(selectedRes);
			
			// Center the window
			var screenSize = DisplayServer.ScreenGetSize();
			var windowPos = new Vector2I(
				(screenSize.X - selectedRes.X) / 2,
				(screenSize.Y - selectedRes.Y) / 2
			);
			DisplayServer.WindowSetPosition(windowPos);
			
			GD.Print($"Applied windowed mode with resolution: {selectedRes.X}x{selectedRes.Y}");
		}

		// Apply VSync
		if (vsyncEnabled)
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
			GD.Print("VSync enabled");
		}
		else
		{
			DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
			GD.Print("VSync disabled");
		}
	}

	private void ResetSettingsToDefault()
	{
		_gameSettings = new Godot.Collections.Dictionary<string, Variant>
		{
			["master_volume"] = 100,
			["sfx_volume"] = 100,
			["music_volume"] = 100,
			["fullscreen"] = false,
			["vsync"] = true,
			["resolution"] = 1 // Default to 1920x1080
		};

		GD.Print("Settings reset to default values");
	}

	// Player data management
	public void UpdatePlayerData(string key, Variant value)
	{
		_playerData[key] = value;
	}

	public Variant GetPlayerData(string key, Variant defaultValue = default)
	{
		return _playerData.GetValueOrDefault(key, defaultValue);
	}

	// Player stats integration
	public void UpdatePlayerStats(int soulHP, int soulMP, int soulSP, int maxHP, int maxMP, int maxSP)
	{
		_playerData["soul_hp"] = soulHP;
		_playerData["soul_mp"] = soulMP;
		_playerData["soul_sp"] = soulSP;
		_playerData["max_soul_hp"] = maxHP;
		_playerData["max_soul_mp"] = maxMP;
		_playerData["max_soul_sp"] = maxSP;
	}

	public System.Collections.Generic.Dictionary<string, int> GetPlayerStats()
	{
		return new System.Collections.Generic.Dictionary<string, int>
		{
			["soul_hp"] = _playerData.GetValueOrDefault("soul_hp", 100).AsInt32(),
			["soul_mp"] = _playerData.GetValueOrDefault("soul_mp", 100).AsInt32(),
			["soul_sp"] = _playerData.GetValueOrDefault("soul_sp", 100).AsInt32(),
			["max_soul_hp"] = _playerData.GetValueOrDefault("max_soul_hp", 100).AsInt32(),
			["max_soul_mp"] = _playerData.GetValueOrDefault("max_soul_mp", 100).AsInt32(),
			["max_soul_sp"] = _playerData.GetValueOrDefault("max_soul_sp", 100).AsInt32()
		};
	}

	// Monster collection system
	public void SaveUnlockedMonsters(int[] unlockedSlots)
	{
		_playerData["unlocked_monster_slots"] = unlockedSlots;
	}

	public int[] GetUnlockedMonsters()
	{
		var defaultSlots = new int[] { 1, 1, 0, 0, 0, 0 };
		if (_playerData.ContainsKey("unlocked_monster_slots"))
		{
			return _playerData["unlocked_monster_slots"].AsInt32Array();
		}
		return defaultSlots;
	}

	// Utility functions
	public bool HasSaveFileInSlot(int slotIndex)
	{
		return FileAccess.FileExists($"user://savegame_slot_{slotIndex}.save");
	}

	private Godot.Collections.Dictionary GetSaveSlotInfo(int slotIndex)
	{
		string slotPath = SAVE_FILE_PREFIX + slotIndex + ".save";

		if (!FileAccess.FileExists(slotPath))
		{
			return new Godot.Collections.Dictionary();
		}

		var saveFile = FileAccess.Open(slotPath, FileAccess.ModeFlags.Read);
		if (saveFile == null)
		{
			return new Godot.Collections.Dictionary();
		}

		string jsonString = saveFile.GetAsText();
		saveFile.Close();

		var json = new Json();
		var parseResult = json.Parse(jsonString);

		if (parseResult != Error.Ok)
		{
			return new Godot.Collections.Dictionary();
		}

		return json.Data.AsGodotDictionary();
	}

	public bool DeleteSaveFile(int slotIndex)
	{
		string filePath = $"user://savegame_slot_{slotIndex}.save";

		if (FileAccess.FileExists(filePath))
		{
			DirAccess.RemoveAbsolute(filePath);
			GD.Print($"Save file deleted: {filePath}");
			return true;
		}
		return false;
	}

	// Scene management
	public void ChangeSceneTo(string scenePath)
	{
		GetTree().ChangeSceneToFile(scenePath);
	}
	
	private Vector2 lastCheckpointPosition = Vector2.Zero;

	public void SetCheckpointPosition(Vector2 position)
	{
		lastCheckpointPosition = position;
	}

	public Vector2 GetLastCheckpointPosition()
	{
		float x = GetPlayerData("last_checkpoint_x", 0).AsSingle();
		float y = GetPlayerData("last_checkpoint_y", 0).AsSingle();
		return new Vector2(x, y);
	}
	
	public string GetLastCheckpointId()
	{
		return GetPlayerData("last_checkpoint_id", "").AsString();
	}
	
	public string GetCheckpointScene()
	{
		return GetPlayerData("checkpoint_scene", "").AsString();
	}
	
	public bool HasCheckpointData()
	{
		string checkpointId = GetLastCheckpointId();
		return !string.IsNullOrEmpty(checkpointId);
	}
	
	public void ClearCheckpointData()
	{
		UpdatePlayerData("last_checkpoint_id", "");
		UpdatePlayerData("last_checkpoint_x", 0);
		UpdatePlayerData("last_checkpoint_y", 0);
		UpdatePlayerData("checkpoint_scene", "");
	}
}
