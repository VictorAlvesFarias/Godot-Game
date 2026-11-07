extends SceneTree

func _init():
	var player_scene = load("res://Scenes/World/Characters/Player.tscn")
	var player = player_scene.instantiate()
	root.add_child(player)

	await process_frame

	var health_label = player.HealthLabel
	print("BEFORE any _Process call:")
	print("  Position: ", health_label.position)
	print("  Size: ", health_label.size)
	print("  OffsetTop/Bottom: ", health_label.offset_top, " / ", health_label.offset_bottom)

	# Simula ser dono (pra pular o "IsOwner() -> esconde" e realmente rodar
	# a logica que mexe em Position).
	player.PeerId = 999999

	await process_frame
	await process_frame
	await process_frame

	print("AFTER a few _Process (owner=false, deve ter rodado UpdateNameplate):")
	print("  Position: ", health_label.position)
	print("  Size: ", health_label.size)
	print("  OffsetTop/Bottom: ", health_label.offset_top, " / ", health_label.offset_bottom)
	print("  Visible: ", health_label.visible)

	quit()
