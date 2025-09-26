extends State
class_name FallState

func enter() -> void:
	pass

func physics_update(delta: float) -> void:
	# Xử lý di chuyển trong khi rơi
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	actor.velocity.x = input_direction.x * actor.run_speed * 0.8
	
	# Cập nhật hướng
	if input_direction.x != 0:
		actor.is_facing_right = input_direction.x > 0
	
	# Chuyển sang Run hoặc Idle khi chạm đất
	if actor.is_on_floor():
		if input_direction.length() > 0:
			transition_to("RunState")
		else:
			transition_to("IdleState")
	
	# Dash trong khi rơi
	if Input.is_action_just_pressed("dash") and actor.can_dash:
		transition_to("DashState")
