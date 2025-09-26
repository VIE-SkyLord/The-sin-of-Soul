# PossessedState.gd
extends State
class_name PossessedState

func enter() -> void:
	actor.velocity = Vector2.ZERO
	if actor.has_node("Sprite2D"):
		actor.get_node("Sprite2D").modulate = Color(0.5, 1.0, 1.0)
	print("PossessedState: Entered for ", actor.name)

func exit() -> void:
	if actor.has_node("Sprite2D"):
		actor.get_node("Sprite2D").modulate = Color(1.0, 1.0, 1.0)
	actor.velocity = Vector2.ZERO
	print("PossessedState: Exited for ", actor.name)

func physics_update(delta: float) -> void:
	if not actor.is_on_floor():
		actor.velocity.y += actor.gravity * delta
	else:
		actor.velocity.y = 0
	var direction = Input.get_axis("move_left", "move_right")
	actor.velocity.x = direction * actor.move_speed
	
	# Only play idle/run if no attack animation is playing
	if not actor.animation_player.is_playing() or not "attack" in actor.animation_player.current_animation:
		if direction != 0:
			actor.face_direction(direction)
			actor.play_run_animation()
		else:
			actor.play_idle_animation()
	
	if Input.is_action_just_pressed("jump") and actor.is_on_floor():
		actor.velocity.y = -(actor.jump_force if "jump_force" in actor else 400.0)
	if Input.is_action_just_pressed("attack"):
		actor.trigger_normal_attack()
	if Input.is_action_just_pressed("special_attack"):
		actor.trigger_special_attack()
	actor.move_and_slide()
