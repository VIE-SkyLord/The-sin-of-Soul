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
var body_original_rate_recovery: float = 0

# Thông số đẩy lùi khi bị sát thương chí tử
@export var knockback_force: float = 300.0
@export var knockback_duration: float = 0.3
@export var soul_survival_health_percent: float = 0.05  # 5% máu
@export var invulnerability_duration: float = 1.0  # Thời gian bất tử sau khi bị đẩy ra

func _ready() -> void:
	add_to_group("PossessionManager")
	soul = get_tree().get_first_node_in_group("Player")
	soul_original_layer = soul.collision_layer
	soul_original_mask = soul.collision_mask
	if soul.has_node("HurtboxComponent"):
		var soul_hurtbox = soul.get_node("HurtboxComponent")
		soul_hurtbox_original_layer = soul_hurtbox.collision_layer
		soul_hurtbox_original_mask = soul_hurtbox.collision_mask
		print("Soul Hurtbox - Layer: ", soul_hurtbox_original_layer, " Mask: ", soul_hurtbox_original_mask)

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
	
	# Kết nối signal died của xác để bắt sự kiện chết
	if target.has_node("HealthComponent"):
		var body_health = target.get_node("HealthComponent")
		if not body_health.died.is_connected(_on_possessed_body_died):
			body_health.died.connect(_on_possessed_body_died)
	
	# Lưu collision ban đầu của xác
	body_original_layer = target.collision_layer
	body_original_mask = target.collision_mask
	
	# Lưu hurtbox ban đầu của xác
	if target.has_node("HurtboxComponent"):
		var hurtbox = target.get_node("HurtboxComponent")
		body_hurtbox_original_layer = hurtbox.collision_layer
		body_hurtbox_original_mask = hurtbox.collision_mask
		
		# CẤU HÌNH HURTBOX CỦA XÁC KHI NHẬP
		hurtbox.collision_layer = 1  # Player layer
		hurtbox.collision_mask = 2   # Enemies layer
		print("Possessed Body Hurtbox - Layer: 1 (Player), Mask: 2 (Enemies)")
	
	# Set collision cho xác
	target.collision_layer = soul_original_layer
	target.collision_mask = soul_original_mask
	
	# CẤU HÌNH ATTACK AREAS CỦA XÁC
	if target.has_node("LightAttackArea"):
		var light_attack = target.get_node("LightAttackArea")
		light_attack.collision_layer = 1
		light_attack.collision_mask = 2
		print("Light Attack - Layer: 1, Mask: 2")
		
	if target.has_node("HeavyAttackArea"):
		var heavy_attack = target.get_node("HeavyAttackArea")
		heavy_attack.collision_layer = 1
		heavy_attack.collision_mask = 2
		print("Heavy Attack - Layer: 1, Mask: 2")
	
	# Tắt hurtbox của Soul
	if soul.has_node("HurtboxComponent"):
		soul.get_node("HurtboxComponent").set_deferred("monitoring", false)
	
	# Xóa Soul và tất cả node con khỏi group Player
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
	
	# Ẩn Soul
	soul.collision_layer = 0
	soul.collision_mask = 0
	soul.visible = false
	soul.global_position = target.global_position
	soul.set_physics_process(false)
	
	# Chuyển sang PossessedState
	if target.has_node("StateMachine"):
		var sm = target.get_node("StateMachine")
		if "PossessedState" in sm.states:
			sm.set_current_state(sm.states["PossessedState"])
	
	# Đồng bộ % máu từ Soul sang xác
	sync_health_to_body(target)
	
	# Chuyển PotionComponent sang sử dụng HealthComponent của xác
	if soul.potion_component and target.has_node("HealthComponent"):
		soul.potion_component.health_component = target.get_node("HealthComponent")
		print("PotionComponent now healing body: ", target.name)
		
	# Đồng bộ rate_recovery từ Soul sang xác
	if soul.has_node("HealthComponent") and target.has_node("HealthComponent"):
		var soul_health = soul.get_node("HealthComponent")
		var body_health = target.get_node("HealthComponent")
		body_health.rate_recovery = soul_health.rate_recovery
		print("Synced rate_recovery to body: ", body_health.rate_recovery, " HP/s")
	
	# VÔ HIỆU HÓA DASH NẾU XÁC KHÔNG CÓ DASHCOMPONENT
	disable_dash_if_body_lacks_component(target)
	
	soul.on_possess(target)
	
	# CẬP NHẬT TARGET cho TẤT CẢ kẻ địch
	update_all_enemies_target(target)
	
	# Set camera
	var camera = get_tree().get_first_node_in_group("camera")
	if camera:
		camera.set_target(target)
	
	print("PossessionManager: Emitting possessed signal for ", target.name)
	possessed.emit(target)

func disable_dash_if_body_lacks_component(target: Node2D) -> void:
	"""Vô hiệu hóa dash của Soul nếu xác không có DashComponent"""
	if not target.has_node("DashComponent"):
		if soul.dash_component:
			soul.dash_component.set_physics_process(false)
			soul.dash_component.can_dash = false
			print("Dash disabled - Body lacks DashComponent")
	else:
		print("Dash available - Body has DashComponent")

