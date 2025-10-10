extends State
class_name IdleState

func enter() -> void:
	pass

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
	
	# Chuyển sang Dash - KIỂM TRA STAMINA TRƯỚC
	if Input.is_action_just_pressed("dash"):
		if can_perform_dash():
			transition_to("DashState")

# Kiểm tra có thể dash không
func can_perform_dash() -> bool:
	if not actor.dash_component:
		return false
	
	if not actor.dash_component.can_dash:
		return false
	
	if not actor.is_on_floor():
		return false
	
	if actor.has_node("StaminaComponent"):
		var stamina_component = actor.get_node("StaminaComponent")
		if stamina_component.current_value < actor.dash_component.stamina_consume:
			return false
	
	return true
