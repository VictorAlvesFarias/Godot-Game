extends SceneTree

func _initialize():
	var tile_set: TileSet = load("res://Assets/Textures/Tiles/TileSet.tres")
	var atlas_source: TileSetAtlasSource = tile_set.get_source(7)

	var bit_order = [
		TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
		TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
		TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
		TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
		TileSet.CELL_NEIGHBOR_LEFT_SIDE,
		TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER,
		TileSet.CELL_NEIGHBOR_TOP_SIDE,
		TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
	]
	var offsets = [
		Vector2i(1,0), Vector2i(1,1), Vector2i(0,1), Vector2i(-1,1),
		Vector2i(-1,0), Vector2i(-1,-1), Vector2i(0,-1), Vector2i(1,-1),
	]

	# collect all authored (terrain_set=0, terrain=0) signatures on this atlas source
	var authored := {}
	var count = atlas_source.get_tiles_count()
	for i in count:
		var atlas = atlas_source.get_tile_id(i)
		var alt_count = atlas_source.get_alternative_tiles_count(atlas)
		for a in alt_count:
			var alt = atlas_source.get_alternative_tile_id(atlas, a)
			var td = atlas_source.get_tile_data(atlas, alt)
			if td.terrain_set != 0 or td.terrain != 0:
				continue
			var sig := 0
			for bi in range(8):
				var v = td.get_terrain_peering_bit(bit_order[bi])
				if v == 0:
					sig |= (1 << bi)
			if not authored.has(sig):
				authored[sig] = []
			authored[sig].append(atlas)

	print("total authored (terrain 0) tiles: ", count, " unique signatures: ", authored.size())

	# now brute force test all 256 neighbor patterns via actual SetCellsTerrainConnect
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	root.add_child(layer)

	var exact_matches := 0
	var mismatches := []

	for pattern in range(256):
		# clear a fresh area
		for dx in range(-2, 3):
			for dy in range(-2, 3):
				layer.set_cell(Vector2i(dx, dy), -1)

		var center = Vector2i(0, 0)
		var cells: Array[Vector2i] = [center]
		for bi in range(8):
			if (pattern & (1 << bi)) != 0:
				var pos = center + offsets[bi]
				layer.set_cell(pos, 7, Vector2i(1,1))
				cells.append(pos)

		layer.set_cells_terrain_connect(cells, 0, 0, false)

		var got_atlas = layer.get_cell_atlas_coords(center)
		var got_alt = layer.get_cell_alternative_tile(center)
		var td = atlas_source.get_tile_data(got_atlas, got_alt)

		var got_sig := 0
		for bi in range(8):
			var v = td.get_terrain_peering_bit(bit_order[bi])
			if v == 0:
				got_sig |= (1 << bi)

		if got_sig == pattern:
			exact_matches += 1
		else:
			mismatches.append([pattern, got_sig, got_atlas])

	print("exact matches: ", exact_matches, " / 256")
	print("mismatches: ", mismatches.size())
	for m in mismatches:
		print("  requested=", "%08b" % m[0], " got=", "%08b" % m[1], " chose atlas=", m[2])

	quit()
