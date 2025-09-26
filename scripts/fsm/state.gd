# State.gd
extends Node
class_name State

signal transition_requested(new_state_name: StringName)

var state_machine: StateMachine
var actor: Node

func enter() -> void:
	pass

func update(_delta: float) -> void:
	pass

func physics_update(_delta: float) -> void:
	pass

func exit() -> void:
	pass

func handle_input(_event: InputEvent) -> void:
	pass

func transition_to(target_state_name: StringName) -> void:
	transition_requested.emit(target_state_name)
