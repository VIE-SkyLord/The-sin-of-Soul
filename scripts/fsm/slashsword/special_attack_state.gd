# SpecialAttackState.gd
extends State
class_name SpecialAttackState

@export var transition_cooldown: float = 10.0  # Hồi chiêu tùy chỉnh trong editor
var is_attacking: bool = false
var last_transition: float = 0.0

func enter() -> void:
	actor.stop_moving()
	if actor.target:
		var direction = sign(actor.target.global_position.x - actor.global_position.x)
		actor.face_direction(direction)
	actor.trigger_special_attack()
	is_attacking = true
	last_transition = 0.0
	print("SpecialAttackState: Started attack")

func physics_update(delta: float) -> void:
	last_transition += delta
	actor.velocity.x = 0  # Đảm bảo không di chuyển khi tấn công
	if is_attacking:
		# Kiểm tra nếu hoạt ảnh tấn công kết thúc
		if not actor.animation_player.is_playing() or not "attack" in actor.animation_player.current_animation:
			is_attacking = false
			actor.deactivate_light_attack()
			actor.deactivate_heavy_attack()
			if last_transition >= transition_cooldown:
				print("SpecialAttackState: Attack finished, returning to patrol")
				transition_to("PatrolState")
				last_transition = 0.0

func exit() -> void:
	actor.deactivate_light_attack()
	actor.deactivate_heavy_attack()
	is_attacking = false
