extends Node
class_name PossessionManager

signal possessed(body: Node2D)
signal unpossessed(body: Node2D)

var current_body: Node2D = null
var soul: Soul = null

func _ready() -> void:
	soul = get_tree().get_first_node_in_group("Player")

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
	soul.visible = false
	soul.global_position = target.global_position
	soul.set_physics_process(false)
	if target.has_node("StateMachine"):
		var sm = target.get_node("StateMachine")
		if "PossessedState" in sm.states:
			sm.set_current_state(sm.states["PossessedState"])
	possessed.emit(target)
	get_tree().get_first_node_in_group("camera").set_target(target)

func unpossess(force: bool = false) -> void:
	if not current_body: return
	if force and current_body.has_node("HealthComponent"):
		var health = current_body.get_node("HealthComponent")
		if health.current_value <= 0:
			current_body.queue_free()
	soul.visible = true
	soul.global_position = current_body.global_position
	soul.set_physics_process(true)
	if current_body.has_node("StateMachine"):
		current_body.get_node("StateMachine").on_transition_requested("PatrolState")
	get_tree().get_first_node_in_group("camera").set_target(soul)
	unpossessed.emit(current_body)
	current_body = null

func _input(event: InputEvent) -> void:
	if Input.is_action_just_pressed("possess"):
		if current_body:
			unpossess()
		elif soul.interact_area.get_overlapping_bodies().size() > 0:
			var target = soul.interact_area.get_overlapping_bodies()[0]
			if target.is_in_group("Enemies"):
				possess(target)
