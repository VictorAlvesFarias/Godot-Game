// Item no chao flutuando (sobe e desce). Feito no vertex shader, com um material so para todos os itens: nenhum script
// mexe na posicao do sprite a cada quadro. A fase vem da posicao do item, para os itens nao subirem juntos.
shader_type canvas_item;

// Altura do salto em pixels do mundo e velocidade (radianos por segundo).
uniform float amplitude = 4.0;
uniform float speed = 2.5;

void vertex() {
	float phase = MODEL_MATRIX[3].x * 0.1237 + MODEL_MATRIX[3].y * 0.0713;
	float scale_y = max(length(MODEL_MATRIX[1].xy), 0.0001);

	VERTEX.y -= abs(sin(TIME * speed + phase)) * amplitude / scale_y;
}
