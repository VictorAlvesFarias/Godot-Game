"""
Prototipo standalone (fora do Godot) pra iterar visualmente nos parametros do ruido de
biomas antes de portar pro C# (BiomeResolver.cs). Gera uma imagem onde cada bloco de
TILE_PIXELS x TILE_PIXELS representa 1 tile do jogo: vermelho = Olive, verde = Lime.

Uso:
    python biome_noise_preview.py

Mexa nos parametros na secao CONFIG abaixo e rode de novo - a imagem sai em
biome_noise_preview.png, do lado deste script. Quando estiver bom, me fale os valores
finais que eu porto pra BiomeResolver.cs.
"""

import math
import random
from PIL import Image, ImageDraw

# ======================= CONFIG (mexa aqui) =======================

WORLD_SEED = 12345

IMAGE_SIZE_PIXELS = 5000       # tamanho final da imagem (5000x5000 conforme pedido)
TILE_PIXELS = 32               # cada tile do jogo vira um bloco de NxN pixels na imagem

# Ruido base que decide as grandes regioes Lime/Olive (equivalente ao BiomeNoiseFrequency)
BIOME_NOISE_FREQUENCY = 0.004

# O ruido bruto cruza o zero de forma irregular - as vezes gera uma faixa de bioma enorme, as
# vezes uma bem fina. Pra garantir uma largura minima, usamos a MEDIA de varias amostras
# espalhadas numa janela de MIN_BIOME_BAND_WIDTH tiles (filtro passa-baixa simples).
MIN_BIOME_BAND_WIDTH = 64
BIOME_SMOOTHING_SAMPLE_COUNT = 5

# Ruido que EMPENA a fronteira (desloca a posicao X conforme Y) perto da divisa - cria as
# tendrilhas organicas horizontais em vez de uma linha reta.
WARP_NOISE_FREQUENCY = 0.04
WARP_AMPLITUDE = 48.0
WARP_FRACTAL_OCTAVES = 2  # 4 oitavas oscilava a cada 1-2 tiles no jogo (sem tile pra isso)
WARP_FRACTAL_LACUNARITY = 2.3
WARP_FRACTAL_GAIN = 0.55

# O quanto o warp desvanece suavemente conforme se afasta do cruzamento real (0 = sem
# nenhum warp longe da fronteira, sem corte brusco em lugar nenhum).
FADE_RANGE = 0.2

# Linha de superficie (em TILES, nao pixels de imagem - mesma unidade que o jogo usa) onde o
# relevo fica centrado - tudo acima vira "ceu", so abaixo da altura calculada por coluna e que
# conta como chao (mesmos parametros de altura que cada bioma ja tem em BiomeDB.cs).
SURFACE_BASE_TILE = 78

LIME_HEIGHT_FREQUENCY = 0.05
LIME_HEIGHT_AMPLITUDE = 4.0
LIME_HEIGHT_OFFSET = 0

OLIVE_HEIGHT_FREQUENCY = 0.08
OLIVE_HEIGHT_AMPLITUDE = 8.0
OLIVE_HEIGHT_OFFSET = 0

# A partir de qual tile comecar a procurar um cruzamento entre biomas pra centralizar a
# imagem nele. Mude esse valor pra "rolar" e olhar outras fronteiras (o mundo tem varias,
# uma a cada ~125 tiles em media).
SEARCH_START_TILE = 0

# ======================= FIM CONFIG =======================


