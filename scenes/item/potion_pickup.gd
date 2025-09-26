extends Area2D

@export var potion_amount: int = 1  # Số bình được cộng thêm

func _ready() -> void:
	body_entered.connect(_on_body_entered)

func _on_body_entered(body: Node2D) -> void:
	if body.is_in_group("Player"):
		var soul: Soul = body
		soul.add_potion(potion_amount)
		queue_free()  # Xóa vật phẩm sau khi thu thập
