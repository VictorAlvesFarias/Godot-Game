shader_type canvas_item;
render_mode blend_add, unshaded;
#include "res://Assets/Shaders/world_light_sampling.gdshaderinc"
void fragment() {
    vec2 p = UV*map_size*0.5;
    float beam = 0.0;
    if (geometry_ready && depth_beam_enabled && volume_density > 0.0)
        beam = window_beam(p);
    COLOR = vec4(sun_color.rgb*sun_energy,clamp(beam*volume_density,0.0,1.0));
}
