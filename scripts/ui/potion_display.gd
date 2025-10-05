extends HBoxContainer

@export var full_potion_texture: Texture2D
@export var empty_potion_texture: Texture2D
@export var cooldown_potion_texture: Texture2D

# Cài đặt hiển thị
@export_group("Display Settings")
@export var icon_size: Vector2 = Vector2(40, 40)
@export var icon_spacing: int = 8
@export var show_cooldown_timer: bool = true
@export var animate_changes: bool = true

# Màu sắc
@export_group("Colors")
@export var normal_color: Color = Color.WHITE
@export var cooldown_color: Color = Color(0.5, 0.5, 0.5, 0.8)
@export var empty_color: Color = Color(1, 1, 1, 0.5)

var potion_icons: Array = []
var cooldown_labels: Array = []  # Label hiển thị thời gian cooldown
var soul: Soul
var cooldown_timer_label: Label  # Label tổng thể cho cooldown

func _ready() -> void:
	add_theme_constant_override("separation", icon_spacing)
	
	# Thu thập các icon có sẵn
	for child in get_children():
		if child is TextureRect:
			potion_icons.append(child)
			_setup_icon(child)
	
	# Ẩn tất cả icon ban đầu
	for icon in potion_icons:
		icon.visible = false
	
	# Tạo label cooldown tổng thể
	_create_cooldown_timer_label()
	
	# Kết nối với soul
	_find_and_connect_soul()

func _find_and_connect_soul() -> void:
	soul = get_tree().get_first_node_in_group("Player")
	if soul and soul.potion_component:
		_setup_connections()
	else:
		call_deferred("_find_and_connect_soul")

func _setup_connections() -> void:
	if not soul or not soul.potion_component:
		return
	
	var pc = soul.potion_component
	
	# Kết nối signals
	if not pc.potion_count_changed.is_connected(_on_potion_count_changed):
		pc.potion_count_changed.connect(_on_potion_count_changed)
	if not pc.potion_cooldown_updated.is_connected(_on_potion_cooldown_updated):
		pc.potion_cooldown_updated.connect(_on_potion_cooldown_updated)
	if not pc.potion_used.is_connected(_on_potion_used):
		pc.potion_used.connect(_on_potion_used)
	
	# Cập nhật hiển thị ban đầu
	_on_potion_count_changed(pc.current_potions)

func _setup_icon(icon: TextureRect) -> void:
	icon.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.custom_minimum_size = icon_size
	icon.pivot_offset = icon_size / 2

func _create_cooldown_timer_label() -> void:
	if not show_cooldown_timer:
		return
	
	cooldown_timer_label = Label.new()
	cooldown_timer_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	cooldown_timer_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	cooldown_timer_label.add_theme_font_size_override("font_size", 14)
	cooldown_timer_label.modulate = Color(1, 0.3, 0.3)
	cooldown_timer_label.visible = false
	add_child(cooldown_timer_label)

func _on_potion_count_changed(new_count: int) -> void:
	if not soul or not soul.potion_component:
		return
	
	var max_potions = soul.potion_component.max_potions
	
	# Tạo thêm icon nếu cần
	while potion_icons.size() < max_potions:
		_add_potion_icon()
	
	# Cập nhật hiển thị từng icon
	for i in range(potion_icons.size()):
		var icon = potion_icons[i]
		
		if i < max_potions:
			icon.visible = true
			
			# Xác định texture và màu
			if i < new_count:
				icon.texture = full_potion_texture
				icon.modulate = normal_color
			else:
				icon.texture = empty_potion_texture
				icon.modulate = empty_color
			
			# Hiệu ứng fade in khi tăng max potions
			if animate_changes and icon.modulate.a < 1.0 and i < new_count:
				_animate_fade_in(icon)
		else:
			if animate_changes:
				_animate_fade_out(icon)
			else:
				icon.visible = false

