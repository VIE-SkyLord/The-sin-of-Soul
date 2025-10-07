extends StatComponent
class_name HealthComponent

signal died
signal healed(amount: float)  # Signal mới cho hiệu ứng

@export var heal_flash_color: Color = Color.GREEN
@export var heal_flash_duration: float = 0.15
@export var heal_flash_count: int = 1

var sprite: Sprite2D
var original_modulate: Color

func _ready() -> void:
	super._ready()
	value_changed.connect(_on_value_changed)
	healed.connect(_on_healed)  # Lắng nghe signal heal
	
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
	# Chỉ kiểm tra chết, không tự động flash
	if new_value <= 0:
		died.emit()

# Hàm heal mới - gọi khi muốn có hiệu ứng
func heal(amount: float) -> void:
	change(amount)
	healed.emit(amount)  # Kích hoạt hiệu ứng

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
