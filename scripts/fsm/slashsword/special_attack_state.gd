# SpecialAttackState.gd
extends State
class_name SpecialAttackState

var attack_timeout: float = 0.0
var max_attack_time: float = 3.0
var transition_cooldown: float = 0.1
var last_transition: float = 0.0

func enter() -> void:
	actor.stop_moving()
	if actor.target:
		var direction = sign(actor.target.global_position.x - actor.global_position.x)
		actor.face_direction(direction)
	actor.trigger_special_attack()
	attack_timeout = 0.0
	last_transition = 0.0
	print("SpecialAttackState: Started attack")

func physics_update(delta: float) -> void:
	attack_timeout += delta
	last_transition += delta
	
	if attack_timeout >= max_attack_time:
		print("SpecialAttackState: Attack timeout, returning to patrol")
		if last_transition >= transition_cooldown:
			transition_to("PatrolState")
			last_transition = 0.0

func exit() -> void:
	actor.deactivate_light_attack()
	actor.deactivate_heavy_attack()
