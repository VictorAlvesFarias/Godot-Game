shader_type canvas_item;
render_mode blend_mul, unshaded;
#include "res://Assets/Shaders/world_light_sampling.gdshaderinc"
void fragment() {
    vec4 light = sample_world_light(UV,fwidth(UV*map_size*0.5));
    COLOR = vec4(mix(vec3(1.0),light.rgb,light.a),1.0);
}
