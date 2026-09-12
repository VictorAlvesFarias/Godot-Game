shader_type canvas_item;
render_mode unshaded;
#include "res://Assets/Shaders/world_light_sampling.gdshaderinc"
uniform float shadow_entity = 1.0;
uniform vec2 map_world_origin;
uniform vec2 map_world_axis_x;
uniform vec2 map_world_axis_y;
varying vec2 world_position;
void vertex() { world_position = (MODEL_MATRIX * vec4(VERTEX,0.0,1.0)).xy; }
void fragment() {
    vec2 delta = world_position-map_world_origin;
    vec2 tile = vec2(dot(delta,map_world_axis_x),dot(delta,map_world_axis_y));
    vec2 uv = tile/(map_size*0.5);
    vec2 safe = clamp(uv,vec2(0.0),vec2(0.99999));
    // Entidade tem peso de sombra proprio, nao o do alvo que estiver atras dela. O overlay ja vai
    // multiplicar este sprite pela composicao da celula; dividir por ela deixa sobrar a da entidade.
    vec3 cell = compose_world_light(safe,fwidth(tile),receiver_shadow_weight(safe));
    vec3 mine = compose_world_light(safe,fwidth(tile),clamp(shadow_entity,0.0,1.0));
    COLOR.rgb *= mine/max(cell,vec3(0.001));
}
