class_name SafeArrayUtils

# Safe array access functions
static func safe_get(array: Array, index: int, default_value = null):
	if array.is_empty() or index < 0 or index >= array.size():
		return default_value
	return array[index]

static func safe_set(array: Array, index: int, value) -> bool:
	if index < 0 or index >= array.size():
		return false
	array[index] = value
	return true

static func safe_remove_at(array: Array, index: int) -> bool:
	if index < 0 or index >= array.size():
		return false
	array.remove_at(index)
	return true

static func safe_pop_back(array: Array):
	if array.is_empty():
		return null
	return array.pop_back()

static func safe_pop_front(array: Array):
	if array.is_empty():
		return null  
	return array.pop_front()

# Batch operations
static func clean_invalid_objects(array: Array) -> Array:
	return array.filter(func(obj): return obj != null and is_instance_valid(obj))

static func limit_array_size(array: Array, max_size: int) -> void:
	while array.size() > max_size:
		array.pop_front()

# Debug helper
static func validate_array_access(array: Array, index: int, context: String = "") -> bool:
	if index < 0 or index >= array.size():
		var error_msg = "Array bounds error: index %d, size %d" % [index, array.size()]
		if not context.is_empty():
			error_msg += " in " + context
		push_error(error_msg)
		return false
	return true
