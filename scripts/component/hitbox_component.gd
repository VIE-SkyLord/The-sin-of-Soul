extends Area2D
class_name HitboxComponent

@export var damage: int = 10
@export var active: bool = true

func _ready():
	# Debug: In thông tin khi khởi tạo
	print("HitboxComponent ready - Owner: ", get_parent().name, " Damage: ", damage)

func set_active(value: bool) -> void:
	active = value
	print("HitboxComponent: Active set to ", value, " for ", get_parent().name)
	
	# Có thể thêm logic ẩn/hiện collision shape nếu cần
	for child in get_children():
		if child is CollisionShape2D or child is CollisionPolygon2D:
			child.set_deferred("disabled", not value)
			print("HitboxComponent: CollisionShape disabled = ", not value)
