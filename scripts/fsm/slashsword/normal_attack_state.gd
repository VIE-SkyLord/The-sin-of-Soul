# NormalAttackState.gd
extends State
class_name NormalAttackState

var attack_timeout: float = 0.0
var max_attack_time: float = 2.0
var transition_cooldown: float = 0.1
var last_transition: float = 0.0

func enter() -> void:
	actor.stop_moving()
	if actor.target:
		var direction = sign(actor.target.global_position.x - actor.global_position.x)
		actor.face_direction(direction)
	actor.trigger_normal_attack()
	attack_timeout = 0.0
	last_transition = 0.0
	print("NormalAttackState: Started attack")

func physics_update(delta: float) -> void:
	attack_timeout += delta
	last_transition += delta
	
	if attack_timeout >= max_attack_time:
		print("NormalAttackState: Attack timeout, returning to patrol")
		if last_transition >= transition_cooldown:
			transition_to("PatrolState")
			last_transition = 0.0

func exit() -> void:
	actor.deactivate_light_attack()
