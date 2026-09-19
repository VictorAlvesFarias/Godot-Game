shader_type canvas_item;
render_mode blend_disabled, unshaded;
uniform sampler2D scene_color : hint_screen_texture, filter_nearest, repeat_disable;

#include "res://Assets/Shaders/window_beam_sampling.gdshaderinc"
#include "res://Assets/Shaders/projected_shadow.gdshaderinc"
uniform bool geometry_ready=false;
uniform float shadow_strength=0.65;
uniform float ambient_light_influence=0.75;
varying vec2 beam_world_position;
void vertex() { beam_world_position=(MODEL_MATRIX*vec4(VERTEX,0.0,1.0)).xy; }
uniform sampler2D terrain_mask : filter_nearest, repeat_disable;

uniform vec4 light_uv_rect=vec4(0.0,0.0,1.0,1.0);
uniform float ambient_min=0.04;

void fragment() {
	// Coverage follows actual texture pixels; only the light grid is smoothed.
	float occupied = texture(terrain_mask, UV).r;
	vec3 emission=texture(TEXTURE,light_uv_rect.xy+UV*light_uv_rect.zw).rgb;
    vec3 light=window_sun_color.rgb*environment_sky(beam_world_position);
	float beam=include_skylight?window_light(beam_world_position):0.0;
    if(window_wall_receiver(beam_world_position)) {
        vec2 offset=beam_world_position-beam_origin;
        vec2 p=vec2(dot(offset,beam_axis_x),dot(offset,beam_axis_y));
        float shadow=geometry_ready?projected_shadow(p,fwidth(p)):0.0;
        // Shadow strength remains geometric opacity. Available light fills that
        // shadow locally; this control cannot create light in an enclosed room.
        vec3 available=clamp(light,vec3(0.0),vec3(1.0));
        float opacity=clamp(shadow_strength*shadow,0.0,1.0);
        vec3 fill=available*clamp(ambient_light_influence,0.0,1.0);
        light=available*(vec3(1.0)-opacity*(vec3(1.0)-fill));
        // The window field already traces foreground obstacles up to the opening.
        // Its transmitted light must not be occluded again by the 2D projection
        // (which sees the roof beyond that opening). Keep its continuous profile.
        light=max(light,window_sun_color.rgb*beam);
    }
	// Display floor is not incoming light and must never fill a shadow.
    // Independent sources add irradiance, including on sunlit surfaces.
    // Do not clamp to one before multiplying the surface albedo.
    light=max(light+max(emission,local_window_light(beam_world_position)),vec3(ambient_min));
	// Explicitly shade the scene color: fixed-function multiply clamps gains
    // above one and would hide local emitters on sunlit surfaces.
    vec4 surface=texture(scene_color,SCREEN_UV);
	COLOR = vec4(surface.rgb*mix(vec3(1.0),light,occupied),surface.a);
}
