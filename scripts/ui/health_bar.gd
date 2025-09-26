# health_bar.gd
extends TextureProgressBar

var health_component: HealthComponent

func _ready() -> void:
	# Tìm player thông qua group
	var players = get_tree().get_nodes_in_group("Player")
	if players.size() > 0:
		var player = players[0]
		health_component = player.find_child("HealthComponent")
		if health_component:
			health_component.value_changed.connect(_on_health_changed)
			max_value = health_component.max_value
			value = health_component.current_value

func _on_health_changed(_old_value: float, new_value: float) -> void:
	max_value = health_component.max_value
	value = new_value
