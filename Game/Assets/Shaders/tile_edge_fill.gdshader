shader_type canvas_item;

// So deixa preencher pixels transparentes do lado do tile que aponta pra essa direcao (a
// metade do tile voltada pro vizinho de tileset diferente). (1,0)=direita, (-1,0)=esquerda,
// (0,-1)=cima, (0,1)=baixo.
uniform vec2 edge_direction = vec2(1.0, 0.0);
// Alcance da busca por um pixel vizinho colorido, em texels.
uniform int fill_radius : hint_range(1, 16) = 6;
// Distancia maxima de cor (RGB, 0-1) pra aceitar preencher com aquele pixel - se o vizinho
// encontrado for muito diferente da cor de maior destaque do tile, nao preenche.
uniform float color_tolerance : hint_range(0.0, 1.0) = 0.18;
// Tamanho do tile em pixels de MUNDO (nao da textura) - usado pra achar a posicao local dentro
// da celula atual via posicao de mundo, sem depender de como o atlas empacota as texturas
// (margem/separacao entre tiles na folha quebrava o calculo baseado so em UV).
uniform float tile_pixel_size = 32.0;
// DEBUG temporario: 0 = normal, 1 = mostra a posicao local dentro da celula como cor
// (vermelho=x, verde=y, deve repetir em quadradinhos identicos de tile em tile), 2 = mostra o
// lado permitido (verde=preenche, vermelho=nao).
uniform int debug_mode : hint_range(0, 2) = 0;

varying vec2 world_pos;

void vertex()
{
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

vec4 sample_tile(sampler2D tex, vec2 uv)
{
	return texture(tex, clamp(uv, vec2(0.0), vec2(1.0)));
}

// Aproxima a cor de maior destaque do tile amostrando uma grade 5x5 ao redor do pixel atual (em
// texels) e tirando a media dos pixels opacos - nao e um histograma exato, mas evita puxar cor
// de detalhes pequenos (flor, pedra) pro preenchimento.
vec3 estimate_dominant_color(sampler2D tex, vec2 uv, vec2 texel)
{
	vec3 sum = vec3(0.0);
	float count = 0.0;

	for (int ix = -8; ix <= 8; ix += 4)
	{
		for (int iy = -8; iy <= 8; iy += 4)
		{
			vec4 sample_color = sample_tile(tex, uv + vec2(float(ix), float(iy)) * texel);

			if (sample_color.a > 0.5)
			{
				sum += sample_color.rgb;
				count += 1.0;
			}
		}
	}

	if (count <= 0.0)
	{
		return vec3(0.0);
	}

	return sum / count;
}

void fragment()
{
	vec4 tex_color = texture(TEXTURE, UV);

	// Posicao local (0..1) dentro da CELULA atual, calculada a partir da posicao de mundo do
	// pixel (independente de onde/como o tile esta empacotado na textura de origem).
	vec2 local_pixel = mod(world_pos, vec2(tile_pixel_size));
	vec2 local_uv = local_pixel / vec2(tile_pixel_size);
	float side = dot(local_uv - vec2(0.5), edge_direction);

	if (debug_mode == 1)
	{
		COLOR = vec4(local_uv, 0.0, 1.0);
	}
	else if (debug_mode == 2)
	{
		COLOR = side > 0.0 ? vec4(0.0, 1.0, 0.0, 1.0) : vec4(1.0, 0.0, 0.0, 1.0);
	}
	else
	{
		if (tex_color.a < 0.5 && side > 0.0)
		{
			vec2 texel = TEXTURE_PIXEL_SIZE;
			vec3 dominant = estimate_dominant_color(TEXTURE, UV, texel);
			vec4 best_color = tex_color;
			float best_distance = 1000.0;
			bool found = false;

			for (int dx = -fill_radius; dx <= fill_radius; dx++)
			{
				for (int dy = -fill_radius; dy <= fill_radius; dy++)
				{
					if (dx == 0 && dy == 0)
					{
						continue;
					}

					vec4 sample_color = sample_tile(TEXTURE, UV + vec2(float(dx), float(dy)) * texel);

					if (sample_color.a <= 0.5)
					{
						continue;
					}

					if (distance(sample_color.rgb, dominant) > color_tolerance)
					{
						continue;
					}

					float dist = float(dx * dx + dy * dy);

					if (dist < best_distance)
					{
						best_distance = dist;
						best_color = sample_color;
						found = true;
					}
				}
			}

			if (found)
			{
				tex_color = vec4(dominant, 1.0);
			}
		}

		COLOR = tex_color;
	}
}
