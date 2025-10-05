extends Area2D

@export var potion_amount: int = 1  # Số bình được cộng thêm

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	add_to_group("PotionPickup")

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("Player"):
		# Tìm PossessionManager để lấy Soul
		var possession_manager = get_tree().get_first_node_in_group("PossessionManager")
		if not possession_manager:
			print("PotionPickup: PossessionManager not found!")
			return
		
		# Luôn thêm bình máu cho Soul (không phải xác đang nhập)
		var soul = possession_manager.soul
		if soul and soul.has_method("add_potion"):
			soul.add_potion(potion_amount)
			print("PotionPickup: Added ", potion_amount, " potion(s) to Soul")
			queue_free()
		else:
			print("PotionPickup: Soul not found or doesn't have add_potion method")
