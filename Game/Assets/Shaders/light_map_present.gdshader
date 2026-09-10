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

// Finite local projection from depth openings. One grid traversal, with a
// mip-filtered aperture footprint for the angular size of the existing sun.
uniform sampler2D depth_beam_data : filter_linear_mipmap, repeat_disable;
uniform bool depth_beam_enabled = false;
float window_beam(vec2 p) {
    vec2 size = vec2(textureSize(depth_beam_data, 0));
    p += vec2(24.0);
    ivec2 cell = ivec2(floor(p));
    vec3 receiver = texelFetch(depth_beam_data, cell, 0).rgb;
    if (receiver.b < 0.5 || receiver.g > 0.5) return 0.0;
    vec2 ray = vec2(sin(sun_angle), -cos(sun_angle));
    if (ray.y >= -0.001) return 0.0;
    vec2 stride = 1.0 / max(abs(ray), vec2(0.000001));
    ivec2 direction = ivec2(sign(ray));
    vec2 boundary = vec2(ray.x >= 0.0 ? float(cell.x+1)-p.x : p.x-float(cell.x),
                         ray.y >= 0.0 ? float(cell.y+1)-p.y : p.y-float(cell.y)) * stride;
    float distance = 0.0;
    float spread = tan(radians(8.0) * pow(1.0-clamp(penumbra,0.0,1.0),2.0));
    float beam = 0.0;
    for (int step_index = 0; step_index < 64; step_index++) {
        distance = min(boundary.x,boundary.y);
        if (distance >= 24.0) break;
        if (boundary.x <= distance + 0.000001) { cell.x += direction.x; boundary.x += stride.x; }
        if (boundary.y <= distance + 0.000001) { cell.y += direction.y; boundary.y += stride.y; }
        if (any(lessThan(cell,ivec2(0))) || any(greaterThanEqual(cell,ivec2(size)))) break;
        if (texelFetch(depth_beam_data,cell,0).g > 0.5) break;
        vec2 point = p + ray * (distance + 0.01);
        float footprint = max(1.0, 2.0 * distance * spread);
        float aperture = textureLod(depth_beam_data, point / size, log2(footprint)).r;
        float fade = exp(-distance / 12.0) * (1.0-smoothstep(20.0,24.0,distance));
        beam = max(beam,aperture * fade);
        if (beam > 0.99) break;
    }
    return beam;
}

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
    if (geometry_ready) {
        sun = analytic_sun(world_pixel, fwidth(world_pixel));
        if (depth_beam_enabled) sun = max(sun,window_beam(world_pixel));
    }
    // Reserve the display range for unoccluded daylight BEFORE applying attenuation.
    // Clipping ambient + sun afterwards erases the first portion of every gradient.
    vec3 daylight = sky_color.rgb * ambient_energy + sun_color.rgb * sun_energy;
    float daylight_scale = max(1.0, max(daylight.r, max(daylight.g, daylight.b)));
    vec3 energy = (sky_color.rgb * sky * ambient_energy + sun_color.rgb * sun * sun_energy) / daylight_scale + emission;
    COLOR = vec4(show_raw ? vec3(sky, sun, material) : clamp(energy, vec3(0.0), vec3(1.0)), 1.0);
}
