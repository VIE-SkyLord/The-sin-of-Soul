extends State
class_name DashState

func enter() -> void:
	# Thêm kiểm tra mặt đất trước khi dash
	if not actor.is_on_floor():
		# Nếu không trên mặt đất, chuyển sang state phù hợp
		var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
		if input_direction.length() > 0:
			transition_to("RunState")
		else:
			transition_to("IdleState")
		return
	
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	
	if actor.dash_component.start_dash(input_direction):
		# Dash thành công
		pass
	else:
		# Không đủ stamina/đang hồi chiêu - chuyển state khác
		if actor.is_on_floor():
			transition_to("RunState" if input_direction.length() > 0 else "IdleState")
		else:
			transition_to("FallState")

func physics_update(delta: float) -> void:
	if not actor.dash_component.is_dashing:
		# Chờ 1 frame để tránh chuyển cảnh giật
		await get_tree().process_frame
		
		if actor.is_on_floor():
			var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
			transition_to("RunState" if input_direction.length() > 0 else "IdleState")
		else:
			transition_to("FallState")

func exit() -> void:
	actor.dash_component.end_dash()
