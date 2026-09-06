shader_type canvas_item;
render_mode blend_mul, unshaded;

uniform sampler2D dados_mapa : filter_nearest, repeat_disable;

uniform vec2 grade = vec2(1.0);
uniform float piso_ar = 0.0;
uniform float piso_materia = 0.0;
uniform bool mostrar_mapa_cru = false;

void fragment() {
    vec3 luz = texture(TEXTURE, UV).rgb;

    if (mostrar_mapa_cru) {
        COLOR = vec4(luz, 1.0);
    } else {
        // O piso da sombra e escolhido pela geometria real do pixel, nao pela textura de luz. Se
        // ele fosse gravado no passe, a interpolacao levaria o piso da materia para dentro do ar em
        // sombra, e a juncao da sombra com o bloco ficava clara.
        ivec2 limite = ivec2(grade) - ivec2(1);
        ivec2 c = clamp(ivec2(floor(UV * grade)), ivec2(0), limite);
        bool materia = texelFetch(dados_mapa, c, 0).r > 0.002;

        COLOR = vec4(max(vec3(materia ? piso_materia : piso_ar), luz), 1.0);
    }
}
