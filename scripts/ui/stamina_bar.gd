extends TextureProgressBar

@export var stamina_component: StaminaComponent

func _ready() -> void:
	# Tìm player thông qua group
	var players = get_tree().get_nodes_in_group("Player")
	if players.size() > 0:
		var player = players[0]
		stamina_component = player.find_child("StaminaComponent")
		if stamina_component:
			stamina_component.value_changed.connect(_on_stamina_changed)
			max_value = stamina_component.max_value
			value = stamina_component.current_value

func _on_stamina_changed(_old_value: float, new_value: float) -> void:
	max_value = stamina_component.max_value
	value = new_value