func enable_dash_for_soul() -> void:
	"""Bật lại dash cho Soul khi unpossess"""
	if soul.dash_component:
		soul.dash_component.set_physics_process(true)
		soul.dash_component.can_dash = true
		soul.dash_component.cooldown_timer = 0.0
		print("Dash enabled - Soul unpossessed")

func _on_possessed_body_died() -> void:
	"""Xử lý khi xác bị chết - đẩy soul ra với knockback"""
	if not current_body:
		return
	
	print("PossessionManager: Possessed body died! Ejecting soul with knockback")
	
	# Tính hướng knockback (ngược với hướng tấn công cuối cùng)
	var knockback_direction = Vector2.ZERO
	var last_attacker_position = Vector2.ZERO
	
	# Lấy vị trí attacker từ HealthComponent
	if current_body.has_node("HealthComponent"):
		var body_health = current_body.get_node("HealthComponent")
		last_attacker_position = body_health.get_last_attacker_position()
	
	# Tính hướng đẩy lùi
	if last_attacker_position != Vector2.ZERO:
		knockback_direction = (current_body.global_position - last_attacker_position).normalized()
		print("Knockback from attacker at: ", last_attacker_position)
	else:
		# Nếu không xác định được, đẩy theo hướng sprite đang quay
		var sprite_direction = 1 if not current_body.sprite_2d.flip_h else -1
		knockback_direction = Vector2(-sprite_direction, -0.5).normalized()
		print("Knockback using sprite direction fallback")
	
	# Tính vị trí knockback trước
	var knockback_distance = knockback_force * knockback_duration
	var target_position = current_body.global_position + (knockback_direction * knockback_distance)
	
	# Set soul vào vị trí knockback NGAY LẬP TỨC (tránh bị hit thêm lần nữa)
	soul.global_position = target_position
	
	# BƯỚC 1: Set soul health về 1% TRƯỚC KHI unpossess (KHÔNG EMIT SIGNAL)
	if soul.has_node("HealthComponent"):
		var soul_health = soul.get_node("HealthComponent")
		var target_health = max(1.0, soul_health.max_value * soul_survival_health_percent)
		
		# QUAN TRỌNG: Chỉ set giá trị, KHÔNG emit signal
		# Vì HealthBar đang lắng nghe body, không phải soul
		soul_health.current_value = target_health
		
		print("Soul health set to: ", soul_health.current_value, "/", soul_health.max_value, " (", (soul_health.current_value/soul_health.max_value)*100, "%) WITHOUT emitting signal")
	
	# BƯỚC 2: KHÔNG cần await frame nữa
	
	# BƯỚC 3: Thực hiện unpossess với force = true
	unpossess(true)
	
	# BƯỚC 4: Tạo invulnerability frame cho soul
	make_soul_invulnerable(invulnerability_duration)

func apply_knockback_to_soul(direction: Vector2) -> void:
	"""Áp dụng hiệu ứng knockback cho soul"""
	if not soul:
		return
	
	# Đảm bảo soul có thể di chuyển
	soul.set_physics_process(true)
	
	# Tạo tween để tạo hiệu ứng knockback mượt mà
	var tween = create_tween()
	var knockback_velocity = direction * knockback_force
	var target_position = soul.global_position + knockback_velocity * knockback_duration
	
	# Di chuyển soul với tween
	tween.tween_property(soul, "global_position", target_position, knockback_duration)
	tween.set_ease(Tween.EASE_OUT)
	tween.set_trans(Tween.TRANS_CUBIC)
	
	print("Knockback applied to soul: direction=", direction, " force=", knockback_force)

func make_soul_invulnerable(duration: float) -> void:
	"""Làm soul bất tử trong khoảng thời gian"""
	if not soul or not soul.has_node("HurtboxComponent"):
		return
	
	var hurtbox = soul.get_node("HurtboxComponent")
	
	# Tắt hurtbox
	hurtbox.set_deferred("monitoring", false)
	print("Soul invulnerable for ", duration, " seconds")
	
	# Tạo hiệu ứng nhấp nháy
	create_invulnerability_flash(duration)
	
	# Bật lại sau duration
	await get_tree().create_timer(duration).timeout
	if soul and soul.has_node("HurtboxComponent"):
		hurtbox.set_deferred("monitoring", true)
		print("Soul vulnerability restored")

func create_invulnerability_flash(duration: float) -> void:
	"""Tạo hiệu ứng nhấp nháy khi bất tử"""
	if not soul:
		return
	
	var flash_count = int(duration / 0.1)  # Nhấy mỗi 0.1 giây
	var tween = create_tween()
	
	for i in flash_count:
		tween.tween_property(soul, "modulate", Color(1, 1, 1, 0.3), 0.05)
		tween.tween_property(soul, "modulate", Color(1, 1, 1, 1.0), 0.05)
	
	# Đảm bảo kết thúc ở alpha = 1
	tween.tween_property(soul, "modulate", Color(1, 1, 1, 1.0), 0.0)

