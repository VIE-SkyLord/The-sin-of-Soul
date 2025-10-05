extends Node
class_name PossessionManager

signal possessed(body: Node2D)
signal unpossessed(body: Node2D)

var current_body: Node2D = null
var soul: Soul = null
var soul_original_layer: int = 0
var soul_original_mask: int = 0
var soul_hurtbox_original_layer: int = 0
var soul_hurtbox_original_mask: int = 0
var body_original_layer: int = 0
var body_original_mask: int = 0
var body_hurtbox_original_layer: int = 0
var body_hurtbox_original_mask: int = 0

func _ready() -> void:
	add_to_group("PossessionManager")
	soul = get_tree().get_first_node_in_group("Player")
	soul_original_layer = soul.collision_layer
	soul_original_mask = soul.collision_mask
	if soul.has_node("HurtboxComponent"):
		var soul_hurtbox = soul.get_node("HurtboxComponent")
		soul_hurtbox_original_layer = soul_hurtbox.collision_layer
		soul_hurtbox_original_mask = soul_hurtbox.collision_mask

func _process(delta: float) -> void:
	if current_body:
		soul.global_position = current_body.global_position
	if Input.is_action_just_pressed("use_potion"):
		if soul.potion_component and soul.potion_component.can_use_potion():
			soul.potion_component.use_potion()

func can_possess(target: Node2D) -> bool:
	if not target.has_node("PossessableComponent"): return false
	var possessable = target.get_node("PossessableComponent")
	if not possessable.is_possessable: return false
	if not target.has_node("HealthComponent"): return false
	var health = target.get_node("HealthComponent")
	return (health.current_value / float(health.max_value)) <= possessable.health_threshold

func possess(target: Node2D) -> void:
	if current_body or not can_possess(target): return
	current_body = target
	body_original_layer = target.collision_layer
	body_original_mask = target.collision_mask
	if target.has_node("HurtboxComponent"):
		var hurtbox = target.get_node("HurtboxComponent")
		body_hurtbox_original_layer = hurtbox.collision_layer
		body_hurtbox_original_mask = hurtbox.collision_mask
		hurtbox.set_deferred("monitoring", false)
	target.collision_layer = soul_original_layer
	target.collision_mask = soul_original_mask
	if target.has_node("LightAttackArea"):
		target.get_node("LightAttackArea").collision_mask = 2
	if target.has_node("HeavyAttackArea"):
		target.get_node("HeavyAttackArea").collision_mask = 2
	if soul.has_node("HurtboxComponent"):
		soul.get_node("HurtboxComponent").set_deferred("monitoring", false)
	
	# XÓA Soul và tất cả node con khỏi group Player
	soul.remove_from_group("Player")
	for child in soul.get_children():
		if child.is_in_group("Player"):
			child.remove_from_group("Player")
	
	# Xóa xác khỏi Enemies trước khi thêm vào Player
	if target.is_in_group("Enemies"):
		target.remove_from_group("Enemies")
	for child in target.get_children():
		if child.is_in_group("Enemies"):
			child.remove_from_group("Enemies")
	
	# Thêm xác vào group Player
	target.add_to_group("Player")
	
	soul.collision_layer = 0
	soul.collision_mask = 0
	soul.visible = false
	soul.global_position = target.global_position
	soul.set_physics_process(false)
	
	if target.has_node("StateMachine"):
		var sm = target.get_node("StateMachine")
		if "PossessedState" in sm.states:
			sm.set_current_state(sm.states["PossessedState"])
	
	# ĐỒng bộ % máu từ Soul sang xác
	sync_health_to_body(target)
	
	# Chuyển PotionComponent sang sử dụng HealthComponent của xác
	if soul.potion_component and target.has_node("HealthComponent"):
		soul.potion_component.health_component = target.get_node("HealthComponent")
		print("PotionComponent now healing body: ", target.name)
	
	soul.on_possess(target)
	
	# Set camera trước khi emit signal
	var camera = get_tree().get_first_node_in_group("camera")
	if camera:
		camera.set_target(target)
	
	# Emit signal SAU KHI tất cả đã được setup
	print("PossessionManager: Emitting possessed signal for ", target.name)
	possessed.emit(target)

