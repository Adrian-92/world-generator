extends ProgressBar

@onready var endurance_lostbar: ProgressBar = $enduranceLostbar
@onready var timer: Timer = $Timer

var endurance = 0 : set = _set_endurance

func _set_endurance(newEndurance):
	var prevEndurance = endurance
	endurance = min(max_value,newEndurance)
	value = endurance
	if endurance < prevEndurance:
		timer.start()
	else:
		endurance_lostbar.value = endurance

func init_endurance(_endurance):
	endurance = _endurance
	max_value = endurance
	value = endurance
	endurance_lostbar.max_value = endurance
	endurance_lostbar.value = endurance


func _on_timer_timeout():
	endurance_lostbar.value = endurance
