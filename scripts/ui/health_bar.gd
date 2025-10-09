extends TextureProgressBar

var health_component: HealthComponent
var possession_manager: Node

# Low health warning settings
@export var low_health_threshold: float = 0.2  # 20%
@export var vignette_pulse_speed: float = 2.0
@export var vignette_pulse_min_intensity: float = 0.3  # Độ mờ nhất khi pulse
@export var vignette_pulse_max_intensity: float = 0.7  # Độ đậm nhất khi pulse
@export var vignette_softness: float = 0.8  # Độ mềm của gradient (càng cao càng mềm)

var is_low_health: bool = false
var pulse_timer: float = 0.0

# Reference to the vignette overlay với shader
var vignette_overlay: ColorRect

func _ready() -> void:
	# Add to group so PossessionManager can find it
	add_to_group("HealthBar")
	
	# Find the vignette overlay in the GUI
	await get_tree().process_frame
	_find_vignette_overlay()
	
	# Find PossessionManager to listen for possession events
	possession_manager = get_tree().get_first_node_in_group("PossessionManager")
	if possession_manager:
		possession_manager.possessed.connect(_on_possessed)
		possession_manager.unpossessed.connect(_on_unpossessed)
		print("HealthBar: Connected to PossessionManager")
	else:
		print("HealthBar: WARNING - PossessionManager not found!")
	
	# Connect to initial Soul
	await get_tree().process_frame
	_connect_to_player()

func _find_vignette_overlay() -> void:
	"""Tìm ColorRect có shader vignette trong GUI hierarchy"""
	var canvas_layer = get_tree().get_first_node_in_group("PlayerGUICanvas")
	if canvas_layer:
		vignette_overlay = canvas_layer.get_node_or_null("VignetteOverlay")
		if vignette_overlay:
			# Đảm bảo nó bắt đầu với intensity = 0 (vô hình)
			var shader_material = vignette_overlay.material as ShaderMaterial
			if shader_material:
				shader_material.set_shader_parameter("intensity", 0.0)
				shader_material.set_shader_parameter("softness", vignette_softness)
				print("HealthBar: Found and initialized VignetteOverlay with shader")
			else:
				print("HealthBar: WARNING - VignetteOverlay found but has no ShaderMaterial")
		else:
			print("HealthBar: WARNING - VignetteOverlay not found in CanvasLayer")
	else:
		print("HealthBar: WARNING - PlayerGUICanvas group not found")

func _process(delta: float) -> void:
	if is_low_health and vignette_overlay:
		var shader_material = vignette_overlay.material as ShaderMaterial
		if shader_material:
			# Tạo hiệu ứng pulse cho vignette
			pulse_timer += delta * vignette_pulse_speed
			# Sử dụng sine wave để tạo pulse mượt mà
			var pulse = (sin(pulse_timer) + 1.0) / 2.0  # Chuyển từ -1..1 thành 0..1
			var intensity = lerp(vignette_pulse_min_intensity, vignette_pulse_max_intensity, pulse)
			shader_material.set_shader_parameter("intensity", intensity)

func _connect_to_player() -> void:
	"""Connect to current Player's HealthComponent"""
	# Disconnect from previous health component if exists
	if health_component:
		if health_component.value_changed.is_connected(_on_health_changed):
			health_component.value_changed.disconnect(_on_health_changed)
			print("HealthBar: Disconnected from previous health component")
	
	# Find current player (Soul or possessed body)
	# ONLY GET CharacterBody2D, ignore other nodes
	var players = get_tree().get_nodes_in_group("Player")
	var valid_player: Node2D = null
	
	for player in players:
		if player is CharacterBody2D:
			valid_player = player
			break
	
	print("HealthBar: Found ", players.size(), " nodes in Player group, valid player: ", valid_player.name if valid_player else "null")
	
	if valid_player:
		print("HealthBar: Trying to connect to ", valid_player.name)
		
		# Find HealthComponent
		if valid_player.has_node("HealthComponent"):
			health_component = valid_player.get_node("HealthComponent")
		else:
			health_component = valid_player.find_child("HealthComponent")
		
		if health_component:
			# Connect signal
			if not health_component.value_changed.is_connected(_on_health_changed):
				health_component.value_changed.connect(_on_health_changed)
			
			# Update UI immediately
			_update_display()
			print("HealthBar: Connected to ", valid_player.name, " - HP: ", value, "/", max_value)
		else:
			print("HealthBar: ERROR - No HealthComponent found on ", valid_player.name)
	else:
		print("HealthBar: ERROR - No valid CharacterBody2D found in Player group")

func _update_display() -> void:
	"""Update health bar display"""
	if health_component:
		max_value = health_component.max_value
		value = health_component.current_value
		
		# Check low health status
		var health_percent = value / max_value
		var was_low_health = is_low_health
		is_low_health = health_percent <= low_health_threshold
		
		# Change health bar color when health is low
		if is_low_health:
			modulate = Color(1.5, 0.3, 0.3)  # Bright red
			if not was_low_health:
				pulse_timer = 0.0  # Reset timer khi vừa vào trạng thái low health
				# Hiển thị vignette overlay
				if vignette_overlay:
					vignette_overlay.visible = true
		else:
			modulate = Color(1, 1, 1)  # Normal color
			if was_low_health:
				# Ẩn vignette overlay khi hồi máu
				if vignette_overlay:
					var shader_material = vignette_overlay.material as ShaderMaterial
					if shader_material:
						shader_material.set_shader_parameter("intensity", 0.0)
					vignette_overlay.visible = false
		
		# Force visual update
		queue_redraw()
		#print("HealthBar: Display updated - value=", value, " max=", max_value, " percent=", health_percent*100, "%")

func _on_possessed(body: Node2D) -> void:
	"""Called when possessing a body - switch to showing body's health"""
	print("HealthBar: _on_possessed called for ", body.name)
	# Wait for group to be updated
	await get_tree().process_frame
	_connect_to_player()

func _on_unpossessed(body: Node2D) -> void:
	"""Called when leaving a body - return to showing Soul's health"""
	print("HealthBar: _on_unpossessed called for ", body.name)
	# Wait multiple frames to ensure:
	# 1. Group is updated
	# 2. Soul health is set to 1%
	# 3. Signal value_changed is emitted
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame
	_connect_to_player()
	# Force update once more to be sure
	await get_tree().process_frame
	_update_display()

func _on_health_changed(_old_value: float, new_value: float) -> void:
	if health_component:
		_update_display()
		#print("HealthBar: Health changed to ", value, "/", max_value)

func force_update() -> void:
	"""Force reconnect and update display - called from PossessionManager"""
	print("HealthBar: force_update() called")
	_connect_to_player()
	await get_tree().process_frame
	_update_display()

func disconnect_health() -> void:
	"""Temporarily disconnect from current HealthComponent"""
	if health_component:
		if health_component.value_changed.is_connected(_on_health_changed):
			health_component.value_changed.disconnect(_on_health_changed)
			print("HealthBar: Disconnected from health component (temporary)")
