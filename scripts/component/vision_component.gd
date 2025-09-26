extends RayCast2D
class_name VisionComponent

@export var vision_angle: float = 90.0
@export var vision_distance: float = 300.0

func _ready():
	target_position = Vector2(0, -vision_distance)

func can_see_target(target: Node2D) -> bool:
	if target:
		global_position = get_parent().global_position
		target_position = to_local(target.global_position)
		force_raycast_update()
		return get_collider() == target
	return false
