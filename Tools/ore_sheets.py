"""Gera as folhas de autotile dos minerios a partir da folha de terra exposta (bordercap do bioma lime).

Cada folha e a mesma folha de terra (mesmo layout de 47 tiles em regioes de 24x24), com veios de pontos na cor do
metal desenhados so dentro da parte opaca de cada tile, longe das bordas transparentes. O desenho e deterministico
(semente por minerio e por tile), entao rodar de novo da a mesma arte.

Uso:
  py Tools/ore_sheets.py            # gera em Game/Assets/Textures/Tiles/ores/
"""

import random
from pathlib import Path

from PIL import Image

GAME = Path(__file__).resolve().parent.parent / "Game"
SOURCE = GAME / "Assets" / "Textures" / "Tiles" / "lime" / "lime_bordercap_texture.png"
OUT = GAME / "Assets" / "Textures" / "Tiles" / "ores"
REGION = 24
MARGIN = 3

# (sombra, cor, brilho)
ORES = {
    "copper_ore": ((130, 70, 40), (201, 113, 57), (240, 160, 90)),
    "iron_ore": ((105, 100, 104), (176, 170, 166), (222, 217, 212)),
    "gold_ore": ((150, 110, 30), (230, 190, 60), (255, 236, 130)),
}

# Forma de um ponto de minerio (3x3, sem os cantos): brilho no topo-esquerda, sombra embaixo-direita.
SPECK = [
    (0, -1, 2), (-1, 0, 2), (0, 0, 1), (1, 0, 1), (0, 1, 0), (1, 1, 0),
]


def opaque(image, x, y):
    return 0 <= x < image.width and 0 <= y < image.height and image.getpixel((x, y))[3] > 0


def solid_around(image, x, y):
    """O ponto e seus vizinhos ate MARGIN pixels estao todos opacos."""
    return all(opaque(image, x + dx, y + dy) for dx in (-MARGIN, 0, MARGIN) for dy in (-MARGIN, 0, MARGIN))


def draw_sheet(base, ore_id, colors):
    sheet = base.copy()
    for ty in range(base.height // REGION):
        for tx in range(base.width // REGION):
            rng = random.Random(f"{ore_id}:{tx}:{ty}")
            ox, oy = tx * REGION, ty * REGION
            candidates = [(ox + x, oy + y) for y in range(REGION) for x in range(REGION) if solid_around(base, ox + x, oy + y)]
            if not candidates:
                continue
            for _ in range(rng.randint(3, 5)):
                cx, cy = rng.choice(candidates)
                for dx, dy, shade in SPECK:
                    if opaque(base, cx + dx, cy + dy):
                        sheet.putpixel((cx + dx, cy + dy), colors[shade] + (255,))
    return sheet


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    base = Image.open(SOURCE).convert("RGBA")
    for ore_id, colors in ORES.items():
        draw_sheet(base, ore_id, colors).save(OUT / f"{ore_id}_texture.png")
        print("gerada", ore_id)


if __name__ == "__main__":
    main()