func sync_health_to_body(target: Node2D) -> void:
	"""Đồng bộ % máu từ Soul sang xác khi nhập"""
	if not soul.has_node("HealthComponent"): return
	if not target.has_node("HealthComponent"): return
	
	var soul_health = soul.get_node("HealthComponent")
	var body_health = target.get_node("HealthComponent")
	
	# Tính % máu hiện tại của Soul
	var health_percent = soul_health.current_value / float(soul_health.max_value)
	
	# Áp dụng % đó cho xác (dựa trên max_value của xác)
	var new_body_health = int(body_health.max_value * health_percent)
	body_health.current_value = max(1, new_body_health)  # Đảm bảo ít nhất 1 HP
	
	print("Possession: Soul ", soul_health.current_value, "/", soul_health.max_value, 
		  " (", health_percent * 100, "%) -> Body ", body_health.current_value, "/", body_health.max_value)

func unpossess(force: bool = false) -> void:
	if not current_body: return
	
	# Đồng bộ % máu từ xác về Soul trước khi thoát
	sync_health_to_soul()
	
	if force and current_body.has_node("HealthComponent"):
		var health = current_body.get_node("HealthComponent")
		if health.current_value <= 0:
			current_body.queue_free()
	soul.collision_layer = soul_original_layer
	soul.collision_mask = soul_original_mask
	if soul.has_node("HurtboxComponent"):
		var soul_hurtbox = soul.get_node("HurtboxComponent")
		soul_hurtbox.set_deferred("monitoring", true)
		soul_hurtbox.collision_layer = soul_hurtbox_original_layer
		soul_hurtbox.collision_mask = soul_hurtbox_original_mask
	current_body.collision_layer = body_original_layer
	current_body.collision_mask = body_original_mask
	if current_body.has_node("HurtboxComponent"):
		var hurtbox = current_body.get_node("HurtboxComponent")
		hurtbox.set_deferred("monitoring", true)
		hurtbox.collision_layer = body_hurtbox_original_layer
		hurtbox.collision_mask = body_hurtbox_original_mask
	if current_body.has_node("LightAttackArea"):
		current_body.get_node("LightAttackArea").collision_mask = 1
	if current_body.has_node("HeavyAttackArea"):
		current_body.get_node("HeavyAttackArea").collision_mask = 1
	# Chuyển xác về Enemy và Soul về Player
	if current_body.is_in_group("Player"):
		current_body.remove_from_group("Player")
	current_body.add_to_group("Enemies")
	
	# Thêm Soul vào group Player
	if not soul.is_in_group("Player"):
		soul.add_to_group("Player")
	
	soul.visible = true
	soul.global_position = current_body.global_position
	soul.set_physics_process(true)
	if current_body.has_node("StateMachine"):
		current_body.get_node("StateMachine").on_transition_requested("PatrolState")
	soul.on_unpossess()
	
	# Trả camera về Soul
	var camera = get_tree().get_first_node_in_group("camera")
	if camera:
		camera.set_target(soul)
	
	# Emit signal SAU KHI tất cả đã được setup
	print("PossessionManager: Emitting unpossessed signal for ", current_body.name)
	unpossessed.emit(current_body)
	current_body = null

func sync_health_to_soul() -> void:
	"""Đồng bộ % máu từ xác về Soul khi thoát nhập"""
	if not current_body: return
	if not soul.has_node("HealthComponent"): return
	if not current_body.has_node("HealthComponent"): return
	
	var body_health = current_body.get_node("HealthComponent")
	var soul_health = soul.get_node("HealthComponent")
	
	# Tính % máu hiện tại của xác
	var health_percent = body_health.current_value / float(body_health.max_value)
	
	# Áp dụng % đó cho Soul (dựa trên max_value của Soul)
	var new_soul_health = int(soul_health.max_value * health_percent)
	soul_health.current_value = max(1, new_soul_health)  # Đảm bảo ít nhất 1 HP
	
	# Khôi phục PotionComponent về dùng HealthComponent của Soul
	if soul.potion_component:
		soul.potion_component.health_component = soul_health
		print("PotionComponent restored to soul")
	
	print("Unpossession: Body ", body_health.current_value, "/", body_health.max_value,
		  " (", health_percent * 100, "%) -> Soul ", soul_health.current_value, "/", soul_health.max_value)

func _input(event: InputEvent) -> void:
	if Input.is_action_just_pressed("possess"):
		if current_body:
			unpossess()
		elif soul.interact_area.get_overlapping_bodies().size() > 0:
			var target = soul.interact_area.get_overlapping_bodies()[0]
			if target.is_in_group("Enemies"):
				possess(target)
