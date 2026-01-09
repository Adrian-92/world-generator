extends Node
@onready var generator = $"../WorldGenerator"
@onready var interaction_controller = $"../InteractionController"
@onready var debug_label: RichTextLabel = $"../CanvasLayer/RichTextLabel"
var mouse_pos = ""
var world_pos = ""
var current_biome = ""
var biome_names = ["FOREST", "DESERT", "ICE", "TUNDRA", "CAVE","LAVA"]

func _ready() -> void:
	pass

func set_debug_text():
	debug_label.text =  world_pos + mouse_pos + current_biome

func _mouse_tile_changed(new_pos: Vector2i) -> void:
	world_pos = "\n World-Pos: %d , %d" % [new_pos.x,new_pos.y]
	check_current_biome(new_pos)
	set_debug_text()

func _mouse_screen_pos_changed(new_pos: Vector2i) -> void:
	mouse_pos = "\n Mouse-Pos: %1f , %1f" % [new_pos.x,new_pos.y]
	set_debug_text()

func check_current_biome(new_pos: Vector2i):
	if biome_names.is_empty(): return
	# Hole den Index vom C# WorldGenerator
	var biome_index = generator.GetBiomeAt(new_pos.x, new_pos.y)
	# Absicherung gegen Index-Fehler
	if biome_index >= 0 and biome_index < biome_names.size():
		var biome_name = biome_names[biome_index]
		current_biome = "\n Biome: %s" % [biome_name]
	else:
		current_biome = "\n Biome: Index Error (%d)" % biome_index
