extends Node
class_name PotionComponent

# Tín hiệu thông báo số bình thay đổi
signal potion_count_changed(new_count: int)
signal potion_used(heal_amount: float)
signal potion_cooldown_updated(remaining_cooldown: float)
signal max_potions_upgraded(new_max: int)

# Cấu hình
@export var max_potions: int = 3
@export var heal_percent: float = 0.2  # 20%
@export var cooldown_time: float = 5.0  # 5 giây

# Trạng thái
var current_potions: int = 3:
	set(value):
		current_potions = value
		potion_count_changed.emit(current_potions)

var current_cooldown: float = 0.0
var is_on_cooldown: bool = false

# Tham chiếu
var health_component: HealthComponent

func _ready() -> void:
	current_potions = max_potions

func _process(delta: float) -> void:
	# Cập nhật cooldown
	if is_on_cooldown:
		current_cooldown -= delta
		potion_cooldown_updated.emit(current_cooldown)
		
		if current_cooldown <= 0:
			is_on_cooldown = false
			current_cooldown = 0.0

# Sử dụng bình máu
func use_potion() -> bool:
	# Kiểm tra điều kiện
	if current_potions <= 0:
		print("No potions left!")
		return false
	
	if is_on_cooldown:
		print("Potion on cooldown!")
		return false
	
	if not health_component:
		print("HealthComponent not assigned!")
		return false
	
	# Kiểm tra xem có cần hồi máu không
	if health_component.current_value >= health_component.max_value:
		print("Health already full!")
		return false
	
	# Tính lượng máu hồi
	var heal_amount = health_component.max_value * heal_percent
	health_component.change(heal_amount)
	
	# Giảm số bình và bắt đầu cooldown
	current_potions -= 1
	is_on_cooldown = true
	current_cooldown = cooldown_time
	
	potion_used.emit(heal_amount)
	print("Used potion! Healed: ", heal_amount, " | Potions left: ", current_potions)
	return true
	

# Thêm bình (khi nhận vật phẩm)
func add_potion(amount: int = 1) -> void:
	#nếu chưa đầy
	current_potions = min(current_potions + amount, max_potions)

# Nâng cấp số bình tối đa
func upgrade_max_potions(new_max: int) -> void:
	var old_max = max_potions
	max_potions = new_max
	current_potions = min(current_potions + (new_max - old_max), new_max)  # Thêm bình khi nâng cấp
	max_potions_upgraded.emit(max_potions)
	print("Potions upgraded! Max potions: ", max_potions)
	
func can_use_potion() -> bool:
	return current_potions > 0 and not is_on_cooldown and health_component and health_component.current_value < health_component.max_value

# Reset cooldown (khi chết hoặc respawn)
func reset_cooldown() -> void:
	is_on_cooldown = false
	current_cooldown = 0.0
