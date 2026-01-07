extends Node
const cleanup_rate = 5
var cleanup_timer = 0
var mouse_screen_pos = Vector2i.ZERO
var mouse_world_pos = Vector2i.ZERO


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	$WorldGenerator.SetupNoise(randi(), 0.1)

@onready var debug_label: RichTextLabel = $CanvasLayer/RichTextLabel
@onready var interaction_controller = $InteractionController
# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	$PhysicsHandler.UpdateChunksAround($Player.global_position)
	cleanup_timer += delta
	if cleanup_timer >= cleanup_rate:
		$PhysicsHandler.UnloadFarChunks($Player.global_position)
		cleanup_timer = 0

		
	_set_debug_text()	

func _set_debug_text():
	mouse_screen_pos = get_viewport().get_mouse_position()
	mouse_world_pos = interaction_controller.tile_pos
	var mouse_pos = "\n Mouse-Pos: %1f , %1f" % [mouse_screen_pos.x,mouse_screen_pos.y]
	var world_pos = "\n World-Pos: %d , %d" % [mouse_world_pos.x,mouse_world_pos.y]
	debug_label.text = mouse_pos + "\n" + world_pos
	


func _on_mouse_tile_changed(new_pos: Vector2i) -> void:
	pass # Replace with function body.
