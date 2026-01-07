extends Node2D

@onready var physics_handler = $"../PhysicsHandler"
@onready var player = $"../Player"

const tile_size = 16
var tile_pos := Vector2i.ZERO
var world_pos := Vector2.ZERO

signal mouse_tile_changed(new_pos: Vector2i)

func _ready():
	pass
	
func _process(delta: float) -> void:
	world_pos = get_global_mouse_position()
	var new_tile_pos = Vector2i(
		int(floor(world_pos.x / tile_size)),
		int(floor(world_pos.y / tile_size))
	)
	if new_tile_pos != tile_pos:
		tile_pos = new_tile_pos
		mouse_tile_changed.emit(tile_pos)



func _input(event: InputEvent) -> void:
	if event.is_action_pressed("left_click"):
		physics_handler.SetTile(tile_pos.x, tile_pos.y, 999) # make tile air
	if event.is_action_pressed("right_click"):
		physics_handler.SetTile(tile_pos.x, tile_pos.y, 1) # make tile earth
