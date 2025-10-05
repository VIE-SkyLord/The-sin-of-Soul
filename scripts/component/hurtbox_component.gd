# hurtbox_component.gd
extends Area2D
class_name HurtboxComponent

signal took_damage(amount: int)

func _ready() -> void:
	area_entered.connect(_on_area_entered)
	# Debug: In thông tin khi khởi tạo
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
		else:
			print("  ✗ Hitbox is NOT active")
	else:
		print("✗ Area is NOT a HitboxComponent - Type: ", area.get_class())
	print("========================\n")
