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
		Visible = false;
		player = GetTree().GetFirstNodeInGroup("Player") as Player;
		
		// Tạo các slot
		for (int i = 0; i < MaxSlots; i++)
		{
			SummonSlot slot = SlotScene.Instantiate() as SummonSlot;
			slot.SlotIndex = i;
			slot.Visible = i < 2; // Chỉ 2 slot đầu tiên mở khóa
			AddChild(slot);
			slots.Add(slot);
			
			// Kết nối sự kiện
			slot.Connect("SlotSelected", new Callable(this, nameof(OnSlotSelected)));
		}
		
		UpdateSlotPositions();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("open_summon_gui"))
		{
			ToggleGUI();
		}
		
		if (isOpen && @event.IsActionPressed("ui_cancel"))
		{
			CloseGUI();
		}
	}

	private void ToggleGUI()
	{
		isOpen = !isOpen;
		Visible = isOpen;
		
		if (isOpen)
		{
			GetTree().Paused = true;
			UpdateSlotInfo();
		}
		else
		{
			GetTree().Paused = false;
		}
	}

	private void CloseGUI()
	{
		isOpen = false;
		Visible = false;
		GetTree().Paused = false;
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
			if (i < player.MonsterScenes.Length && player.MonsterScenes[i] != null)
			{
				slots[i].SetMonsterInfo(
					player.MonsterScenes[i].ResourcePath,
					player.SummonCosts[i]
				);
				slots[i].Disabled = !player.IsSlotUnlocked(i); // Sửa thành phương thức
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
