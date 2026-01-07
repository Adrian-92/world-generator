extends CharacterBody2D

@export var SPEED = 70.0

const SPRINT_SPEED = 140
const MAX_SPRINT_TIME = 5.0	
const SPRINT_COOLDOWN = 10.0

const DASH_FORCE = 500.0
const JUMP_VELOCITY = -500.0
const COYOTE_TIME = 0.3
const DASH_DURATION = 0.2

#jump variables
var coyote_time_timer = 0.0
var doubleJump = false
var isOnGround = false

var gravity = ProjectSettings.get_setting("physics/2d/default_gravity")
@onready var animated_sprite: AnimatedSprite2D = $AnimatedSprite2D


#movement and dash
var is_dashing = false
var dash_timer = 0.0
var moveDirection = 1
var sprint_timer = MAX_SPRINT_TIME
var is_sprint_on_cooldown = false
var cooldown_timer = 0.0

var endurance = 0




func die():
	print("Du bist gestorben!")
	get_tree().reload_current_scene()


func _ready():
	pass
	
func _process(_delta: float) -> void:
	pass

func _physics_process(delta: float) -> void:
		# Get the input direction for normal movement
	var direction := Input.get_axis("move_left", "move_right")
	
	#check direction of charakters face
	if direction != 0:
		moveDirection = sign(direction)
	# Add gravity unless dashing
	if not is_on_floor() and not is_dashing:
		velocity.y += gravity * delta

	# Sprint logic
	var current_speed = SPEED
	if Input.is_action_pressed("sprint") and (Input.is_action_pressed("move_left") or Input.is_action_pressed("move_right")):
		if sprint_timer > 0:
			current_speed = SPRINT_SPEED
			sprint_timer -= delta
	else:
		if sprint_timer < MAX_SPRINT_TIME and not Input.is_action_pressed("sprint"):
			sprint_timer += delta

	# Reset coyote time and double jump
	if is_on_floor():
		coyote_time_timer = 0.0
		doubleJump = true
		isOnGround = true
	else:
		coyote_time_timer += delta
		isOnGround = false

	# Handle jump
	if Input.is_action_just_pressed("jump"):
		if isOnGround:
			velocity.y = JUMP_VELOCITY
		elif coyote_time_timer <= COYOTE_TIME:
			velocity.y = JUMP_VELOCITY
			coyote_time_timer = COYOTE_TIME + 1  # Prevent further coyote jumps
		elif doubleJump:
			velocity.y = JUMP_VELOCITY
			doubleJump = false

	# Dash logic
	if Input.is_action_just_pressed("dash") and not is_dashing:
		is_dashing = true
		dash_timer = DASH_DURATION
		velocity.x = DASH_FORCE * moveDirection
		
	if is_dashing:
		dash_timer -= delta
		if dash_timer <= 0:
			is_dashing = false



	# Flip sprite based on movement direction
	if direction > 0:
		animated_sprite.flip_h = false
	elif direction < 0:
		animated_sprite.flip_h = true

	# Apply normal movement only if not dashing
	if not is_dashing:
		if direction:
			velocity.x = direction * current_speed
		else:
			velocity.x = move_toward(velocity.x, 0, SPEED)

	# Play animation
	if is_on_floor():
		if direction == 0:
			animated_sprite.play("idle")
		else:
			animated_sprite.play("run")
	else:
		animated_sprite.play("jump")

	move_and_slide()
