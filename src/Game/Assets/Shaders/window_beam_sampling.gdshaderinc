uniform vec4 window_sun_color : source_color = vec4(1.0);
uniform sampler2D window_beam : filter_linear, repeat_disable;
uniform bool window_beam_ready = false;
uniform vec2 beam_origin;
uniform vec2 beam_axis_x;
uniform vec2 beam_axis_y;
uniform vec2 beam_size;

uniform sampler2D window_cells : filter_nearest, repeat_disable;
bool window_field_contains(vec2 world)
{
    if (!window_beam_ready)
        return false;
    vec2 d = world - beam_origin;
    vec2 uv = vec2(dot(d, beam_axis_x), dot(d, beam_axis_y)) / beam_size;
    return all(greaterThanEqual(uv, vec2(0.0))) && all(lessThan(uv, vec2(1.0)));
}

bool window_wall_cell(vec2 cell)
{
    if (any(lessThan(cell, vec2(0.0))) || any(greaterThanEqual(cell, beam_size)))
        return false;
    vec2 material = texelFetch(window_cells, ivec2(cell), 0).rg;
    return material.r < 0.5 && material.g > 0.5;
}

// Os feixes so existem nas celulas de wall (zero no ar), mas o tile 24x24 transborda
// 4px a celula de 16x16. O pixel de wall fora da propria celula pertence a celula de
// wall vizinha mais proxima: a amostra vai para dentro dela, sem o meio texel (1/8 de
// celula no cache 4x) que o filtro linear misturaria com o zero do ar.
vec2 window_receiver_uv(vec2 world)
{
    vec2 d = world - beam_origin;
    vec2 p = vec2(dot(d, beam_axis_x), dot(d, beam_axis_y));
    vec2 cell = floor(p);
    vec2 owner = cell;

    if (!window_wall_cell(cell))
    {
        vec2 f = p - cell;
        vec2 side = vec2(f.x < 0.5 ? -1.0 : 1.0, f.y < 0.5 ? -1.0 : 1.0);
        vec2 gap = min(f, vec2(1.0) - f);
        bool horizontal = window_wall_cell(cell + vec2(side.x, 0.0));
        bool vertical = window_wall_cell(cell + vec2(0.0, side.y));

        if (horizontal && (!vertical || gap.x <= gap.y))
            owner = cell + vec2(side.x, 0.0);
        else if (vertical)
            owner = cell + vec2(0.0, side.y);
        else if (window_wall_cell(cell + side))
            owner = cell + side;
    }

    vec2 lo = owner + vec2(0.125);
    vec2 hi = owner + vec2(0.875);

    if (window_wall_cell(owner - vec2(1.0, 0.0)))
        lo.x = owner.x;
    if (window_wall_cell(owner + vec2(1.0, 0.0)))
        hi.x = owner.x + 1.0;
    if (window_wall_cell(owner - vec2(0.0, 1.0)))
        lo.y = owner.y;
    if (window_wall_cell(owner + vec2(0.0, 1.0)))
        hi.y = owner.y + 1.0;

    return clamp(p, lo, hi) / beam_size;
}

float window_light(vec2 uv)
{
    return texture(window_beam, uv).r;
}

uniform sampler2D sky_access : filter_linear, repeat_disable;
uniform bool include_skylight = true;
float environment_sky(vec2 world)
{
    if (!window_beam_ready || !include_skylight)
        return 0.0;
    vec2 d = world - beam_origin;
    vec2 uv = vec2(dot(d, beam_axis_x), dot(d, beam_axis_y)) / beam_size;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThanEqual(uv, vec2(1.0))))
        return 0.0;

    vec4 cell = texture(window_cells, uv);
    if (cell.r < 0.5 && cell.b == 0.0)
        return 0.0;
    return texture(sky_access, uv).b;
}

uniform sampler2D local_window_beam : filter_linear, repeat_disable;
vec3 local_window_light(vec2 uv)
{
    return texture(local_window_beam, uv).rgb;
}
