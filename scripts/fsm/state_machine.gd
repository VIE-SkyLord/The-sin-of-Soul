# StateMachine.gd
extends Node
class_name StateMachine

signal state_changed(new_state_name: String)

var current_state: State
var states: Dictionary = {}
var is_transitioning: bool = false  # Recursion guard
@export var initial_state_path: NodePath
var actor: Node

func _ready() -> void:
	actor = get_parent()
	
	for child in get_children():
		if child is State:
			var state: State = child
			states[state.name] = state
			state.state_machine = self
			state.actor = actor
			state.transition_requested.connect(on_transition_requested)
	
	if initial_state_path:
		var initial_state_node = get_node(initial_state_path)
		if initial_state_node is State:
			set_current_state(initial_state_node)
		else:
			push_error("Initial state node is not a valid State: " + str(initial_state_path))
	else:
		push_error("No initial state path set!")

func _process(delta: float) -> void:
	if current_state:
		current_state.update(delta)

func _physics_process(delta: float) -> void:
	if current_state:
		current_state.physics_update(delta)

func _unhandled_input(event: InputEvent) -> void:
	if current_state:
		current_state.handle_input(event)

func set_current_state(new_state: State) -> void:
	if is_transitioning:
		print("Blocked recursive transition to: ", new_state.name if new_state else "null")
		return
	is_transitioning = true
	
	if current_state:
		print("Exiting state: ", current_state.name)
		current_state.exit()
	
	current_state = new_state
	
	if current_state:
		state_changed.emit(new_state.name)
		print("Entering state: ", new_state.name)
		current_state.enter()
	
	is_transitioning = false

func on_transition_requested(new_state_name: StringName) -> void:
	print("Transition requested from ", current_state.name if current_state else "none", " to ", new_state_name)
	var new_state = states.get(new_state_name)
	if new_state:
		set_current_state(new_state)
	else:
		push_error("State not found: " + str(new_state_name))
