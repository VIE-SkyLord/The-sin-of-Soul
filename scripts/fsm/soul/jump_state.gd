extends State
class_name JumpState

func enter() -> void:
	actor.velocity.y = -actor.jump_force
	actor.can_dash = true

func physics_update(delta: float) -> void:
	# Xử lý di chuyển trong khi nhảy
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	actor.velocity.x = input_direction.x * actor.run_speed * 0.8
	
	# Cập nhật hướng
	if input_direction.x != 0:
		actor.is_facing_right = input_direction.x > 0
	
	# Chuyển sang Fall khi đạt đỉnh nhảy
	if actor.velocity.y >= 0:
		transition_to("FallState")
