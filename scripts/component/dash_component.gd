# dash_component.gd
extends Node
class_name DashComponent

# Các tham số dash
@export var max_dash_distance: float = 320
@export var dash_duration: float = 0.5
@export var dash_cooldown: float = 1.0
@export var stamina_consume: float = 20
@export var safe_stop_deceleration: float = 500.0
@export var safe_position_check_distance: float = 80.0  # Khoảng cách kiểm tra vị trí an toàn
@export var max_backtrack_attempts: int = 10  # Số lần thử lùi lại

# References
var actor: Soul
var stamina_component: StaminaComponent
var collision_shape: CollisionShape2D  # Shape để kiểm tra overlap

# Trạng thái nội bộ
var is_dashing: bool = false
var is_stopping: bool = false
var is_repositioning: bool = false  # Đang tìm vị trí an toàn
var dash_direction: Vector2
var dash_timer: float = 0.0
var cooldown_timer: float = 0.0
var can_dash: bool = true
var dash_distance_traveled: float = 0.0
var previous_position: Vector2
var safe_position: Vector2  # Vị trí an toàn cuối cùng
var dash_path_positions: Array[Vector2] = []  # Lưu các vị trí đã đi qua

# Biến lưu màu gốc
var original_modulate: Color

func _ready():
	# Lấy reference đến actor
	actor = get_parent()
	
	# Kiểm tra actor có tồn tại không
	if actor:
		original_modulate = actor.modulate
		previous_position = actor.global_position
		safe_position = actor.global_position
	else:
		push_error("DashComponent: Không thể lấy reference đến actor!")
	
	# Tìm StaminaComponent
	stamina_component = actor.get_node("StaminaComponent") if actor and actor.has_node("StaminaComponent") else null
	
	# Tìm CollisionShape2D của actor để kiểm tra overlap
	find_collision_shape()

func find_collision_shape():
	if not actor:
		return
	
	# Tìm CollisionShape2D trong actor
	for child in actor.get_children():
		if child is CollisionShape2D:
			collision_shape = child
			break

func _physics_process(delta):
	update_cooldown(delta)
	update_dash_movement(delta)
	update_safe_stop(delta)
	update_repositioning(delta)
	
	# Lưu vị trí an toàn trong khi dash
	if is_dashing:
		record_safe_positions()
		check_max_distance()

# Cập nhật thời gian hồi chiêu dash
func update_cooldown(delta: float):
	if not can_dash:
		cooldown_timer += delta
		if cooldown_timer >= dash_cooldown:
			can_dash = true
			cooldown_timer = 0.0

# Lưu lại các vị trí đã đi qua (chỉ lưu khi không overlap)
func record_safe_positions():
	if not actor or not actor.is_on_floor():
		return
	
	var current_pos = actor.global_position
	var distance_from_last = 0.0
	
	if dash_path_positions.size() > 0:
		distance_from_last = current_pos.distance_to(dash_path_positions[-1])
	
	# Lưu vị trí mỗi 20 pixel
	if dash_path_positions.size() == 0 or distance_from_last >= 20.0:
		if is_position_safe(current_pos):
			dash_path_positions.append(current_pos)
			safe_position = current_pos

# Kiểm tra vị trí có an toàn không (không overlap với enemy)
func is_position_safe(pos: Vector2) -> bool:
	if not actor or not collision_shape:
		return true
	
	# Tạo query để kiểm tra overlap
	var space_state = actor.get_world_2d().direct_space_state
	var query = PhysicsShapeQueryParameters2D.new()
	
	# Sử dụng shape của actor
	query.shape = collision_shape.shape
	query.transform = Transform2D(0, pos)
	query.collision_mask = 2  # Layer của enemy
	query.exclude = [actor.get_rid()]
	
	var results = space_state.intersect_shape(query, 1)
	
	# An toàn nếu không overlap với enemy
	return results.size() == 0

# Tìm vị trí an toàn gần nhất
func find_nearest_safe_position() -> Vector2:
	if not actor:
		return actor.global_position
	
	var current_pos = actor.global_position
	
	# 1. Kiểm tra vị trí hiện tại trước
	if is_position_safe(current_pos):
		return current_pos
	
	# 2. Tìm trong danh sách vị trí đã đi qua (từ gần đến xa)
	for i in range(dash_path_positions.size() - 1, -1, -1):
		var test_pos = dash_path_positions[i]
		if is_position_safe(test_pos):
			return test_pos
	
	# 3. Thử lùi lại theo hướng ngược lại
	var back_direction = -dash_direction
	for i in range(1, max_backtrack_attempts + 1):
		var test_pos = current_pos + back_direction * (i * 10.0)
		if is_position_safe(test_pos):
			return test_pos
	
	# 4. Thử các hướng xung quanh
	var angles = [45, -45, 90, -90, 135, -135]
	for angle in angles:
		var test_direction = dash_direction.rotated(deg_to_rad(angle))
		for dist in [20.0, 40.0, 60.0]:
			var test_pos = current_pos + test_direction * dist
			if is_position_safe(test_pos):
				return test_pos
	
	# 5. Cuối cùng dùng safe_position đã lưu
	return safe_position