class PerlinNoise:
    """Perlin classico (permutation table), 1D e 2D, com seed proprio - so pra prototipagem
    visual, nao precisa bater bit-a-bit com o FastNoiseLite do Godot, so ter forma parecida."""

    def __init__(self, seed):
        rng = random.Random(seed)
        perm = list(range(256))
        rng.shuffle(perm)
        self.perm = perm * 2

    @staticmethod
    def _fade(t):
        return t * t * t * (t * (t * 6 - 15) + 10)

    @staticmethod
    def _lerp(t, a, b):
        return a + t * (b - a)

    @staticmethod
    def _grad1(hash_val, x):
        return x if (hash_val & 1) == 0 else -x

    @staticmethod
    def _grad2(hash_val, x, y):
        h = hash_val & 3
        u = x if h < 2 else y
        v = y if h < 2 else x
        return (u if (h & 1) == 0 else -u) + (v if (h & 2) == 0 else -v)

    def noise1(self, x):
        xi = math.floor(x) & 255
        xf = x - math.floor(x)
        u = self._fade(xf)

        a = self.perm[xi]
        b = self.perm[xi + 1]

        return self._lerp(u, self._grad1(a, xf), self._grad1(b, xf - 1))

    def noise2(self, x, y):
        xi = math.floor(x) & 255
        yi = math.floor(y) & 255
        xf = x - math.floor(x)
        yf = y - math.floor(y)

        u = self._fade(xf)
        v = self._fade(yf)

        aa = self.perm[self.perm[xi] + yi]
        ab = self.perm[self.perm[xi] + yi + 1]
        ba = self.perm[self.perm[xi + 1] + yi]
        bb = self.perm[self.perm[xi + 1] + yi + 1]

        x1 = self._lerp(u, self._grad2(aa, xf, yf), self._grad2(ba, xf - 1, yf))
        x2 = self._lerp(u, self._grad2(ab, xf, yf - 1), self._grad2(bb, xf - 1, yf - 1))

        return self._lerp(v, x1, x2)


def stable_string_hash(value: str) -> int:
    h = 1469598103934665603
    for c in value:
        h ^= ord(c)
        h *= 1099511628211
        h &= 0xFFFFFFFFFFFFFFFF
    return h


def combine_biome_seed(world_seed: int, tag: str) -> int:
    h = world_seed
    h = (h * 397) ^ stable_string_hash(tag)
    h &= 0xFFFFFFFFFFFFFFFF
    return h


_base_noise = PerlinNoise(combine_biome_seed(WORLD_SEED, "biome") & 0x7FFFFFFF)
_warp_noise = PerlinNoise(combine_biome_seed(WORLD_SEED, "biome_warp") & 0x7FFFFFFF)
_lime_height_noise = PerlinNoise(combine_biome_seed(WORLD_SEED, "height_lime") & 0x7FFFFFFF)
_olive_height_noise = PerlinNoise(combine_biome_seed(WORLD_SEED, "height_olive") & 0x7FFFFFFF)


def get_base_value(world_x: float) -> float:
    return _base_noise.noise1(world_x * BIOME_NOISE_FREQUENCY)


def get_smoothed_base_value(world_x: int) -> float:
    half = BIOME_SMOOTHING_SAMPLE_COUNT // 2
    step = MIN_BIOME_BAND_WIDTH / BIOME_SMOOTHING_SAMPLE_COUNT
    total = 0.0

    for i in range(-half, half + 1):
        total += get_base_value(world_x + round(i * step))

    return total / BIOME_SMOOTHING_SAMPLE_COUNT


def get_warp_fbm(world_y: float) -> float:
    total = 0.0
    amplitude = 1.0
    frequency = WARP_NOISE_FREQUENCY
    max_amplitude = 0.0

    for _ in range(WARP_FRACTAL_OCTAVES):
        total += _warp_noise.noise1(world_y * frequency) * amplitude
        max_amplitude += amplitude
        amplitude *= WARP_FRACTAL_GAIN
        frequency *= WARP_FRACTAL_LACUNARITY

    return total / max_amplitude if max_amplitude > 0 else 0.0


