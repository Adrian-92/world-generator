extends ProgressBar


@export var character: CharacterBody2D


@onready var damagebar: ProgressBar = $damagebar
@onready var timer: Timer = $Timer
@onready var health: Node = character.find_children("*", "Health")[0]


func _ready():
	max_value = health.get_max_health()
	value = health.get_health()
	damagebar.max_value = health.max_health
	damagebar.value = health.health
	health.health_changed.connect(_on_health_changed)
	health.max_health_changed.connect(_on_max_health_changed)
	health.health_depleted.connect(_on_health_depleted)


func _on_health_changed(_diff: int):
	value = health.get_health()
	damagebar.value = health.get_health()

func _on_max_health_changed(_diff: int):
	max_value = health.get_max_health()
	damagebar.max_value = health.get_max_health()

func _on_health_depleted():
	value = 0
	damagebar.value = 0


func _on_timer_timeout() -> void:
	damagebar.value = health.get_health()
