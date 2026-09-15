shader_type canvas_item;
render_mode unshaded;
#include "res://Assets/Shaders/projected_shadow.gdshaderinc"
uniform float shadow_strength=0.65;
uniform bool geometry_ready=false;
uniform vec2 map_world_origin;
uniform vec2 map_world_axis_x;
uniform vec2 map_world_axis_y;
varying vec2 world_position;
void vertex() { world_position=(MODEL_MATRIX*vec4(VERTEX,0.0,1.0)).xy; }
void fragment() {
    vec2 offset=world_position-map_world_origin;
    vec2 p=vec2(dot(offset,map_world_axis_x),dot(offset,map_world_axis_y));
    float shadow=0.0;
    if(geometry_ready && all(greaterThanEqual(p,vec2(0.0))) && all(lessThan(p,map_size*0.5)))
        shadow=projected_shadow(p,fwidth(p));
    // The wall's own fragments/alpha are the receiver mask. Foreground and
    // entity fragments never run this material, even at the same screen pixel.
    COLOR.rgb*=1.0-clamp(shadow_strength,0.0,1.0)*shadow;
}
