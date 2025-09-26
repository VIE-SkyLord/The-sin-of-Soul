# dash_component.gd
extends Node2D
class_name DashComponent

# Các tham số dash
@export var max_dash_distance: float = 320
@export var dash_duration: float = 0.5
@export var dash_cooldown: float = 1.0
@export var stamina_consume: float = 20

# References
var actor: Soul
var stamina_component: StaminaComponent

# Trạng thái nội bộ
var is_dashing: bool = false
var dash_direction: Vector2
var dash_timer: float = 0.0
var cooldown_timer: float = 0.0
var can_dash: bool = true

# Biến lưu màu gốc
var original_modulate: Color

func _ready():
	# Lấy reference đến actor
	actor = get_parent()
	
	# Kiểm tra actor có tồn tại không
	if actor:
		original_modulate = actor.modulate
	else:
		push_error("DashComponent: Không thể lấy reference đến actor!")
	
	# Tìm StaminaComponent
	stamina_component = actor.get_node("StaminaComponent") if actor and actor.has_node("StaminaComponent") else null

func _physics_process(delta):
	update_cooldown(delta)
	update_dash_movement(delta)

# Cập nhật thời gian hồi chiêu dash
func update_cooldown(delta: float):
	if not can_dash:
		cooldown_timer += delta
		if cooldown_timer >= dash_cooldown:
			can_dash = true
			cooldown_timer = 0.0

# Xử lý chuyển động khi đang dash
func update_dash_movement(delta: float):
	if is_dashing:
		dash_timer += delta
		
		# Tính vận tốc dash: 320 pixel / 2 giây = 160 pixel/giây
		var dash_speed = max_dash_distance / dash_duration
		actor.velocity = dash_direction * dash_speed
		
		if dash_timer >= dash_duration:
			end_dash()

# Bắt đầu dash
func start_dash(input_direction: Vector2) -> bool:
	# Thêm điều kiện kiểm tra mặt đất
	if not can_dash or not actor.is_on_floor():
		return false
	
	if stamina_component and stamina_component.current_value < stamina_consume:
		return false
	
	# Kiểm tra actor có tồn tại không
	if not actor:
		return false
	
	# Tiêu hao stamina
	if stamina_component:
		stamina_component.change(-stamina_consume)
	
	# Thiết lập dash
	is_dashing = true
	dash_timer = 0.0
	can_dash = false
	
	# Xác định hướng dash
	if input_direction.length() > 0:
		dash_direction = input_direction.normalized()
	else:
		dash_direction = Vector2.RIGHT if actor.is_facing_right else Vector2.LEFT
	
	# Hiệu ứng hình ảnh khi dash (chỉ áp dụng nếu actor có modulate)
	actor.modulate = Color(0.7, 0.7, 1.0, 0.8)  # Màu xanh nhạt, trong suốt
	
	return true

# Kết thúc dash
func end_dash():
	is_dashing = false
	dash_timer = 0.0
	
	# Khôi phục màu sắc ban đầu (chỉ nếu actor tồn tại)
	if actor:
		actor.modulate = original_modulate

# Reset dash khi chạm đất
func reset_dash():
	can_dash = true
	cooldown_timer = 0.0
