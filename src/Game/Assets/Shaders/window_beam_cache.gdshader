shader_type canvas_item;
render_mode unshaded;
uniform sampler2D cells : filter_nearest, repeat_disable;
uniform vec2 grid_size;
uniform float angle;
uniform float aperture;
uniform float shadow_curve = 1.0;
uniform float ambient_curve = 1.0;

float trace_opening(vec2 p, vec2 direction)
{
    vec2 cell = floor(p);
    vec2 step_dir = sign(direction);
    vec2 delta = 1.0 / max(abs(direction), vec2(0.00001));
    vec2 edge = mix(p - cell, cell + vec2(1.0) - p, step(vec2(0.0), direction)) * delta;
    for (int i = 0; i < 64; i++)
    {
        float distance = min(edge.x, edge.y);
        if (distance > 24.0)
            return 0.0;
        if (edge.x < edge.y)
        {
            cell.x += step_dir.x;
            edge.x += delta.x;
        }
        else
        {
            cell.y += step_dir.y;
            edge.y += delta.y;
        }
        if (any(lessThan(cell, vec2(0.0))) || any(greaterThanEqual(cell, grid_size)))
            return 0.0;
        vec2 material = texelFetch(cells, ivec2(cell), 0).rg;
        if (material.r > 0.5)
            return 0.0;
        if (material.g < 0.5)
            return 1.0 - smoothstep(0.0, 24.0, distance);
    }
    return 0.0;
}
uniform bool local_mode = false;
uniform sampler2D local_sources : filter_nearest, repeat_disable;
uniform int local_count = 0;
float local_visibility(vec2 p, vec2 source)
{
    vec2 offset = source - p;
    float length_to_source = length(offset);
    if (length_to_source < 0.001 || length_to_source > 24.0)
        return 0.0;
    vec2 direction = offset / length_to_source;
    vec2 cell = floor(p), step_dir = sign(direction);
    vec2 delta = 1.0 / max(abs(direction), vec2(0.00001));
    vec2 edge = mix(p - cell, cell + vec2(1.0) - p, step(vec2(0.0), direction)) * delta;
    bool opening = false;
    for (int i = 0; i < 80; i++)
    {
        float distance = min(edge.x, edge.y);
        if (distance >= length_to_source)
            return opening ? 1.0 - smoothstep(0.0, 24.0, length_to_source) : 0.0;
        if (edge.x < edge.y)
        {
            cell.x += step_dir.x;
            edge.x += delta.x;
        }
        else
        {
            cell.y += step_dir.y;
            edge.y += delta.y;
        }
        if (any(lessThan(cell, vec2(0))) || any(greaterThanEqual(cell, grid_size)))
            return 0.0;
        vec2 material = texelFetch(cells, ivec2(cell), 0).rg;
        if (material.r > 0.5)
            return 0.0;
        if (material.g < 0.5)
            opening = true;
    }
    return 0.0;
}
void fragment()
{
    vec2 p = UV * grid_size;
    vec2 material = texture(cells, UV).rg;
    vec3 local_light = vec3(0.0);
    if (local_mode && material.r < 0.5 && material.g > 0.5)
    {
        for (int source_index = 0; source_index < local_count; source_index++)
        {
            vec2 source = texelFetch(local_sources, ivec2(0, source_index), 0).xy;
            vec3 color = texelFetch(local_sources, ivec2(1, source_index), 0).rgb;
            float visible = 0.0;

            for (int j = 0; j < 5; j++)
            {
                float a = float(j) * 1.256637;
                visible += local_visibility(p, source + vec2(cos(a), sin(a)) * 0.15);
            }
            local_light += color * visible / 5.0;
        }
    }
    float beam = 0.0;
    if (!local_mode && material.r < 0.5 && material.g > 0.5)
    {
        for (int i = 0; i < 15; i++)
        {
            float u = (float(i) + 0.5) / 15.0 * 2.0 - 1.0;
            float a = angle + aperture * u;
            beam += trace_opening(p, vec2(sin(a), -cos(a))) * sqrt(1.0 - u * u);
        }
        beam /= 11.832;
    }
    beam = clamp(beam, 0.0, 1.0);

    if (beam < 0.5)
        beam = 0.5 * pow(beam * 2.0, mix(8.0, 1.0, ambient_curve));
    else
        beam = 1.0 - 0.5 * pow((1.0 - beam) * 2.0, mix(8.0, 1.0, shadow_curve));
    COLOR = vec4(local_mode ? local_light : vec3(beam), 1.0);
}