# Kiểm tra quãng đường đã di chuyển
func check_max_distance():
	if not actor:
		return
	
	var distance_this_frame = actor.global_position.distance_to(previous_position)
	dash_distance_traveled += distance_this_frame
	previous_position = actor.global_position
	
	# Nếu đã đi đủ xa hoặc bị kẹt, dừng lại
	if dash_distance_traveled >= max_dash_distance or distance_this_frame < 1.0:
		begin_safe_stop()

# Xử lý chuyển động khi đang dash
func update_dash_movement(delta: float):
	if is_dashing:
		dash_timer += delta
		
		# Tính vận tốc dash
		var dash_speed = max_dash_distance / dash_duration
		
		# Giảm tốc dần khi sắp hết thời gian dash
		var time_remaining = dash_duration - dash_timer
		if time_remaining < 0.1:
			var fade_factor = time_remaining / 0.1
			dash_speed *= fade_factor
		
		actor.velocity = dash_direction * dash_speed
		
		if dash_timer >= dash_duration:
			begin_safe_stop()

# Xử lý dừng lại an toàn
func update_safe_stop(delta: float):
	if is_stopping and actor:
		var current_speed = actor.velocity.length()
		
		if current_speed > 10.0:
			var deceleration = safe_stop_deceleration * delta
			var new_speed = max(0.0, current_speed - deceleration)
			
			if actor.velocity.length() > 0:
				actor.velocity = actor.velocity.normalized() * new_speed
		else:
			# Khi đã gần dừng, kiểm tra vị trí có an toàn không
			check_and_reposition()

# Kiểm tra và di chuyển đến vị trí an toàn
func check_and_reposition():
	if not actor:
		complete_stop()
		return
	
	var current_pos = actor.global_position
	
	# Nếu vị trí hiện tại an toàn, hoàn tất
	if is_position_safe(current_pos):
		complete_stop()
		return
	
	# Nếu không an toàn, tìm vị trí an toàn và di chuyển đến đó
	var target_safe_pos = find_nearest_safe_position()
	
	if target_safe_pos.distance_to(current_pos) > 5.0:
		# Bắt đầu repositioning
		is_stopping = false
		is_repositioning = true
		safe_position = target_safe_pos
	else:
		complete_stop()

# Xử lý di chuyển đến vị trí an toàn
func update_repositioning(delta: float):
	if not is_repositioning or not actor:
		return
	
	var current_pos = actor.global_position
	var distance = current_pos.distance_to(safe_position)
	
	if distance < 5.0:
		# Đã đến vị trí an toàn
		actor.global_position = safe_position
		is_repositioning = false
		complete_stop()
		return
	
	# Di chuyển nhanh đến vị trí an toàn
	var direction = (safe_position - current_pos).normalized()
	var move_speed = min(800.0, distance / delta)
	actor.velocity = direction * move_speed

# Bắt đầu quá trình dừng an toàn
func begin_safe_stop():
	if not is_dashing:
		return
	
	is_dashing = false
	is_stopping = true
	dash_timer = 0.0
	
	# Giảm opacity
	if actor:
		actor.modulate = Color(0.85, 0.85, 1.0, 0.9)

# Hoàn tất việc dừng
func complete_stop():
	is_stopping = false
	is_repositioning = false
	
	if actor:
		# Reset velocity ngang
		var horizontal_velocity = actor.velocity.x
		if abs(horizontal_velocity) < 50.0:
			actor.velocity.x = 0.0
		
		# Khôi phục màu sắc
		actor.modulate = original_modulate
	
	# Clear dash path
	dash_path_positions.clear()

# Bắt đầu dash
func start_dash(input_direction: Vector2) -> bool:
	if is_stopping or is_repositioning:
		return false
	
	if not can_dash or not actor.is_on_floor():
		return false
	
	if stamina_component and stamina_component.current_value < stamina_consume:
		return false
	
	if not actor:
		return false
	
	# Tiêu hao stamina
	if stamina_component:
		stamina_component.change(-stamina_consume)
	
	# Thiết lập dash
	is_dashing = true
	is_stopping = false
	is_repositioning = false
	dash_timer = 0.0
	can_dash = false
	dash_distance_traveled = 0.0
	previous_position = actor.global_position
	safe_position = actor.global_position
	dash_path_positions.clear()
	dash_path_positions.append(actor.global_position)
	
	# Xác định hướng dash
	if input_direction.length() > 0:
		dash_direction = input_direction.normalized()
	else:
		dash_direction = Vector2.RIGHT if actor.is_facing_right else Vector2.LEFT
	
	# Hiệu ứng hình ảnh
	actor.modulate = Color(0.7, 0.7, 1.0, 0.8)
	
	return true

# Kết thúc dash
func end_dash():
	if is_dashing:
		begin_safe_stop()
	elif is_stopping:
		check_and_reposition()
	elif is_repositioning:
		complete_stop()

# Reset dash khi chạm đất
func reset_dash():
	can_dash = true
	cooldown_timer = 0.0

# Helper functions
func get_dash_progress() -> float:
	if is_dashing:
		return dash_timer / dash_duration
	return 0.0

func is_in_dash_state() -> bool:
	return is_dashing or is_stopping or is_repositioning
