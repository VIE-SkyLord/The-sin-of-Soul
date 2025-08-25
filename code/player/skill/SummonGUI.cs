using Godot;
using System;
using System.Collections.Generic;

public partial class SummonGUI : Control
{
	[Export] public PackedScene SlotScene;
	[Export] public float CircleRadius = 200f;
	[Export] public int MaxSlots = 6;
	
	private bool isOpen = false;
	private int selectedSlot = -1;
	private List<SummonSlot> slots = new List<SummonSlot>();
	private Player player;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // Quan trọng: Luôn xử lý input ngay cả khi tạm dừng
		Visible = false;
		// Tìm player node đúng cách hơn
		player = GetTree().Root.GetNodeOrNull<Player>("Character/Player"); // Thay "Main/Player" bằng đường dẫn đến player node của bạn
		
		// Hoặc sử dụng phương pháp tìm kiếm an toàn hơn
		if (player == null)
		{
			var players = GetTree().GetNodesInGroup("Player");
			if (players.Count > 0)
				player = players[0] as Player;
		}
		
		if (player == null)
		{
			GD.PrintErr("Không thể tìm thấy Player!");
			return;
		}
		
		// Tạo các slot
		for (int i = 0; i < MaxSlots; i++)
		{
			SummonSlot slot = SlotScene.Instantiate() as SummonSlot;
			slot.SlotIndex = i;
			AddChild(slot);
			slots.Add(slot);
			
			// Kết nối sự kiện
			slot.Connect("SlotSelected", new Callable(this, nameof(OnSlotSelected)));
		}
		
		UpdateSlotPositions();
		UpdateSlotInfo(); // Cập nhật thông tin slot ngay khi ready
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("open_summon_gui"))
		{
			ToggleGUI();
			GetViewport().SetInputAsHandled(); // Quan trọng: Đánh dấu sự kiện đã xử lý
		}
		
		if (isOpen && @event.IsActionPressed("ui_cancel"))
		{
			CloseGUI();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ToggleGUI()
	{
		isOpen = !isOpen;
		Visible = isOpen;
		
		if (isOpen)
		{
			GD.Print("Mở Summon GUI");
			GetTree().Paused = true;
			UpdateSlotInfo();
		}
		else
		{
			GD.Print("Đóng Summon GUI");
			GetTree().Paused = false;
		}
	}

	private void CloseGUI()
	{
		isOpen = false;
		Visible = false;
		GetTree().Paused = false;
		GD.Print("Đóng Summon GUI (bằng ESC)");
	}

	private void UpdateSlotPositions()
	{
		for (int i = 0; i < slots.Count; i++)
		{
			float angle = 2 * Mathf.Pi * i / slots.Count;
			Vector2 position = new Vector2(
				Mathf.Cos(angle) * CircleRadius,
				Mathf.Sin(angle) * CircleRadius
			);
			
			slots[i].Position = position + Size / 2;
		}
	}

	private void UpdateSlotInfo()
	{
		for (int i = 0; i < slots.Count; i++)
		{
			if (i < player.MonsterScenes.Length && 
				player.MonsterScenes[i] != null && 
				player.IsSlotUnlocked(i))
			{
				slots[i].SetMonsterInfo(
					player.MonsterScenes[i].ResourcePath,
					player.SummonCosts[i]
				);
			}
			else
			{
				slots[i].SetLocked();
			}
		}
	}

	private void OnSlotSelected(int slotIndex)
	{
		// Kiểm tra slot đã mở khóa chưa
		if (!player.IsSlotUnlocked(slotIndex)) // Sửa thành phương thức
		{
			GD.Print("Slot chưa được mở khóa!");
			return;
		}

		// Kiểm tra có quái vật trong slot không
		if (slotIndex >= player.MonsterScenes.Length || player.MonsterScenes[slotIndex] == null)
		{
			GD.Print("Slot không có quái vật!");
			return;
		}

		// Kiểm tra đang nhập xác hoặc đã có quái triệu hồi (sử dụng phương thức public)
		if (player.IsPossessing() || player.HasSummonedMonster()) // Sửa thành phương thức public
		{
			GD.Print("Đang nhập xác hoặc đã có quái triệu hồi!");
			return;
		}

		// Kiểm tra đủ SP
		if (player.CurSoulSP >= player.SummonCosts[slotIndex])
		{
			player.SetSelectedMonsterSlot(slotIndex); // Sửa thành phương thức public
			player.SummonMonster(slotIndex);
			CloseGUI();
		}
		else
		{
			GD.Print("Không đủ SP để triệu hồi!");
		}
	}
}
