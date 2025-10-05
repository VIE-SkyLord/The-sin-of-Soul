extends CharacterBody2D
class_name LightArcher

@onready var state_machine: StateMachine = $StateMachine
@onready var animation_player: AnimationPlayer = $AnimationPlayer
@onready var health_component: HealthComponent = $HealthComponent
@onready var hurtbox_component: HurtboxComponent = $HurtboxComponent
@onready var vision_raycast: RayCast2D = $VisionRayCast
@onready var light_attack_area: HitboxComponent = $LightAttackArea
@onready var heavy_attack_area: HitboxComponent = $HeavyAttackArea
@onready var possessable_component: PossessableComponent = $PossessableComponent

@export var gravity: float = 980.0
@export var move_speed: float = 80.0
@export var jump_force: float = 400.0
@export var patrol_range: float = 200.0
@export var attack_range: float = 100.0

var target: Node2D = null
var patrol_center: Vector2
var current_facing_direction: int = 1

func _ready():
	add_to_group("Enemies")
	patrol_center = global_position
	hurtbox_component.took_damage.connect(_on_took_damage)
	health_component.died.connect(_on_died)
	animation_player.animation_finished.connect(_on_animation_finished)
	target = get_tree().get_first_node_in_group("Player")
	print("LightArcher: Initialized with target ", target.name if target else "null")

func _physics_process(delta):
	velocity.y += gravity * delta  # Apply gravity always
	move_and_slide()  # Let state machine handle velocity.x

func _on_took_damage(amount: int):
	# Trừ HP trước
	if health_component:
		health_component.change(-amount)
		print(name, " took damage: ", amount, " | HP: ", health_component.current_value, "/", health_component.max_value)
	
	# Kiểm tra nếu đang bị nhập và chết thì tự động thoát nhập
	var possession_mgr = get_tree().get_first_node_in_group("PossessionManager")
	if possession_mgr and possession_mgr.current_body == self:
		if health_component and health_component.current_value <= 0:
			possession_mgr.unpossess(true)

func _on_died():
	if "DeathState" in state_machine.states:
		state_machine.set_current_state(state_machine.states["DeathState"])

func can_see_target() -> bool:
	if target and is_instance_valid(target):
		vision_raycast.target_position = to_local(target.global_position)
		vision_raycast.force_raycast_update()
		return vision_raycast.get_collider() == target
	return false

func move_towards(position: Vector2) -> void:
	var direction = Vector2.ZERO
	if global_position.x < position.x - 5.0:
		direction.x = 1
	elif global_position.x > position.x + 5.0:
		direction.x = -1
	velocity.x = direction.x * move_speed
	if direction.x != 0:
		face_direction(direction.x)
	# Animation handled in state scripts to avoid conflicts

func stop_moving() -> void:
	velocity.x = 0
	play_idle_animation()

func face_direction(direction: float) -> void:
	if direction != 0 and sign(direction) != current_facing_direction:
		current_facing_direction = sign(direction)
		var flip_scale = Vector2(current_facing_direction, 1)
		$Sprite2D.scale.x = abs($Sprite2D.scale.x) * current_facing_direction
		$LightAttackArea.scale = flip_scale
		if heavy_attack_area:
			heavy_attack_area.scale = flip_scale

func play_idle_animation() -> void:
	if animation_player.has_animation("light_archer/idle"):
		animation_player.play("light_archer/idle")
	else:
		print("LightArcher: Missing idle animation")

func play_run_animation() -> void:
	if animation_player.has_animation("light_archer/run"):
		animation_player.play("light_archer/run")
	else:
		print("LightArcher: Missing run animation")

func play_death_animation() -> void:
	if animation_player.has_animation("light_archer/die"):
		animation_player.play("light_archer/die")
	else:
		print("LightArcher: Missing die animation")

func trigger_normal_attack() -> void:
	if animation_player.has_animation("light_archer/normal_attack"):
		animation_player.play("light_archer/normal_attack")
		if state_machine.current_state.name != "PossessedState":
			state_machine.on_transition_requested("NormalAttackState")
	else:
		print("LightArcher: Missing normal_attack animation")

func trigger_special_attack() -> void:
	if animation_player.has_animation("light_archer/special_attack"):
		animation_player.play("light_archer/special_attack")
		if state_machine.current_state.name != "PossessedState":
			state_machine.on_transition_requested("SpecialAttackState")
	else:
		print("LightArcher: Missing special_attack animation")

func activate_light_attack() -> void:
	if light_attack_area:
		light_attack_area.set_active(true)

func activate_heavy_attack() -> void:
	if heavy_attack_area:
		heavy_attack_area.set_active(true)

func deactivate_light_attack() -> void:
	if light_attack_area:
		light_attack_area.set_active(false)

func deactivate_heavy_attack() -> void:
	if heavy_attack_area:
		heavy_attack_area.set_active(false)

func _on_animation_finished(anim_name: String):
	if anim_name in ["light_archer/normal_attack", "light_archer/special_attack"]:
		deactivate_light_attack()
		deactivate_heavy_attack()
		if state_machine and "PatrolState" in state_machine.states and state_machine.current_state.name != "PossessedState":
			state_machine.on_transition_requested("PatrolState")

func set_target(new_target: Node2D) -> void:
	target = new_target
