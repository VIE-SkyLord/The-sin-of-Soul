# skill_mons.gd
extends TextureProgressBar

var possession_manager: PossessionManager = null
var current_possessed_state: PossessedState = null
var is_possessed: bool = false

func _ready() -> void:
	# Tìm PossessionManager
	possession_manager = get_tree().get_first_node_in_group("PossessionManager")
	
	if possession_manager:
		# Kết nối signals
		possession_manager.possessed.connect(_on_possessed)
		possession_manager.unpossessed.connect(_on_unpossessed)
		print("SkillMons: Connected to PossessionManager")
	else:
		push_error("SkillMons: PossessionManager not found!")
	
	# Ẩn skill icon khi chưa nhập xác
	visible = false
	value = 0

func _process(delta: float) -> void:
	if is_possessed and current_possessed_state:
		# Cập nhật thanh cooldown
		var cooldown_percent = (current_possessed_state.last_special_attack / current_possessed_state.special_attack_cooldown) * 100.0
		value = clamp(cooldown_percent, 0, 100)

func _on_possessed(body: Node2D) -> void:
	print("SkillMons: Body possessed - ", body.name)
	
	# Tìm PossessedState của body vừa nhập
	if body.has_node("StateMachine"):
		var state_machine = body.get_node("StateMachine")
		if state_machine.current_state is PossessedState:
			current_possessed_state = state_machine.current_state
			is_possessed = true
			visible = true
			value = 0
			print("SkillMons: Tracking special attack cooldown - ", current_possessed_state.special_attack_cooldown, "s")
		else:
			push_error("SkillMons: Current state is not PossessedState!")
	else:
		push_error("SkillMons: Body has no StateMachine!")

func _on_unpossessed(body: Node2D) -> void:
	print("SkillMons: Body unpossessed - ", body.name)
	
	# Reset và ẩn skill icon
	is_possessed = false
	current_possessed_state = null
	visible = false
	value = 0
	scale = Vector2(0.1, 0.1)
	modulate = Color(1, 1, 1, 1)
