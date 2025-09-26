extends StatComponent
class_name HealthComponent

signal died

func _ready() -> void:
	super._ready()
	value_changed.connect(_on_value_changed)

func _on_value_changed(old_value: float, new_value: float) -> void:
	if new_value <= 0:
		died.emit()
