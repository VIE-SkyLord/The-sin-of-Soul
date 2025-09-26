extends Node
class_name TargetFinder

@export var target_group: String = "Player"  # Nhóm của Soul
@export var search_interval: float = 1.0  # Tìm mỗi giây

var actor: CharacterBody2D
var search_timer: float = 0.0

func _ready():
	actor = get_parent()
	# Thử tìm target ngay lập tức
	find_target()

func _process(delta):
	search_timer += delta
	if search_timer >= search_interval:
		search_timer = 0.0
		# Nếu chưa có target, tiếp tục tìm
		if actor.target == null:
			find_target()

func find_target():
	var targets = get_tree().get_nodes_in_group(target_group)
	if targets.size() > 0:
		actor.set_target(targets[0])
		print("TargetFinder: Found target - ", targets[0].name)
	else:
		print("TargetFinder: No targets found in group '", target_group, "'")
