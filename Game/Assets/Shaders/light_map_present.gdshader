shader_type canvas_item;
render_mode blend_mul, unshaded;
#include "res://Assets/Shaders/analytic_sun.gdshaderinc"
uniform sampler2D emission_data : filter_nearest, repeat_disable;
uniform float ambient_energy = 0.32;
uniform float sun_energy = 0.85;
uniform vec4 sky_color : source_color = vec4(0.72, 0.83, 1.0, 1.0);
uniform vec4 sun_color : source_color = vec4(1.0, 0.95, 0.84, 1.0);
uniform bool show_raw = false;
uniform bool geometry_ready = false;

void fragment() {
    vec2 p = UV * map_size - 0.5;
    ivec2 base = ivec2(floor(p));
    vec2 f = fract(p);
    ivec2 last = ivec2(map_size) - ivec2(1);
    ivec2 center = clamp(ivec2(floor(UV * map_size)), ivec2(0), last);
    float material = texelFetch(light_data, center, 0).b;
    float sky = 0.0;
    vec3 emission = vec3(0.0);
    float weight_sum = 0.0;
    for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) {
        ivec2 c = clamp(base + ivec2(x, y), ivec2(0), last);
        vec3 v = texelFetch(light_data, c, 0).rgb;
        float w = (x == 0 ? 1.0 - f.x : f.x) * (y == 0 ? 1.0 - f.y : f.y);
        w *= abs(v.b - material) < 0.01 ? 1.0 : 0.0;
        sky += v.r * w;
        emission += texelFetch(emission_data, c, 0).rgb * w;
        weight_sum += w;
    }
    sky /= max(weight_sum, 0.0001);
    emission /= max(weight_sum, 0.0001);
    // Evaluate coverage at the actual display pixel, not a magnified cached texel.
    vec2 world_pixel = UV * map_size / 2.0;
    float sun = 0.0;
    if (geometry_ready) sun = analytic_sun(world_pixel, fwidth(world_pixel));
    vec3 energy = sky_color.rgb * sky * ambient_energy + sun_color.rgb * sun * sun_energy + emission;
    COLOR = vec4(show_raw ? vec3(sky, sun, material) : clamp(energy, vec3(0.0), vec3(1.0)), 1.0);
}
