shader_type canvas_item;

// Preenche pixel transparente do tile usando a cor do pixel vizinho (dentro do mesmo tile) mais
// proximo que tiver cor - fecha pequenos buracos que aparecem na costura quando dois tilesets
// diferentes (formas de borda diferentes) nao encaixam perfeitamente um no outro.
uniform int fill_radius : hint_range(1, 16) = 6;

void fragment()
{
	vec4 tex_color = texture(TEXTURE, UV);

	if (tex_color.a < 0.5)
	{
		vec2 texel = TEXTURE_PIXEL_SIZE;
		vec4 best_color = tex_color;
		float best_alpha = 0.0;
		float best_distance = 1000.0;

		for (int dx = -fill_radius; dx <= fill_radius; dx++)
		{
			for (int dy = -fill_radius; dy <= fill_radius; dy++)
			{
				if (dx == 0 && dy == 0)
				{
					continue;
				}

				vec4 sample_color = texture(TEXTURE, UV + vec2(float(dx), float(dy)) * texel);

				if (sample_color.a <= 0.5)
				{
					continue;
				}

				float dist = float(dx * dx + dy * dy);

				if (dist < best_distance)
				{
					best_distance = dist;
					best_alpha = sample_color.a;
					best_color = sample_color;
				}
			}
		}

		if (best_alpha > 0.0)
		{
			tex_color = vec4(best_color.rgb, 1.0);
		}
	}

	COLOR = tex_color;
}
