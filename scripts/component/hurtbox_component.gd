# hurtbox_component.gd
extends Area2D
class_name HurtboxComponent

signal took_damage(amount: int, attacker_position: Vector2)

@export var flash_duration: float = 0.15
@export var flash_count: int = 3

var sprite: Sprite2D
var original_modulate: Color
var last_attacker_position: Vector2 = Vector2.ZERO

func _ready() -> void:
	area_entered.connect(_on_area_entered)
	
	# KẾT NỐI VỚI HEALTH COMPONENT
	call_deferred("_connect_to_health")
	
	# Tìm Sprite2D từ parent
	var parent = get_parent()
	sprite = _find_sprite(parent)
	
	if sprite:
		original_modulate = sprite.modulate
	else:
		push_warning("HurtboxComponent: Không tìm thấy Sprite2D trong parent!")
	
	print("HurtboxComponent ready - Owner: ", get_parent().name, 
		  " Layer: ", collision_layer, " Mask: ", collision_mask)

func _connect_to_health() -> void:
	"""Kết nối signal took_damage với HealthComponent"""
	var parent = get_parent()
	if parent.has_node("HealthComponent"):
		var health = parent.get_node("HealthComponent")
		if not took_damage.is_connected(health.damage):
			took_damage.connect(health.damage)
			print("HurtboxComponent: Connected to HealthComponent of ", parent.name)
	else:
		push_warning("HurtboxComponent: No HealthComponent found on ", parent.name)

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
			# Lưu vị trí của kẻ tấn công
			if hitbox.get_parent():
				last_attacker_position = hitbox.get_parent().global_position
			else:
				last_attacker_position = area.global_position
			
			print("  ✓ DAMAGE APPLIED: ", hitbox.damage)
			print("  Attacker position: ", last_attacker_position)
			
			# QUAN TRỌNG: Emit signal với 2 tham số
			took_damage.emit(hitbox.damage, last_attacker_position)
			_flash_red()
		else:
			print("  ✗ Hitbox is NOT active")
	else:
		print("✗ Area is NOT a HitboxComponent - Type: ", area.get_class())
	print("========================\n")

func get_last_attacker_position() -> Vector2:
	"""Trả về vị trí của kẻ tấn công cuối cùng"""
	return last_attacker_position

func _flash_red() -> void:
	if not sprite:
		return
	
	var red_transparent = Color(1.0, 0.0, 0.0, 0.498)
	var tween = create_tween()
	
	for i in flash_count:
		tween.tween_property(sprite, "modulate", red_transparent, flash_duration)
		tween.tween_property(sprite, "modulate", original_modulate, flash_duration)
	
	print("Flash effect started on: ", get_parent().name)
