# DeathState.gd
extends State
class_name DeathState

var death_timer: float = 0.0
const DEATH_DURATION: float = 10.0

func enter() -> void:
	actor.stop_moving()
	actor.set_physics_process(false)
	
	# Gọi hàm play_death_animation() nếu actor có hàm này
	if actor.has_method("play_death_animation"):
		actor.play_death_animation()
	
	# Reset timer
	death_timer = 0.0

func update(delta: float) -> void:
	death_timer += delta
	
	# Sau 10 giây thì xóa actor
	if death_timer >= DEATH_DURATION:
		actor.queue_free()

func exit() -> void:
	pass