func _on_potion_cooldown_updated(remaining_cooldown: float) -> void:
	if not soul or not soul.potion_component:
		return
	
	var pc = soul.potion_component
	
	if pc.is_on_cooldown and remaining_cooldown > 0:
		# Hiển thị cooldown trên tất cả bình trống
		var current = pc.current_potions
		
		for i in range(potion_icons.size()):
			if i >= current and i < pc.max_potions:
				var icon = potion_icons[i]
				icon.modulate = cooldown_color
				
				# Hiệu ứng nhấp nháy nhẹ
				if animate_changes:
					_animate_pulse(icon)
		
		# Hiển thị thời gian cooldown
		if show_cooldown_timer and cooldown_timer_label:
			cooldown_timer_label.visible = true
			cooldown_timer_label.text = "%.1fs" % remaining_cooldown
			
			# Di chuyển label về phía bình đầu tiên đang cooldown
			if current < pc.max_potions and current < potion_icons.size():
				var target_icon = potion_icons[current]
				cooldown_timer_label.position = target_icon.position + Vector2(0, icon_size.y + 5)
	else:
		# Kết thúc cooldown
		if cooldown_timer_label:
			cooldown_timer_label.visible = false
		
		# Khôi phục màu bình thường
		_on_potion_count_changed(pc.current_potions)
		
		# Hiệu ứng khi có bình mới
		if animate_changes and pc.current_potions > 0:
			_animate_refill(potion_icons[pc.current_potions - 1])

func _on_potion_used(_heal_amount: float) -> void:
	if not soul or not soul.potion_component:
		return
	
	var used_index = soul.potion_component.current_potions
	
	if used_index < potion_icons.size() and animate_changes:
		_animate_use(potion_icons[used_index])

func _add_potion_icon() -> void:
	var new_icon = TextureRect.new()
	_setup_icon(new_icon)
	
	# Thêm vào trước cooldown label
	if cooldown_timer_label:
		var label_index = get_child_count() - 1
		add_child(new_icon)
		move_child(new_icon, label_index)
	else:
		add_child(new_icon)
	
	potion_icons.append(new_icon)

# === HIỆU ỨNG ANIMATION ===

func _animate_use(icon: TextureRect) -> void:
	var tween = create_tween()
	tween.set_parallel(true)
	
	# Phóng to và mờ dần
	tween.tween_property(icon, "scale", Vector2(1.5, 1.5), 0.2).set_trans(Tween.TRANS_BACK)
	tween.tween_property(icon, "modulate:a", 0.0, 0.2)
	
	tween.chain()
	tween.tween_property(icon, "scale", Vector2(1.0, 1.0), 0.0)
	tween.tween_callback(func(): _on_potion_count_changed(soul.potion_component.current_potions))

func _animate_refill(icon: TextureRect) -> void:
	icon.scale = Vector2(0.5, 0.5)
	icon.modulate.a = 0.0
	
	var tween = create_tween()
	tween.set_parallel(true)
	
	# Hiệu ứng bounce khi xuất hiện
	tween.tween_property(icon, "scale", Vector2(1.2, 1.2), 0.15).set_trans(Tween.TRANS_BACK)
	tween.tween_property(icon, "modulate:a", 1.0, 0.15)
	
	tween.chain()
	tween.tween_property(icon, "scale", Vector2(1.0, 1.0), 0.1).set_trans(Tween.TRANS_BOUNCE)

func _animate_pulse(icon: TextureRect) -> void:
	var tween = create_tween()
	tween.set_loops(2)
	tween.tween_property(icon, "scale", Vector2(1.1, 1.1), 0.3)
	tween.tween_property(icon, "scale", Vector2(1.0, 1.0), 0.3)

func _animate_fade_in(icon: TextureRect) -> void:
	var tween = create_tween()
	tween.tween_property(icon, "modulate:a", 1.0, 0.3)

func _animate_fade_out(icon: TextureRect) -> void:
	var tween = create_tween()
	tween.tween_property(icon, "modulate:a", 0.0, 0.2)
	tween.tween_callback(func(): icon.visible = false)

# Hàm công khai để cập nhật max potions
func update_max_potions_display(new_max: int) -> void:
	if soul and soul.potion_component:
		_on_potion_count_changed(soul.potion_component.current_potions)
