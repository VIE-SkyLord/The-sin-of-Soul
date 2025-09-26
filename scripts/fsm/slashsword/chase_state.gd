# ChaseState.gd
extends State
class_name ChaseState

var chase_timeout: float = 0.0
var max_chase_time: float = 10.0
var transition_cooldown: float = 0.1  # Prevent instant transitions
var last_transition: float = 0.0

func enter() -> void:
	print("ChaseState: Entered")
	chase_timeout = 0.0
	last_transition = 0.0

func physics_update(delta: float) -> void:
	chase_timeout += delta
	last_transition += delta
	
	if chase_timeout >= max_chase_time:
		print("ChaseState: Chase timeout, returning to patrol")
		if last_transition >= transition_cooldown:
			transition_to("PatrolState")
			last_transition = 0.0
		return
	
	if not actor.is_on_floor() or not actor.target:
		if last_transition >= transition_cooldown:
			transition_to("PatrolState")
			last_transition = 0.0
		return
	
	if actor.can_see_target():
		var distance_to_target = actor.global_position.distance_to(actor.target.global_position)
		
		if distance_to_target <= actor.attack_range:
			actor.stop_moving()
			if distance_to_target <= actor.attack_range * 0.5 and randf() > 0.7:
				if last_transition >= transition_cooldown:
					transition_to("SpecialAttackState")
					last_transition = 0.0
			else:
				if last_transition >= transition_cooldown:
					transition_to("NormalAttackState")
					last_transition = 0.0
		else:
			actor.move_towards(actor.target.global_position)
			# Play run animation only if actually moving
			if abs(actor.velocity.x) > 0:
				actor.play_run_animation()
			else:
				actor.play_idle_animation()
	else:
		if last_transition >= transition_cooldown:
			transition_to("PatrolState")
			last_transition = 0.0

func exit() -> void:
	print("ChaseState: Exited")
