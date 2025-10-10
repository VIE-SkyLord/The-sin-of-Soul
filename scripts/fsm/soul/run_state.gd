extends State
class_name RunState

func enter() -> void:
	pass

func physics_update(delta: float) -> void:
	# Xử lý di chuyển
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	actor.velocity.x = input_direction.x * actor.run_speed
	
	# Cập nhật hướng
	if input_direction.x != 0:
		actor.is_facing_right = input_direction.x > 0
	
	# Chuyển sang Idle nếu không di chuyển
	if input_direction.length() == 0:
		transition_to("IdleState")
	
	# Chuyển sang Jump
	if Input.is_action_just_pressed("jump") and actor.is_on_floor():
		transition_to("JumpState")
	
	# Chuyển sang Dash - KIỂM TRA STAMINA TRƯỚC
	if Input.is_action_just_pressed("dash"):
		if can_perform_dash():
			transition_to("DashState")

# Kiểm tra có thể dash không (stamina + cooldown + ground check)
func can_perform_dash() -> bool:
	if not actor.dash_component:
		return false
	
	# Kiểm tra cooldown
	if not actor.dash_component.can_dash:
		return false
	
	# Kiểm tra trên mặt đất
	if not actor.is_on_floor():
		return false
	
	# Kiểm tra stamina
	if actor.has_node("StaminaComponent"):
		var stamina_component = actor.get_node("StaminaComponent")
		if stamina_component.current_value < actor.dash_component.stamina_consume:
			return false
	
	return true
