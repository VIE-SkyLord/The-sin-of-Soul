extends Camera2D

@export var target: Node2D
@export var follow_speed: float = 5.0
@export var target_offset: Vector2 = Vector2.ZERO
@export var enable_predictive_follow: bool = true
@export var prediction_strength: float = 2.0
@export var prediction_distance: float = 100.0
@export var velocity_smoothing: float = 8.0
@export var min_velocity_threshold: float = 10.0
@export var shake_decay: float = 2.0
@export var shake_intensity: float = 0.0
@export var max_shake_intensity: float = 10.0
@export var vignette_color: Color = Color(1, 0, 0, 0)
@export var vignette_intensity: float = 0.0
@export var vignette_decay: float = 1.0
@export var max_vignette_intensity: float = 0.8

var _rng = RandomNumberGenerator.new()
var _shake_offset: Vector2 = Vector2.ZERO
var _base_offset: Vector2 = Vector2.ZERO
var _last_target_position: Vector2 = Vector2.ZERO
var _target_velocity: Vector2 = Vector2.ZERO
var _smoothed_velocity: Vector2 = Vector2.ZERO
var _predictive_offset: Vector2 = Vector2.ZERO
var _is_first_frame: bool = true

func _ready() -> void:
	_rng.randomize()
	_base_offset = offset
	make_current()
	add_to_group("camera")
	target = get_tree().get_first_node_in_group("Player")
	
	if target:
		_last_target_position = target.global_position
	
	print("Enhanced Camera initialized with predictive follow")

func _process(delta: float) -> void:
	if target:
		_update_predictive_follow(delta)
		var target_position = target.global_position + _base_offset + target_offset + _predictive_offset
		global_position = global_position.lerp(target_position, follow_speed * delta)
	
	_process_shake(delta)
	_process_vignette(delta)

func _update_predictive_follow(delta: float) -> void:
	if not enable_predictive_follow:
		_predictive_offset = Vector2.ZERO
		return
	
	var current_target_position = target.global_position
	
	if not _is_first_frame:
		_target_velocity = (current_target_position - _last_target_position) / delta
	else:
		_target_velocity = Vector2.ZERO
		_is_first_frame = false
	
	_smoothed_velocity = _smoothed_velocity.lerp(_target_velocity, velocity_smoothing * delta)
	
	if _smoothed_velocity.length() > min_velocity_threshold:
		var normalized_velocity = _smoothed_velocity.normalized()
		var velocity_magnitude = min(_smoothed_velocity.length(), prediction_distance)
		var desired_predictive_offset = normalized_velocity * velocity_magnitude * prediction_strength
		_predictive_offset = _predictive_offset.lerp(desired_predictive_offset, follow_speed * delta)
	else:
		_predictive_offset = _predictive_offset.lerp(Vector2.ZERO, follow_speed * delta)
	
	_last_target_position = current_target_position

func _process_shake(delta: float) -> void:
	if shake_intensity > 0:
		_shake_offset = Vector2(
			_rng.randf_range(-shake_intensity, shake_intensity),
			_rng.randf_range(-shake_intensity, shake_intensity)
		)
		shake_intensity = max(0, shake_intensity - shake_decay * delta)
		offset = _base_offset + _shake_offset
	else:
		_shake_offset = Vector2.ZERO
		offset = _base_offset

func _process_vignette(delta: float) -> void:
	if vignette_intensity > 0:
		vignette_intensity = max(0, vignette_intensity - vignette_decay * delta)
		_apply_vignette_effect()

func _apply_vignette_effect() -> void:
	modulate = Color(1, 1 - vignette_intensity, 1 - vignette_intensity, 1)

func apply_shake(intensity: float) -> void:
	shake_intensity = min(intensity, max_shake_intensity)

func apply_vignette(intensity: float) -> void:
	vignette_intensity = min(intensity, max_vignette_intensity)

func set_target(new_target: Node2D) -> void:
	target = new_target
	if new_target:
		_last_target_position = new_target.global_position
		_is_first_frame = true
	print("Camera target set to: ", new_target.name if new_target else "null")

func set_target_offset(new_offset: Vector2) -> void:
	target_offset = new_offset

func set_vignette_color(color: Color) -> void:
	vignette_color = color

func set_predictive_follow(enabled: bool) -> void:
	enable_predictive_follow = enabled
	if not enabled:
		_predictive_offset = Vector2.ZERO

func set_prediction_strength(strength: float) -> void:
	prediction_strength = max(0, strength)

func set_prediction_distance(distance: float) -> void:
	prediction_distance = max(0, distance)

func get_predictive_offset() -> Vector2:
	return _predictive_offset

func get_target_velocity() -> Vector2:
	return _smoothed_velocity
