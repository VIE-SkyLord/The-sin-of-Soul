extends StatComponent
class_name HealthComponent

signal died
signal healed(amount: float)
signal damaged(amount: int, attacker_position: Vector2)  # Signal mới để truyền thông tin damage

@export var heal_flash_color: Color = Color.GREEN
@export var heal_flash_duration: float = 0.15
@export var heal_flash_count: int = 1

var sprite: Sprite2D
var original_modulate: Color
var last_attacker_position: Vector2 = Vector2.ZERO

func _ready() -> void:
	super._ready()
	value_changed.connect(_on_value_changed)
	healed.connect(_on_healed)
	
	# Tìm Sprite2D từ parent
	sprite = _find_sprite(get_parent())
	if sprite:
		original_modulate = sprite.modulate
	else:
		push_warning("HealthComponent: Không tìm thấy Sprite2D!")

func _find_sprite(node: Node) -> Sprite2D:
	if node is Sprite2D:
		return node
	
	for child in node.get_children():
		if child is Sprite2D:
			return child
		var found = _find_sprite(child)
		if found:
			return found
	
	return null

func _on_value_changed(old_value: float, new_value: float) -> void:
	# Chỉ kiểm tra chết
	if new_value <= 0:
		print("HealthComponent: ", get_parent().name, " died! HP: ", new_value)
		died.emit()

# HÀM DAMAGE MỚI - nhận cả vị trí attacker
func damage(amount: int, attacker_position: Vector2 = Vector2.ZERO) -> void:
	"""Nhận damage và lưu vị trí kẻ tấn công"""
	last_attacker_position = attacker_position
	change(-amount)
	damaged.emit(amount, attacker_position)
	print("HealthComponent: ", get_parent().name, " took ", amount, " damage. HP: ", current_value, "/", max_value)

# Hàm heal - gọi khi muốn có hiệu ứng
func heal(amount: float) -> void:
	change(amount)
	healed.emit(amount)

func get_last_attacker_position() -> Vector2:
	"""Lấy vị trí kẻ tấn công cuối cùng"""
	return last_attacker_position

func _on_healed(amount: float) -> void:
	_flash_heal()

func _flash_heal() -> void:
	if not sprite:
		return
	
	var tween = create_tween()
	
	for i in heal_flash_count:
		tween.tween_property(sprite, "modulate", heal_flash_color, heal_flash_duration)
		tween.tween_property(sprite, "modulate", original_modulate, heal_flash_duration)
	
	print("Heal flash effect on: ", get_parent().name)
