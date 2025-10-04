# PossessedState.gd
extends State
class_name PossessedState

var is_attacking: bool = false
var last_special_attack: float = 0.0
var special_attack_cooldown: float = 10.0  # Lấy từ SpecialAttackState

func enter() -> void:
	actor.velocity = Vector2.ZERO
	if actor.has_node("Sprite2D"):
		actor.get_node("Sprite2D").modulate = Color(0.5, 1.0, 1.0)
	# Lấy transition_cooldown từ SpecialAttackState
	if actor.state_machine and actor.state_machine.has_node("SpecialAttackState"):
		special_attack_cooldown = actor.state_machine.get_node("SpecialAttackState").transition_cooldown
	print("PossessedState: Entered for ", actor.name, " with special attack cooldown ", special_attack_cooldown)

func exit() -> void:
	if actor.has_node("Sprite2D"):
		actor.get_node("Sprite2D").modulate = Color(1.0, 1.0, 1.0)
	actor.velocity = Vector2.ZERO
	actor.deactivate_light_attack()
	actor.deactivate_heavy_attack()
	is_attacking = false
	last_special_attack = 0.0
	print("PossessedState: Exited for ", actor.name)

func physics_update(delta: float) -> void:
	last_special_attack += delta  # Cập nhật thời gian hồi chiêu cho special attack

	if is_attacking:
		actor.velocity.x = 0  # Đảm bảo không di chuyển khi tấn công
		# Kiểm tra nếu hoạt ảnh tấn công kết thúc
		if not actor.animation_player.is_playing() or not "attack" in actor.animation_player.current_animation:
			is_attacking = false
			actor.deactivate_light_attack()
			actor.deactivate_heavy_attack()
			actor.play_idle_animation()
	else:
		if not actor.is_on_floor():
			actor.velocity.y += actor.gravity * delta
		else:
			actor.velocity.y = 0
		var direction = Input.get_axis("move_left", "move_right")
		actor.velocity.x = direction * actor.move_speed

		if direction != 0:
			actor.face_direction(direction)
			actor.play_run_animation()
		else:
			actor.play_idle_animation()

		if Input.is_action_just_pressed("jump") and actor.is_on_floor():
			actor.velocity.y = -(actor.jump_force if "jump_force" in actor else 400.0)

		# Normal attack không cần hồi chiêu
		if Input.is_action_just_pressed("attack"):
			is_attacking = true
			actor.stop_moving()
			actor.trigger_normal_attack()
		# Special attack cần hồi chiêu
		elif Input.is_action_just_pressed("special_attack") and last_special_attack >= special_attack_cooldown:
			is_attacking = true
			actor.stop_moving()
			actor.trigger_special_attack()
			last_special_attack = 0.0  # Đặt lại thời gian hồi chiêu

	actor.move_and_slide()
