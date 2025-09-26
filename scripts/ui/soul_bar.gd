extends TextureProgressBar

@export var soul_component: SoulComponent

func _ready() -> void:
	# Tìm player thông qua group
	var players = get_tree().get_nodes_in_group("Player")
	if players.size() > 0:
		var player = players[0]
		soul_component = player.find_child("SoulComponent")
		if soul_component:
			soul_component.value_changed.connect(_on_soul_changed)
			max_value = soul_component.max_value
			value = soul_component.current_value

func _on_soul_changed(_old_value: float, new_value: float) -> void:
	max_value = soul_component.max_value
	value = new_value
