# hurtbox_component.gd
extends Area2D
class_name HurtboxComponent

signal took_damage(amount: int)

func _ready() -> void:
	area_entered.connect(_on_area_entered)
	# Debug: In thông tin khi khởi tạo
	print("HurtboxComponent ready - Owner: ", get_parent().name)

func _on_area_entered(area: Area2D) -> void:
	print("HurtboxComponent: Area entered - ", area.name, " Type: ", area.get_class())
	
	if area is HitboxComponent:
		var hitbox = area as HitboxComponent
		print("Hitbox detected - Active: ", hitbox.active, " Damage: ", hitbox.damage)
		
		if hitbox.active:
			print("HurtboxComponent: Emitting took_damage signal - ", hitbox.damage)
			took_damage.emit(hitbox.damage)
		else:
			print("HurtboxComponent: Hitbox is not active")
	else:
		print(get_parent(),"HurtboxComponent: Area is not a HitboxComponent")
