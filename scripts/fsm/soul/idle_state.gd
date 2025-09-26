extends State
class_name IdleState

func enter() -> void:pass

func update(_delta: float) -> void:
	# Chuyển sang Run nếu có input di chuyển
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if input_direction.length() > 0:
		# Cập nhật hướng trước khi chuyển state
		if input_direction.x != 0:
			actor.is_facing_right = input_direction.x > 0
		transition_to("RunState")
	
	# Chuyển sang Jump nếu nhấn nút nhảy
	if Input.is_action_just_pressed("jump") and actor.is_on_floor():
		transition_to("JumpState")
	
	# Chuyển sang Dash nếu nhấn nút dash
	if Input.is_action_just_pressed("dash") and actor.can_dash:
		transition_to("DashState")
