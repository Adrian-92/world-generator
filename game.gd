extends Node
const cleanup_rate = 5
var cleanup_timer = 0

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	$WorldGenerator.SetupNoise(69, 0.05)

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
	pass
