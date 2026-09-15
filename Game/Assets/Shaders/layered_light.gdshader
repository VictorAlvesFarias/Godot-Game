shader_type canvas_item;
render_mode blend_mul, unshaded;
#include "res://Assets/Shaders/projected_shadow.gdshaderinc"
uniform sampler2D light_data : filter_nearest, repeat_disable;
uniform sampler2D emission_data : filter_nearest, repeat_disable;
uniform float shadow_strength=0.65;
uniform bool geometry_ready=false;
void fragment() {
    vec2 pixel=UV*map_size-0.5;
    ivec2 base=ivec2(floor(pixel)),last=ivec2(map_size)-ivec2(1);
    vec2 f=fract(pixel);
    float material=texelFetch(light_data,clamp(ivec2(floor(UV*map_size)),ivec2(0),last),0).b;
    vec3 emission=vec3(0.0);
    float sky=0.0,total=0.0;
    for(int y=0;y<2;y++) for(int x=0;x<2;x++) {
        ivec2 c=clamp(base+ivec2(x,y),ivec2(0),last);
        vec3 value=texelFetch(light_data,c,0).rgb;
        float w=(x==0?1.0-f.x:f.x)*(y==0?1.0-f.y:f.y);
        if(abs(value.b-material)>0.01) continue;
        total+=w; sky+=value.r*w;
        emission+=texelFetch(emission_data,c,0).rgb*w;
    }
    sky/=max(total,0.000001);emission/=max(total,0.000001);
    vec3 energy=vec3(sky)+emission;
    COLOR=vec4(clamp(energy,vec3(0.0),vec3(1.0)),1.0);
}
