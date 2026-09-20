shader_type canvas_item;
render_mode blend_mix, unshaded;
#include "res://Assets/Shaders/projected_shadow.gdshaderinc"
uniform bool geometry_ready = false;
void fragment()
{
    vec2 p = UV * map_size * 0.5;
    float covered = geometry_ready ? projected_shadow(p, fwidth(p)) : 0.0;

    COLOR = vec4(vec3(1.0 - covered), 1.0);
}
