using Godot;
using System.Collections.Generic;
using Godot.Collections;

public partial class LoadPanel : Panel
{
	private VBoxContainer _saveSlotContainer;
	private Button _backButton;
	
	// Individual slot buttons and delete buttons
	private List<Button> _saveSlotButtons = new List<Button>();
	private List<Button> _deleteButtons = new List<Button>();
	private int _selectedSlot = -1;

	private const int MAX_SAVE_SLOTS = 3;
	private const string SAVE_FILE_PREFIX = "user://savegame_slot_";

	public override void _Ready()
	{
		// Get node references - match the actual scene structure
		_saveSlotContainer = GetNode<VBoxContainer>("VBoxContainer/SaveSlotContainer");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");

		// Connect back button
		_backButton.Pressed += OnBackPressed;

		// Get existing slot buttons and delete buttons from scene
		GetExistingButtons();
		
		// Connect signals for existing buttons
		ConnectButtonSignals();

		// Refresh save slot display
		RefreshSaveSlots();
	}

	private void GetExistingButtons()
	{
		_saveSlotButtons.Clear();
		_deleteButtons.Clear();

		// Get buttons from the existing HBoxContainer structure
		for (int i = 0; i < MAX_SAVE_SLOTS; i++)
		{
			var hbox = _saveSlotContainer.GetNode<HBoxContainer>($"HBoxContainer{(i == 0 ? "" : (i + 1).ToString())}");
			var saveButton = hbox.GetNode<Button>($"SaveSlot{i + 1}");
			var deleteButton = hbox.GetNode<Button>("Deletebutton");

			_saveSlotButtons.Add(saveButton);
			_deleteButtons.Add(deleteButton);
		}
	}

	private void ConnectButtonSignals()
	{
		for (int i = 0; i < _saveSlotButtons.Count; i++)
		{
			int slotIndex = i; // Capture for lambda
			_saveSlotButtons[i].Pressed += () => OnSlotSelected(slotIndex);
			_deleteButtons[i].Pressed += () => OnDeletePressed(slotIndex);
			
			// Initially disable delete buttons
			_deleteButtons[i].Disabled = true;
		}
	}

	public void RefreshSaveSlots()
	{
		for (int i = 0; i < MAX_SAVE_SLOTS; i++)
		{
			string slotPath = $"user://savegame_slot_{i}.save";
			var saveButton = _saveSlotButtons[i];
			var deleteButton = _deleteButtons[i];

			if (FileAccess.FileExists(slotPath))
			{
				Godot.Collections.Dictionary saveInfo = GetSaveSlotInfo(i);

				if (saveInfo.ContainsKey("timestamp"))
				{
					double timestamp = saveInfo["timestamp"].AsDouble();
					var datetime = Time.GetDatetimeDictFromUnixTime((long)timestamp);
					
					string dateStr = $"{datetime["month"]:00}/{datetime["day"]:00}/{datetime["year"]} {datetime["hour"]:00}:{datetime["minute"]:00}";
					
					string levelName = saveInfo.GetValueOrDefault("level", "Unknown Level").AsString();
					string playtime = FormatPlaytime(saveInfo.GetValueOrDefault("playtime", 0.0).AsDouble());

					saveButton.Text = $"Slot {i + 1} - {levelName}\n{dateStr}\nPlaytime: {playtime}";
					saveButton.Disabled = false;
					deleteButton.Disabled = false;
				}
				else
				{
					saveButton.Text = $"Slot {i + 1} - Corrupted Save";
					saveButton.Disabled = true;
					deleteButton.Disabled = true;
				}
			}
			else
			{
				saveButton.Text = $"Slot {i + 1} - Empty";
				saveButton.Disabled = true;
				deleteButton.Disabled = true;
			}

			// Reset visual state
			saveButton.Modulate = Colors.White;
		}
		
		_selectedSlot = -1;
	}

	private Godot.Collections.Dictionary GetSaveSlotInfo(int slotIndex)
	{
		string slotPath = $"user://savegame_slot_{slotIndex}.save";

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

	private string FormatPlaytime(double seconds)
	{
		int hours = (int)seconds / 3600;
		int minutes = ((int)seconds % 3600) / 60;
		int secs = (int)seconds % 60;

		if (hours > 0)
		{
			return $"{hours:00}:{minutes:00}:{secs:00}";
		}
		else
		{
			return $"{minutes:00}:{secs:00}";
		}
	}

	private void OnSlotSelected(int slotIndex)
	{
		_selectedSlot = slotIndex;

		// Update button states
		for (int i = 0; i < _saveSlotButtons.Count; i++)
		{
			var button = _saveSlotButtons[i];
			if (i == slotIndex)
			{
				button.Modulate = Colors.Yellow;
			}
			else
			{
				button.Modulate = Colors.White;
			}
		}

		// Load the selected save
		LoadSaveSlot(slotIndex);
	}

	private async void LoadSaveSlot(int slotIndex)
	{
		string slotPath = SAVE_FILE_PREFIX + slotIndex + ".save";

		if (!FileAccess.FileExists(slotPath))
		{
			ShowNotification("Save file not found!", Colors.Red);
			return;
		}

		var saveFile = FileAccess.Open(slotPath, FileAccess.ModeFlags.Read);
		if (saveFile == null)
		{
			ShowNotification("Failed to open save file!", Colors.Red);
			return;
		}

		string jsonString = saveFile.GetAsText();
		saveFile.Close();

		var json = new Json();
		var parseResult = json.Parse(jsonString);

		if (parseResult != Error.Ok)
		{
			ShowNotification("Corrupted save file!", Colors.Red);
			return;
		}

		// Load the game data
		if (GameManager.Instance.LoadGameData(json.Data.AsGodotDictionary()))
		{
			ShowNotification("Game loaded successfully!", Colors.Green);

			// Transition to game after a short delay
			await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
			GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
		}
		else
		{
			ShowNotification("Failed to load game data!", Colors.Red);
		}
	}

	private void OnDeletePressed(int slotIndex)
	{
		// Show confirmation dialog
		var confirmation = new ConfirmationDialog();
		confirmation.DialogText = "Are you sure you want to delete this save file?";
		AddChild(confirmation);

		confirmation.Confirmed += () => OnDeleteConfirmed(slotIndex);
		confirmation.PopupCentered();
	}

	private void OnDeleteConfirmed(int slotIndex)
	{
		string slotPath = SAVE_FILE_PREFIX + slotIndex + ".save";

		if (FileAccess.FileExists(slotPath))
		{
			DirAccess.RemoveAbsolute(slotPath);
			ShowNotification("Save deleted!", Colors.Orange);

			// Refresh the display
			RefreshSaveSlots();
		}
	}

	private void OnBackPressed()
	{
		var mainMenu = GetParent() as MainMenu;
		mainMenu?.HideLoadPanel();
	}

	private void ShowNotification(string message, Color color)
	{
		var notification = new Label();
		notification.Text = message;
		notification.AddThemeFontSizeOverride("font_size", 16);
		notification.Modulate = color;
		notification.Position = new Vector2(Size.X / 2 - 100, 10);
		AddChild(notification);

		var tween = CreateTween();
		tween.TweenInterval(2.0f);
		tween.TweenProperty(notification, "modulate:a", 0.0f, 0.5f);
		tween.TweenCallback(Callable.From(() => notification.QueueFree()));
	}
}
