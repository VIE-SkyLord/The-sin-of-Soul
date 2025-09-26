# DeathState.gd
extends State
class_name DeathState

func enter() -> void:
	actor.stop_moving()
	actor.set_process(false)
	actor.set_physics_process(false)

func exit() -> void:
	pass
