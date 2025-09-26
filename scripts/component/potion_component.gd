extends Node
class_name PotionComponent

# Signals
signal potion_count_changed(new_count: int)
signal potion_used(heal_amount: float)
signal potion_cooldown_updated(remaining_cooldown: float)
signal max_potions_upgraded(new_max: int)
signal potion_add_failed(reason: String)  # New signal for failed add

# Configuration
@export var max_potions: int = 3
@export var heal_percent: float = 0.2  # 20%
@export var cooldown_time: float = 5.0  # 5 seconds

# State
var current_potions: int = 3:
	set(value):
		current_potions = value
		potion_count_changed.emit(current_potions)

var current_cooldown: float = 0.0
var is_on_cooldown: bool = false

# Reference
var health_component: HealthComponent

func _ready() -> void:
	current_potions = max_potions

func _process(delta: float) -> void:
	# Update cooldown
	if is_on_cooldown:
		current_cooldown -= delta
		potion_cooldown_updated.emit(current_cooldown)
		
		if current_cooldown <= 0:
			is_on_cooldown = false
			current_cooldown = 0.0

# Use potion
func use_potion() -> bool:
	if current_potions <= 0:
		print("No potions left!")
		return false
	
	if is_on_cooldown:
		print("Potion on cooldown!")
		return false
	
	if not health_component:
		print("HealthComponent not assigned!")
		return false
	
	if health_component.current_value >= health_component.max_value:
		print("Health already full!")
		return false
	
	var heal_amount = health_component.max_value * heal_percent
	health_component.change(heal_amount)
	
	current_potions -= 1
	is_on_cooldown = true
	current_cooldown = cooldown_time
	
	potion_used.emit(heal_amount)
	print("Used potion! Healed: ", heal_amount, " | Potions left: ", current_potions)
	return true

# Add potion
func add_potion(amount: int = 1) -> void:
	if current_potions >= max_potions:
		potion_add_failed.emit("Potion inventory full!")
		print("Cannot add potion: inventory full!")
		return
	current_potions = min(current_potions + amount, max_potions)
	print("Added potion! Current potions: ", current_potions)

# Upgrade max potions
func upgrade_max_potions(new_max: int) -> void:
	var old_max = max_potions
	max_potions = new_max
	current_potions = min(current_potions + (new_max - old_max), new_max)
	max_potions_upgraded.emit(max_potions)
	print("Potions upgraded! Max potions: ", max_potions)

func can_use_potion() -> bool:
	return current_potions > 0 and not is_on_cooldown and health_component and health_component.current_value < health_component.max_value

# Reset cooldown
func reset_cooldown() -> void:
	is_on_cooldown = false
	current_cooldown = 0.0d