func sync_health_to_body(target: Node2D) -> void:
	"""Đồng bộ % máu từ Soul sang xác khi nhập"""
	if not soul.has_node("HealthComponent"): return
	if not target.has_node("HealthComponent"): return
	
	var soul_health = soul.get_node("HealthComponent")
	var body_health = target.get_node("HealthComponent")
	
	var health_percent = soul_health.current_value / float(soul_health.max_value)
	var new_body_health = int(body_health.max_value * health_percent)
	body_health.current_value = max(1, new_body_health)
	
	print("Possession: Soul ", soul_health.current_value, "/", soul_health.max_value, 
		  " (", health_percent * 100, "%) -> Body ", body_health.current_value, "/", body_health.max_value)

func unpossess(force: bool = false) -> void:
	if not current_body: return
	
	# Lưu rate_recovery ban đầu của xác (thêm biến này vào đầu class)
	if current_body.has_node("HealthComponent"):
		var body_health = current_body.get_node("HealthComponent")
		body_health.rate_recovery = body_original_rate_recovery  # Khôi phục về giá trị gốc
	
	# Ngắt kết nối signal died
	if current_body.has_node("HealthComponent"):
		var body_health = current_body.get_node("HealthComponent")
		if body_health.died.is_connected(_on_possessed_body_died):
			body_health.died.disconnect(_on_possessed_body_died)
	
	# Đồng bộ % máu từ xác về Soul (CHỈ khi không phải force eject từ death)
	if not force:
		sync_health_to_soul()
	else:
		print("PossessionManager: Force unpossess - skipping health sync")
	
	if force and current_body.has_node("HealthComponent"):
		var health = current_body.get_node("HealthComponent")
		if health.current_value <= 0:
			current_body.queue_free()
	
	# Khôi phục collision của Soul
	soul.collision_layer = soul_original_layer
	soul.collision_mask = soul_original_mask
	if soul.has_node("HurtboxComponent"):
		var soul_hurtbox = soul.get_node("HurtboxComponent")
		soul_hurtbox.set_deferred("monitoring", true)
		soul_hurtbox.collision_layer = soul_hurtbox_original_layer
		soul_hurtbox.collision_mask = soul_hurtbox_original_mask
		print("Soul Hurtbox restored - Layer: ", soul_hurtbox_original_layer, " Mask: ", soul_hurtbox_original_mask)
	
	# Khôi phục collision của xác
	current_body.collision_layer = body_original_layer
	current_body.collision_mask = body_original_mask
	
	# Khôi phục hurtbox của xác
	if current_body.has_node("HurtboxComponent"):
		var hurtbox = current_body.get_node("HurtboxComponent")
		hurtbox.set_deferred("monitoring", true)
		hurtbox.collision_layer = body_hurtbox_original_layer
		hurtbox.collision_mask = body_hurtbox_original_mask
		print("Body Hurtbox restored - Layer: ", body_hurtbox_original_layer, " Mask: ", body_hurtbox_original_mask)
	
	# Khôi phục attack areas của xác
	if current_body.has_node("LightAttackArea"):
		var light_attack = current_body.get_node("LightAttackArea")
		light_attack.collision_layer = 2
		light_attack.collision_mask = 1
		print("Body Light Attack restored - Layer: 2, Mask: 1")
		
	if current_body.has_node("HeavyAttackArea"):
		var heavy_attack = current_body.get_node("HeavyAttackArea")
		heavy_attack.collision_layer = 2
		heavy_attack.collision_mask = 1
		print("Body Heavy Attack restored - Layer: 2, Mask: 1")
	
	# Chuyển xác về Enemy và Soul về Player
	if current_body.is_in_group("Player"):
		current_body.remove_from_group("Player")
	current_body.add_to_group("Enemies")
	
	if not soul.is_in_group("Player"):
		soul.add_to_group("Player")
	
	soul.visible = true
	soul.set_physics_process(true)
	
	# BẬT LẠI DASH CHO SOUL
	enable_dash_for_soul()
	
	if current_body.has_node("StateMachine"):
		current_body.get_node("StateMachine").on_transition_requested("PatrolState")
	
	soul.on_unpossess()
	
	# CẬP NHẬT TARGET cho TẤT CẢ kẻ địch
	update_all_enemies_target(soul)
	
	# Trả camera về Soul
	var camera = get_tree().get_first_node_in_group("camera")
	if camera:
		camera.set_target(soul)
	
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
	
	var health_percent = body_health.current_value / float(body_health.max_value)
	var new_soul_health = int(soul_health.max_value * health_percent)
	soul_health.current_value = max(1, new_soul_health)
	
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

func update_all_enemies_target(new_target: Node2D) -> void:
	"""Cập nhật target cho tất cả kẻ địch trong scene"""
	var enemies = get_tree().get_nodes_in_group("Enemies")
	for enemy in enemies:
		if enemy.has_method("set_target"):
			enemy.set_target(new_target)
			print("Updated enemy ", enemy.name, " target to ", new_target.name)
