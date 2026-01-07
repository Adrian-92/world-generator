extends Node2D


@onready var physics_handler = $"../PhysicsHandler"
@onready var world_generator = $"../WorldGenerator"
@onready var player = $"../Player"

const tile_size = 16
var mouse_tile_pos := Vector2i.ZERO  # tile in the game world 
signal mouse_tile_changed(new_pos: Vector2i)
signal mouse_screen_pos_changed(new_pos: Vector2i)
func _ready():
	pass
	
func _process(_delta: float) -> void:
	get_mouse_pos()


func get_mouse_pos():
	var mouse_world_pos = get_global_mouse_position()
	var mouse_screen_pos = get_viewport().get_mouse_position()
	var new_tile_pos = Vector2i(
		int(floor(mouse_world_pos.x / tile_size)),
		int(floor(mouse_world_pos.y / tile_size))
	)
	if new_tile_pos != mouse_tile_pos:
		mouse_tile_pos = new_tile_pos
		mouse_tile_changed.emit(mouse_tile_pos)
		mouse_screen_pos_changed.emit(mouse_screen_pos)
	
func _input(event: InputEvent) -> void:
	if event.is_action_pressed("left_click"):
		physics_handler.SetTile(mouse_tile_pos.x, mouse_tile_pos.y, 999) # make tile air
	if event.is_action_pressed("right_click"):
		physics_handler.SetTile(mouse_tile_pos.x, mouse_tile_pos.y, 1) # make tile earth
