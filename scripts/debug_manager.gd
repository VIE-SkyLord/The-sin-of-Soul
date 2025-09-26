extends Node

enum DebugCategory {
	NONE = 0,
	STATE_MACHINE = 1,
	COMBAT = 2,
	MOVEMENT = 4,
	ANIMATION = 8,
	COMPONENTS = 16,
	ALL = 31
}

# Safe category name lookup
var _category_names: Dictionary = {
	DebugCategory.NONE: "NONE",
	DebugCategory.STATE_MACHINE: "STATE_MACHINE", 
	DebugCategory.COMBAT: "COMBAT",
	DebugCategory.MOVEMENT: "MOVEMENT",
	DebugCategory.ANIMATION: "ANIMATION",
	DebugCategory.COMPONENTS: "COMPONENTS",
	DebugCategory.ALL: "ALL"
}

var debug_flags: int = DebugCategory.NONE
var log_to_file: bool = false
var max_log_entries: int = 1000
var _log_entries: Array[Dictionary] = []
var _log_file: FileAccess

func _ready() -> void:
	_load_debug_settings()

func _load_debug_settings() -> void:
	if OS.has_feature("debug"):
		debug_flags = DebugCategory.ALL

func log(category: DebugCategory, message: String, context: Node = null) -> void:
	if not (debug_flags & category):
		return
	
	# SAFE: Sử dụng Dictionary lookup thay vì array access
	var category_name = _category_names.get(category, "UNKNOWN")
	
	var log_entry = {
		"timestamp": Time.get_datetime_string_from_system(),
		"category": category_name,
		"message": message,
		"context": context.name if context else "Global"
	}
	
	# SAFE: Kiểm tra size trước khi thao tác
	if _log_entries.size() >= max_log_entries:
		_log_entries.pop_front()
	
	_log_entries.push_back(log_entry)
	
	var formatted_message = "[%s][%s] %s" % [log_entry.category, log_entry.context, message]
	print(formatted_message)
	
	if log_to_file:
		_write_to_file(formatted_message)

func _write_to_file(message: String) -> void:
	if not _log_file:
		_log_file = FileAccess.open("user://debug.log", FileAccess.WRITE)
	
	if _log_file:
		_log_file.store_line(message)
		_log_file.flush()

func get_log_entries(category: DebugCategory = DebugCategory.ALL) -> Array[Dictionary]:
	if category == DebugCategory.ALL:
		return _log_entries.duplicate()
	
	var category_name = _category_names.get(category, "UNKNOWN")
	return _log_entries.filter(func(entry): return entry.category == category_name)
