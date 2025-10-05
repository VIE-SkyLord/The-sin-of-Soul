extends TextureProgressBar

var health_component: HealthComponent
var possession_manager: Node

func _ready() -> void:
	# Tìm PossessionManager để lắng nghe sự kiện nhập/thoát xác
	possession_manager = get_tree().get_first_node_in_group("PossessionManager")
	if possession_manager:
		possession_manager.possessed.connect(_on_possessed)
		possession_manager.unpossessed.connect(_on_unpossessed)
		print("HealthBar: Connected to PossessionManager")
	else:
		print("HealthBar: WARNING - PossessionManager not found!")
	
	# Kết nối với Soul ban đầu
	await get_tree().process_frame
	_connect_to_player()

func _connect_to_player() -> void:
	"""Kết nối với HealthComponent của Player hiện tại"""
	# Ngắt kết nối cũ nếu có
	if health_component:
		if health_component.value_changed.is_connected(_on_health_changed):
			health_component.value_changed.disconnect(_on_health_changed)
			print("HealthBar: Disconnected from previous health component")
	
	# Tìm player hiện tại (Soul hoặc xác đang nhập)
	# CHỈ LẤY CharacterBody2D, bỏ qua các node khác
	var players = get_tree().get_nodes_in_group("Player")
	var valid_player: Node2D = null
	
	for player in players:
		if player is CharacterBody2D:
			valid_player = player
			break
	
	print("HealthBar: Found ", players.size(), " nodes in Player group, valid player: ", valid_player.name if valid_player else "null")
	
	if valid_player:
		print("HealthBar: Trying to connect to ", valid_player.name)
		
		# Tìm HealthComponent
		if valid_player.has_node("HealthComponent"):
			health_component = valid_player.get_node("HealthComponent")
		else:
			health_component = valid_player.find_child("HealthComponent")
		
		if health_component:
			# Kết nối signal
			if not health_component.value_changed.is_connected(_on_health_changed):
				health_component.value_changed.connect(_on_health_changed)
			
			# Cập nhật UI ngay lập tức
			max_value = health_component.max_value
			value = health_component.current_value
			print("HealthBar: Connected to ", valid_player.name, " - HP: ", value, "/", max_value)
		else:
			print("HealthBar: ERROR - No HealthComponent found on ", valid_player.name)
	else:
		print("HealthBar: ERROR - No valid CharacterBody2D found in Player group")

func _on_possessed(body: Node2D) -> void:
	"""Gọi khi nhập xác - chuyển sang hiển thị máu của xác"""
	print("HealthBar: _on_possessed called for ", body.name)
	# Đợi để group được cập nhật
	await get_tree().process_frame
	_connect_to_player()

func _on_unpossessed(body: Node2D) -> void:
	"""Gọi khi thoát xác - trở về hiển thị máu của Soul"""
	print("HealthBar: _on_unpossessed called for ", body.name)
	# Đợi để group được cập nhật
	await get_tree().process_frame
	_connect_to_player()

func _on_health_changed(_old_value: float, new_value: float) -> void:
	if health_component:
		max_value = health_component.max_value
		value = new_value
		print("HealthBar: Health changed to ", value, "/", max_value)
