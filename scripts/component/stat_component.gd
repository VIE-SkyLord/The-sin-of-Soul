extends Node
class_name StatComponent

signal value_changed(old_value: float, new_value: float)

@export var max_value: float = 100
@export var rate_recovery: float = 0  # Hồi phục mỗi giây

var current_value: float

func _ready() -> void:
	current_value = max_value
	value_changed.emit(current_value, current_value)

func _process(delta: float) -> void:
	if rate_recovery != 0:
		var new_value = current_value + rate_recovery * delta
		set_value(new_value)

func set_value(value: float) -> void:
	var old_value = current_value
	current_value = clamp(value, 0, max_value)
	if old_value != current_value:
		value_changed.emit(old_value, current_value)

func change(amount: float) -> void:
	set_value(current_value + amount)
