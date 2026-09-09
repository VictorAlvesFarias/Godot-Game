shader_type canvas_item;
render_mode blend_mix, unshaded;
#include "res://Assets/Shaders/analytic_sun.gdshaderinc"

void fragment() {
    vec2 p = UV * map_size / 2.0;
    COLOR = vec4(vec3(analytic_sun(p, fwidth(p))), 1.0);
}
