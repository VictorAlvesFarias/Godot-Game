shader_type canvas_item;
render_mode blend_mul, unshaded;

uniform sampler2D terrain_mask : filter_nearest, repeat_disable;

void fragment() {
	// Coverage follows actual texture pixels; only the light grid is smoothed.
	float occupied = texture(terrain_mask, UV).r;
	vec3 light = texture(TEXTURE, UV).rgb;
	COLOR = vec4(mix(vec3(1.0), light, occupied), 1.0);
}
