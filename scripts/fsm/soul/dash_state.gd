extends State
class_name DashState

var dash_started: bool = false  # Flag để track dash có thực sự bắt đầu không

func enter() -> void:
	dash_started = false
	
	# Kiểm tra mặt đất trước
	if not actor.is_on_floor():
		var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
		if input_direction.length() > 0:
			transition_to("RunState")
		else:
			transition_to("IdleState")
		return
	
	var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	
	# Thử bắt đầu dash
	if actor.dash_component.start_dash(input_direction):
		# Dash thành công - BÂY GIỜ MỚI CHẠY ANIMATION
		dash_started = true
		
		# Chạy animation dash nếu có
		if actor.has_node("AnimationPlayer"):
			var anim_player = actor.get_node("AnimationPlayer")
			if anim_player.has_animation("dash"):
				anim_player.play("dash")
	else:
		# Không đủ stamina/đang hồi chiêu - KHÔNG CHẠY ANIMATION
		# Chuyển state khác ngay lập tức
		if actor.is_on_floor():
			transition_to("RunState" if input_direction.length() > 0 else "IdleState")
		else:
			transition_to("FallState")

func physics_update(delta: float) -> void:
	# Chỉ xử lý khi dash thực sự đã bắt đầu
	if not dash_started:
		return
	
	if not actor.dash_component.is_dashing:
		# Chờ 1 frame để tránh chuyển cảnh giật
		await get_tree().process_frame
		
		if actor.is_on_floor():
			var input_direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
			transition_to("RunState" if input_direction.length() > 0 else "IdleState")
		else:
			transition_to("FallState")

func exit() -> void:
	# Chỉ end dash và stop animation khi dash đã thực sự bắt đầu
	if dash_started:
		actor.dash_component.end_dash()
		
		# Dừng animation và reset về idle/run animation
		if actor.has_node("AnimationPlayer"):
			var anim_player = actor.get_node("AnimationPlayer")
			# Reset collision về bình thường bằng cách chạy animation idle
			if anim_player.has_animation("idle"):
				anim_player.play("idle")
	
	dash_started = false
