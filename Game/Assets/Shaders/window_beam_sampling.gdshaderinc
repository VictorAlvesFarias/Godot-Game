uniform vec4 window_sun_color : source_color = vec4(1.0);
uniform sampler2D window_beam : filter_linear, repeat_disable;
uniform bool window_beam_ready=false;
uniform vec2 beam_origin;
uniform vec2 beam_axis_x;
uniform vec2 beam_axis_y;
uniform vec2 beam_size;
float window_light(vec2 world) {
    if(!window_beam_ready) return 0.0;
    vec2 d=world-beam_origin;
    vec2 uv=vec2(dot(d,beam_axis_x),dot(d,beam_axis_y))/beam_size;
    if(any(lessThan(uv,vec2(0.0))) || any(greaterThanEqual(uv,vec2(1.0)))) return 0.0;
    return texture(window_beam,uv).r;
}

uniform sampler2D window_cells : filter_nearest, repeat_disable;
bool window_wall_receiver(vec2 world) {
    if(!window_beam_ready) return false;
    vec2 d=world-beam_origin;
    vec2 uv=vec2(dot(d,beam_axis_x),dot(d,beam_axis_y))/beam_size;
    if(any(lessThan(uv,vec2(0.0))) || any(greaterThanEqual(uv,vec2(1.0)))) return false;
    vec2 cell=texture(window_cells,uv).rg;
    return cell.r<0.5 && cell.g>0.5;
}

uniform sampler2D sky_access : filter_linear, repeat_disable;
uniform bool include_skylight=true;
float environment_sky(vec2 world) {
    if(!window_beam_ready || !include_skylight) return 0.0;
    vec2 d=world-beam_origin;
    vec2 uv=vec2(dot(d,beam_axis_x),dot(d,beam_axis_y))/beam_size;
    if(any(lessThan(uv,vec2(0.0))) || any(greaterThanEqual(uv,vec2(1.0)))) return 0.0;
    // A closed air cell cannot borrow light through bilinear filtering from
    // a neighboring illuminated solid. Lit compartments retain smooth sampling.
    vec4 cell=texture(window_cells,uv);
    if(cell.r<0.5 && cell.b==0.0) return 0.0;
    return texture(sky_access,uv).b;
}
