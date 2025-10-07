# hurtbox_component.gd
extends Area2D
class_name HurtboxComponent

signal took_damage(amount: int)

@export var flash_duration: float = 0.15  # Thời gian mỗi lần nháy
@export var flash_count: int = 3  # Số lần nháy

var sprite: Sprite2D
var original_modulate: Color

func _ready() -> void:
	area_entered.connect(_on_area_entered)
	
	# Tìm Sprite2D từ parent (tìm cả con sâu hơn)
	var parent = get_parent()
	sprite = _find_sprite(parent)
	
	if sprite:
		original_modulate = sprite.modulate
	else:
		push_warning("HurtboxComponent: Không tìm thấy Sprite2D trong parent!")

func _find_sprite(node: Node) -> Sprite2D:
	# Kiểm tra node hiện tại
	if node is Sprite2D:
		return node
	
	# Tìm trong các con
	for child in node.get_children():
		if child is Sprite2D:
			return child
		# Tìm đệ quy nếu cần
		var found = _find_sprite(child)
		if found:
			return found
	
	return null
	
	print("HurtboxComponent ready - Owner: ", get_parent().name, 
		  " Layer: ", collision_layer, " Mask: ", collision_mask)

func _on_area_entered(area: Area2D) -> void:
	print("\n=== HURTBOX COLLISION ===")
	print("Owner: ", get_parent().name)
	print("Hurtbox Layer: ", collision_layer, " Mask: ", collision_mask)
	print("Area entered: ", area.name, " from ", area.get_parent().name if area.get_parent() else "no parent")
	print("Area Layer: ", area.collision_layer, " Mask: ", area.collision_mask)
	
	if area is HitboxComponent:
		var hitbox = area as HitboxComponent
		print("✓ Hitbox detected!")
		print("  - Active: ", hitbox.active)
		print("  - Damage: ", hitbox.damage)
		
		if hitbox.active:
			print("  ✓ DAMAGE APPLIED: ", hitbox.damage)
			took_damage.emit(hitbox.damage)
			_flash_red()  # Kích hoạt hiệu ứng nháy đỏ
		else:
			print("  ✗ Hitbox is NOT active")
	else:
		print("✗ Area is NOT a HitboxComponent - Type: ", area.get_class())
	print("========================\n")

func _flash_red() -> void:
	if not sprite:
		return
	
	# Màu đỏ mờ với alpha = 127/255 ≈ 0.498
	var red_transparent = Color(1.0, 0.0, 0.0, 0.498)
	
	# Tạo tween để nháy đỏ
	var tween = create_tween()
	
	for i in flash_count:
		# Nháy sang màu đỏ mờ
		tween.tween_property(sprite, "modulate", red_transparent, flash_duration)
		# Trở về màu gốc
		tween.tween_property(sprite, "modulate", original_modulate, flash_duration)
	
	print("Flash effect started on: ", get_parent().name)
