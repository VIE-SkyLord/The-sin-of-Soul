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
	# Store and disable body's hurtbox
	if target.has_node("HurtboxComponent"):
		var hurtbox = target.get_node("HurtboxComponent")
		body_hurtbox_original_layer = hurtbox.collision_layer
		body_hurtbox_original_mask = hurtbox.collision_mask
		hurtbox.set_deferred("monitoring", false)  # Disable body's hurtbox
	target.collision_layer = soul_original_layer  # Set to layer 1 (player)
	target.collision_mask = soul_original_mask   # Set to mask 2 (enemies)
	# Set hitbox areas mask to 2 (enemy)
	if target.has_node("LightAttackArea"):
		target.get_node("LightAttackArea").collision_mask = 2
	if target.has_node("HeavyAttackArea"):
		target.get_node("HeavyAttackArea").collision_mask = 2
	# Disable soul's hurtbox
	if soul.has_node("HurtboxComponent"):
		soul.get_node("HurtboxComponent").set_deferred("monitoring", false)
	# Change group: remove from Enemies, add to Player
	if target.is_in_group("Enemies"):
		target.remove_from_group("Enemies")
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
	possessed.emit(target)
	get_tree().get_first_node_in_group("camera").set_target(target)

func unpossess(force: bool = false) -> void:
	if not current_body: return
	if force and current_body.has_node("HealthComponent"):
		var health = current_body.get_node("HealthComponent")
		if health.current_value <= 0:
			current_body.queue_free()
	soul.collision_layer = soul_original_layer  # Restore to layer 1
	soul.collision_mask = soul_original_mask   # Restore to mask 2
	# Restore soul's hurtbox
	if soul.has_node("HurtboxComponent"):
		var soul_hurtbox = soul.get_node("HurtboxComponent")
		soul_hurtbox.set_deferred("monitoring", true)
		soul_hurtbox.collision_layer = soul_hurtbox_original_layer
		soul_hurtbox.collision_mask = soul_hurtbox_original_mask
	current_body.collision_layer = body_original_layer  # Restore to layer 2
	current_body.collision_mask = body_original_mask   # Restore to mask 1
	# Restore body's hurtbox
	if current_body.has_node("HurtboxComponent"):
		var hurtbox = current_body.get_node("HurtboxComponent")
		hurtbox.set_deferred("monitoring", true)  # Re-enable body's hurtbox
		hurtbox.collision_layer = body_hurtbox_original_layer
		hurtbox.collision_mask = body_hurtbox_original_mask
	# Restore hitbox masks (to 1 for player)
	if current_body.has_node("LightAttackArea"):
		current_body.get_node("LightAttackArea").collision_mask = 1
	if current_body.has_node("HeavyAttackArea"):
		current_body.get_node("HeavyAttackArea").collision_mask = 1
	# Restore group: remove from Player, add to Enemies
	if current_body.is_in_group("Player"):
		current_body.remove_from_group("Player")
	current_body.add_to_group("Enemies")
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
