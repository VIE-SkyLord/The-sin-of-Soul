# NormalAttackState.gd
extends State
class_name NormalAttackState

var is_attacking: bool = false

func enter() -> void:
	actor.stop_moving()
	if actor.target:
		var direction = sign(actor.target.global_position.x - actor.global_position.x)
		actor.face_direction(direction)
	actor.trigger_normal_attack()
	is_attacking = true
	print("NormalAttackState: Started attack")

func physics_update(delta: float) -> void:
	actor.velocity.x = 0  # Đảm bảo không di chuyển khi tấn công
	if is_attacking:
		# Kiểm tra nếu hoạt ảnh tấn công kết thúc
		if not actor.animation_player.is_playing() or not "attack" in actor.animation_player.current_animation:
			is_attacking = false
			actor.deactivate_light_attack()
			print("NormalAttackState: Attack finished, returning to patrol")
			transition_to("PatrolState")

func exit() -> void:
	actor.deactivate_light_attack()
	is_attacking = false
