extends CharacterBody2D
class_name Soul

var is_facing_right: bool = true
@export var run_speed: float = 200.0
@export var jump_force: float = 400.0
@export var gravity: float = 980.0

@onready var animation_player: AnimationPlayer = $AnimationPlayer
@onready var state_machine: StateMachine = $StateMachine
@onready var sprite: Sprite2D = $Sprite2D
@onready var dash_component: DashComponent = $DashComponent
@onready var health_component: HealthComponent = $HealthComponent
@onready var potion_component: PotionComponent = $PotionComponent
@onready var hurtbox_component: HurtboxComponent = $HurtboxComponent
@onready var interact_area: Area2D = $InteractArea

var can_dash: bool = true
var current_animation: String = ""
var possessed_body: Node2D = null

func _ready() -> void:
	add_to_group("Player")
	state_machine.state_changed.connect(_on_state_changed)
	if hurtbox_component:
		hurtbox_component.took_damage.connect(_on_took_damage)
	if health_component:
		health_component.died.connect(_on_died)
	if potion_component and health_component:
		potion_component.health_component = health_component
	if interact_area:
		interact_area.body_entered.connect(_on_interact_area_body_entered)

func _physics_process(delta: float) -> void:
	if state_machine.current_state and state_machine.current_state.name != "DashState":
		velocity.y += gravity * delta
	move_and_slide()
	if is_on_floor():
		reset_dash()
	_handle_potion_input()
	if Input.is_action_just_pressed("possess") and interact_area.get_overlapping_bodies().size() > 0:
		var target = interact_area.get_overlapping_bodies()[0]
		if target.is_in_group("Enemies"):
			var possession_mgr = get_tree().get_first_node_in_group("PossessionManager")
			if possession_mgr:
				possession_mgr.possess(target)

func _on_took_damage(amount: int) -> void:
	print("Soul took damage: ", amount)
	# Chỉ giảm máu của Soul nếu không nhập xác
	if health_component and not possessed_body:
		health_component.change(-amount)

func _handle_potion_input() -> void:
	if Input.is_action_just_pressed("use_potion"):
		if potion_component and potion_component.can_use_potion():
			potion_component.use_potion()

func _on_state_changed(new_state_name: String) -> void:
	update_animation(new_state_name)

func update_animation(state_name: String) -> void:
	if not animation_player: return
	var animation_suffix = "_right" if is_facing_right else "_left"
	var new_animation = state_name.trim_suffix("State").to_lower() + animation_suffix
	if animation_player.has_animation(new_animation) and current_animation != new_animation:
		animation_player.play(new_animation)
		current_animation = new_animation
	else:
		var fallback = "idle" + animation_suffix
		if animation_player.has_animation(fallback) and current_animation != fallback:
			animation_player.play(fallback)
			current_animation = fallback

func reset_dash() -> void:
	dash_component.reset_dash()

func _on_died() -> void:
	if potion_component:
		potion_component.reset_cooldown()
	get_tree().reload_current_scene()

func add_potion(amount: int = 1) -> void:
	if potion_component:
		potion_component.add_potion(amount)

func upgrade_max_potions(new_max: int) -> void:
	if potion_component:
		potion_component.upgrade_max_potions(new_max)

func _on_interact_area_body_entered(body: Node2D) -> void:
	# InteractArea chỉ dùng để kiểm tra có thể nhập xác
	# Không xử lý pickup ở đây vì potion_pickup.gd tự xử lý
	pass

# Gọi bởi PossessionManager khi nhập xác
func on_possess(target: Node2D) -> void:
	possessed_body = target
	print("Soul possessed: ", target.name)

# Gọi bởi PossessionManager khi thoát nhập xác
func on_unpossess() -> void:
	print("Soul unpossessed from: ", possessed_body.name if possessed_body else "unknown")
	possessed_body = null