def resolve_biome(world_x: int, world_y: int) -> str:
    """Retorna "lime" ou "olive" - mesma logica de BiomeResolver.Resolve."""
    base_value = get_smoothed_base_value(world_x)
    proximity = max(0.0, min(1.0, 1.0 - abs(base_value) / FADE_RANGE))

    if proximity <= 0.0:
        return "lime" if base_value < 0 else "olive"

    warp_offset = round(get_warp_fbm(world_y) * WARP_AMPLITUDE * proximity)

    shifted_value = get_smoothed_base_value(world_x + warp_offset)

    return "lime" if shifted_value < 0 else "olive"


def get_ground_height(tile_x: int) -> int:
    """Altura do relevo dessa coluna, em TILES (mesma logica de ChunkGenerator.Paint: usa o
    bioma "de referencia" resolvido na linha de superficie, pra nao ter degrau quando a
    fronteira corta a coluna no meio)."""
    column_biome = resolve_biome(tile_x, SURFACE_BASE_TILE)

    if column_biome == "lime":
        noise_value = _lime_height_noise.noise1(tile_x * LIME_HEIGHT_FREQUENCY)
        return SURFACE_BASE_TILE + LIME_HEIGHT_OFFSET + round(noise_value * LIME_HEIGHT_AMPLITUDE)

    noise_value = _olive_height_noise.noise1(tile_x * OLIVE_HEIGHT_FREQUENCY)
    return SURFACE_BASE_TILE + OLIVE_HEIGHT_OFFSET + round(noise_value * OLIVE_HEIGHT_AMPLITUDE)


def find_next_crossing(start_tile: int) -> int:
    """Acha o proximo tile, a partir de start_tile, onde o ruido base troca de sinal (ou
    seja, onde a fronteira real entre Lime e Olive passa)."""
    tile_x = start_tile
    prev_sign = get_smoothed_base_value(tile_x) < 0

    while True:
        tile_x += 1
        sign = get_smoothed_base_value(tile_x) < 0

        if sign != prev_sign:
            return tile_x

        prev_sign = sign


def main():
    tiles_per_side = IMAGE_SIZE_PIXELS // TILE_PIXELS

    crossing_tile = find_next_crossing(SEARCH_START_TILE)
    world_x_offset = crossing_tile - tiles_per_side // 2

    print(f"fronteira encontrada no tile {crossing_tile} - centralizando (offset = {world_x_offset})")

    image = Image.new("RGB", (IMAGE_SIZE_PIXELS, IMAGE_SIZE_PIXELS), (0, 0, 0))
    draw = ImageDraw.Draw(image)

    sky_color = (135, 196, 235)
    lime_color = (46, 168, 74)
    olive_color = (168, 60, 46)

    # worldX/worldY usados no ruido sao o INDICE DO TILE (tx + offset, ty), a mesma unidade
    # que o jogo usa - so a posicao de desenho na imagem (px0, py0) e que e escalada por
    # TILE_PIXELS.
    ground_height_by_column = [get_ground_height(tx + world_x_offset) for tx in range(tiles_per_side)]

    for tx in range(tiles_per_side):
        tile_x = tx + world_x_offset
        px0 = tx * TILE_PIXELS
        ground_height_tile = ground_height_by_column[tx]
        ground_start_tile = max(0, ground_height_tile)

        # ceu: tudo acima da altura do relevo
        if ground_start_tile > 0:
            draw.rectangle([px0, 0, px0 + TILE_PIXELS - 1, ground_start_tile * TILE_PIXELS - 1], fill=sky_color)

        # chao: reaplica o bioma POR CELULA a partir da linha de superficie pra baixo
        for ty in range(ground_start_tile, tiles_per_side):
            biome = resolve_biome(tile_x, ty)
            color = lime_color if biome == "lime" else olive_color

            py0 = ty * TILE_PIXELS

            draw.rectangle([px0, py0, px0 + TILE_PIXELS - 1, py0 + TILE_PIXELS - 1], fill=color)

        if tx % 20 == 0:
            print(f"coluna {tx}/{tiles_per_side}")

    out_path = "biome_noise_preview.png"
    image.save(out_path)
    print(f"salvo em {out_path}")


if __name__ == "__main__":
    main()
