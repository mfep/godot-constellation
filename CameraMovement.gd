extends Camera2D

var dragging = false

func _input(event):
	if event is InputEventMouseButton:
		if event.get_button_index() == BUTTON_MIDDLE:
			if event.is_pressed():
				dragging = true
			else:
				dragging = false
		elif event.get_button_index() == BUTTON_WHEEL_UP:
			zoom *= 0.95
		elif event.get_button_index() == BUTTON_WHEEL_DOWN:
			zoom *= 1.05
	elif event is InputEventMouseMotion and dragging:
		translate(-1.0 * zoom.x * event.get_relative())
