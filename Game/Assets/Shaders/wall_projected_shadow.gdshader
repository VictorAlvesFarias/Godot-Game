shader_type canvas_item;
render_mode unshaded;
// Lighting is composed once in TerrainLightOverlay. Walls must not multiply
// shadow into their albedo before the ambient contribution is added.
void fragment() { }
