# PatrolState.gd
extends State
class_name PatrolState

var patrol_direction: float = 1.0
var patrol_timer: float = 0.0
var transition_cooldown: float = 0.1  # Prevent instant transitions
var last_transition: float = 0.0

func enter() -> void:
	patrol_timer = 0.0
	patrol_direction = 1.0 if randf() > 0.5 else -1.0
	print("PatrolState: Entered - direction: ", patrol_direction)

func physics_update(delta: float) -> void:
	last_transition += delta
	if not actor.is_on_floor():
		return
	
	patrol_timer += delta
	
	if patrol_timer > randf_range(2.0, 4.0):
		patrol_direction *= -1
		patrol_timer = 0.0
	
	var target_x = actor.patrol_center.x + actor.patrol_range * patrol_direction
	var move_target = Vector2(target_x, actor.global_position.y)
	
	actor.move_towards(move_target)
	
	# Play run animation only if actually moving
	if abs(actor.velocity.x) > 0:
		actor.play_run_animation()
	else:
		actor.play_idle_animation()
	
	if actor.can_see_target() and last_transition >= transition_cooldown:
		transition_to("ChaseState")
		last_transition = 0.0

func exit() -> void:
	actor.stop_moving()
