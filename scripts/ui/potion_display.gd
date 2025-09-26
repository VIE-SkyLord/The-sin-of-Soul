extends HBoxContainer

@export var full_potion_texture: Texture2D
@export var empty_potion_texture: Texture2D
@export var cooldown_potion_texture: Texture2D  # Optional: texture khi đang cooldown

var potion_icons: Array = []
var soul: Soul

func _ready() -> void:
	# Thu thập tất cả các TextureRect con
	for child in get_children():
		if child is TextureRect:
			potion_icons.append(child)
	
	# Ẩn tất cả icon ban đầu
	for icon in potion_icons:
		icon.visible = false
	
	# Tìm soul trong scene
	soul = get_tree().get_first_node_in_group("Player")
	if soul and soul.potion_component:
		_setup_connections()
	else:
		# Thử kết nối lại sau một khung hình
		call_deferred("_setup_connections")

func _setup_connections() -> void:
	if soul and soul.potion_component:
		soul.potion_component.potion_count_changed.connect(_on_potion_count_changed)
		soul.potion_component.potion_cooldown_updated.connect(_on_potion_cooldown_updated)
		
		# Cập nhật hiển thị ban đầu
		_on_potion_count_changed(soul.potion_component.current_potions)
	else:
		print("PotionDisplay: Soul or PotionComponent not found!")

func _on_potion_count_changed(new_count: int) -> void:
	# Hiển thị số icon tương ứng với max_potions
	var max_potions = soul.potion_component.max_potions if soul and soul.potion_component else 3
	
	# Đảm bảo có đủ icon
	while potion_icons.size() < max_potions:
		_add_potion_icon()
	
	# Ẩn/hiện icon theo max_potions
	for i in range(potion_icons.size()):
		if i < max_potions:
			potion_icons[i].visible = true
			# Cập nhật texture
			if i < new_count:
				potion_icons[i].texture = full_potion_texture
			else:
				potion_icons[i].texture = empty_potion_texture
		else:
			potion_icons[i].visible = false

func _on_potion_cooldown_updated(_remaining_cooldown: float) -> void:
	# Hiển thị cooldown trên bình đầu tiên (nếu đang cooldown)
	if soul and soul.potion_component and soul.potion_component.is_on_cooldown:
		if potion_icons.size() > 0:
			# Có thể thay đổi texture hoặc thêm hiệu ứng
			potion_icons[0].texture = cooldown_potion_texture if cooldown_potion_texture else empty_potion_texture
			
			# Hiển thị thời gian cooldown (tuỳ chọn)
			potion_icons[0].modulate = Color.GRAY
	else:
		# Khôi phục màu sắc bình thường
		for icon in potion_icons:
			icon.modulate = Color.WHITE
		# Cập nhật lại số bình
		if soul and soul.potion_component:
			_on_potion_count_changed(soul.potion_component.current_potions)

func _add_potion_icon() -> void:
	var new_icon = TextureRect.new()
	new_icon.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	new_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	new_icon.custom_minimum_size = Vector2(32, 32)  # Kích thước phù hợp
	add_child(new_icon)
	potion_icons.append(new_icon)

# Hàm để nâng cấp hiển thị khi tăng số bình tối đa
func update_max_potions_display(new_max: int) -> void:
	_on_potion_count_changed(soul.potion_component.current_potions if soul and soul.potion_component else new_max)
	
func _on_potion_used(_heal_amount: float) -> void:
	# Hiệu ứng cho bình vừa được sử dụng
	if potion_icons.size() > soul.potion_component.current_potions:
		var used_potion_index = soul.potion_component.current_potions
		_create_use_effect(potion_icons[used_potion_index])

func _create_use_effect(icon: TextureRect) -> void:
	# Tạo hiệu ứng nhấp nháy hoặc phóng to thu nhỏ
	var tween = create_tween()
	tween.tween_property(icon, "scale", Vector2(1.2, 1.2), 0.1)
	tween.tween_property(icon, "scale", Vector2(1.0, 1.0), 0.1)
