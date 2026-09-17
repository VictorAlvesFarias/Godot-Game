shader_type canvas_item;
render_mode blend_mul, unshaded;

uniform sampler2D terrain_mask : filter_nearest, repeat_disable;

uniform vec4 light_uv_rect=vec4(0.0,0.0,1.0,1.0);
uniform float ambient_min=0.04;

void fragment() {
	// Coverage follows actual texture pixels; only the light grid is smoothed.
	float occupied = texture(terrain_mask, UV).r;
	vec3 light = max(texture(TEXTURE, light_uv_rect.xy+UV*light_uv_rect.zw).rgb,vec3(ambient_min));
	COLOR = vec4(mix(vec3(1.0), light, occupied), 1.0);
}
