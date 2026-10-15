"""Gera a texture.png de cada item, no tamanho de pixel real e no tamanho do tipo dele.

Cada pasta de item em Game/Assets/Textures/Items/<id>/ fica so com:
  - icon.png: icone do inventario/hotbar (de itens comuns, nao e alterado e e a arte de origem);
  - texture.png: a arte usada no mundo (item caido e cenas de uso).

Itens comuns: a arte do icon.png volta para o pixel real (blocos k x k viram 1 pixel), perde as bordas
transparentes e e redesenhada com o lado maior igual ao tamanho do tipo (TARGET_SIZE), em pixels da grade
do jogador. A reducao nao inteira usa, para cada pixel novo, a cor que mais cobre a area dele.

Blocos: a texture.png e o tile sem conexoes do autotile (com o tamanho da regiao do tileset), com as
camadas que o jogo desenha uma sobre a outra. O icon.png do bloco e a mesma imagem.

Uso:
  py Tools/normalize_items.py            # mostra o que seria feito
  py Tools/normalize_items.py --write    # grava
"""

import re
import sys
from pathlib import Path

from PIL import Image

GAME = Path(__file__).resolve().parent.parent / "Game"
ITEMS_DIR = GAME / "Assets" / "Textures" / "Items"
TILES_DIR = GAME / "Assets" / "Textures" / "Tiles"
OLD_FILES = ("item.png", "dropped.png", "blade.png")
MAX_SCALE = 8
OPAQUE_SHARE = 0.35

SWORD, BOW, ARROW, POTION, PORTAL, TOOL = 20, 12, 10, 7, 13, 16

# Lado maior da arte, em pixels da grade do jogador (o jogador tem 19 de altura).
TARGET_SIZE = {
    "sword_starting": SWORD, "sword_basic": SWORD, "fire_slash_sword": SWORD, "blue_fire_sword": SWORD,
    "blue_fire_sword_alt": SWORD, "dark_slash_sword": SWORD, "real_sword": SWORD, "green_blow_sword": SWORD,
    "gold_sword": SWORD, "silver_sword": SWORD, "whip": SWORD,
    "bow_starting": BOW, "bow_starting2": BOW, "bow_basic": BOW,
    "arrow": ARROW,
    "poison_flask": POTION, "fire_damage_potion": POTION, "health_regen_potion": POTION,
    "speed_potion": POTION, "instant_heal_potion": POTION,
    "portal": PORTAL,
    "pickaxe_starting": TOOL, "pickaxe_onehit": TOOL, "wall_hammer": TOOL,
}

# Blocos: tileset, fontes desenhadas de baixo para cima (como as camadas Base e Compose) e o modulate da camada.
WORLD_TILESET = "world_biomes_tileset.tres"
WALLS_TILESET = "background_walls_tileset.tres"
BLOCK_TILES = {
    "block_dirt": (WORLD_TILESET, [1, 0], 1.0),   # Base: bordercap do bioma lime; Compose: lime_grass
    "item_wood": (WORLD_TILESET, [6], 1.0),
    "item_leaf": (WORLD_TILESET, [7], 1.0),
    "wall_wood": (WALLS_TILESET, [6], 0.65),      # BackgroundWalls desenha com self_modulate 0.65
    "wall_dirt": (WALLS_TILESET, [1], 0.65),      # a parede de terra gerada usa o bordercap do bioma
}


# ---------- Pixel real ----------

def pixels(image):
    """Pixels RGBA, com todo pixel totalmente transparente igual a (0, 0, 0, 0)."""
    data = image.load()
    width, height = image.size
    return [[data[x, y] if data[x, y][3] > 0 else (0, 0, 0, 0) for x in range(width)] for y in range(height)]


def is_scale(grid, width, height, k, ox, oy):
    """True se a imagem e feita de blocos k x k uniformes, com a grade deslocada em (ox, oy)."""
    for y in range(height):
        ay = max(0, oy + ((y - oy) // k) * k)
        for x in range(width):
            ax = max(0, ox + ((x - ox) // k) * k)
            if grid[y][x] != grid[ay][ax]:
                return False
    return True


def detect_scale(grid, width, height):
    """Maior k (e deslocamento) em que a arte e feita de blocos k x k."""
    for k in range(min(MAX_SCALE, width, height), 1, -1):
        for oy in range(k):
            for ox in range(k):
                if is_scale(grid, width, height, k, ox, oy):
                    return k, ox, oy
    return 1, 0, 0


def real_pixels(image):
    """Arte com 1 pixel por pixel real, sem bordas transparentes."""
    image = image.convert("RGBA")
    width, height = image.size
    grid = pixels(image)
    k, ox, oy = detect_scale(grid, width, height)
    xs = sorted({max(0, ox + ((x - ox) // k) * k) for x in range(width)})
    ys = sorted({max(0, oy + ((y - oy) // k) * k) for y in range(height)})
    out = Image.new("RGBA", (len(xs), len(ys)))
    for j, y in enumerate(ys):
        for i, x in enumerate(xs):
            out.putpixel((i, j), grid[y][x])
    bbox = out.getbbox()
    return (out.crop(bbox) if bbox else out), k


# ---------- Reducao para o tamanho do tipo ----------

def luminance(color):
    return 0.299 * color[0] + 0.587 * color[1] + 0.114 * color[2]


def resize_majority(image, width, height):
    """Cada pixel novo pega a cor opaca que mais cobre a area dele (empate: a mais escura, que e o contorno).
    Fica transparente quando a parte opaca da area e menor que OPAQUE_SHARE (baixo o bastante para linhas de 1 pixel)."""
    src = pixels(image)
    sx = image.width / width
    sy = image.height / height
    out = Image.new("RGBA", (width, height))
    for j in range(height):
        y0, y1 = j * sy, (j + 1) * sy
        for i in range(width):
            x0, x1 = i * sx, (i + 1) * sx
            weights = {}
            opaque = 0.0
            for y in range(int(y0), min(image.height, int(y1 + 0.999))):
                hy = min(y1, y + 1) - max(y0, y)
                if hy <= 0:
                    continue
                for x in range(int(x0), min(image.width, int(x1 + 0.999))):
                    hx = min(x1, x + 1) - max(x0, x)
                    if hx <= 0:
                        continue
                    color = src[y][x]
                    if color[3] == 0:
                        continue
                    weights[color] = weights.get(color, 0.0) + hx * hy
                    opaque += hx * hy
            if opaque < OPAQUE_SHARE * sx * sy:
                continue
            best = max(weights, key=lambda c: (round(weights[c], 6), -luminance(c)))
            out.putpixel((i, j), best)
    return out


def item_texture(folder):
    art, k = real_pixels(Image.open(folder / "icon.png"))
    target = TARGET_SIZE.get(folder.name)
    if target is None or max(art.size) <= target:
        return art, k
    factor = target / max(art.size)
    size = (max(1, round(art.width * factor)), max(1, round(art.height * factor)))
    return resize_majority(art, *size), k


# ---------- Blocos ----------

def tileset_sources(name):
    """{id da fonte: (textura, tamanho da regiao, tile sem conexoes)} lidos do .tres."""
    text = (TILES_DIR / name).read_text(encoding="utf-8")
    textures = dict((rid, path) for path, rid in re.findall(r'path="res://([^"]+)" id="([^"]+)"', text))
    sources = {}
    for sid, sub in re.findall(r'sources/(\d+) = SubResource\("([^"]+)"\)', text):
        block = text.split(f'id="{sub}"]', 1)[1].split("\n[", 1)[0]
        texture = GAME / textures[re.search(r'texture = ExtResource\("([^"]+)"\)', block).group(1)]
        region = tuple(int(v) for v in re.search(r"texture_region_size = Vector2i\((\d+), (\d+)\)", block).groups())
        tiles = {}
        for coord, prop in re.findall(r"^(\d+:\d+)/0(/\S*)? = ", block, re.M):
            tile = tiles.setdefault(coord, {"terrain": False, "peering": 0})
            tile["terrain"] |= prop == "/terrain"
            tile["peering"] += prop.startswith("/terrains_peering_bit")
        isolated = [c for c, t in tiles.items() if t["terrain"] and t["peering"] == 0]
        sources[int(sid)] = (texture, region, tuple(int(v) for v in isolated[0].split(":")))
    return sources


def block_texture(name):
    tileset, layers, modulate = BLOCK_TILES[name]
    sources = tileset_sources(tileset)
    out = None
    for sid in layers:
        texture, (w, h), (cx, cy) = sources[sid]
        tile = Image.open(texture).convert("RGBA").crop((cx * w, cy * h, cx * w + w, cy * h + h))
        out = tile if out is None else Image.alpha_composite(out, tile)
    if modulate != 1.0:
        r, g, b, a = out.split()
        out = Image.merge("RGBA", [c.point(lambda v: round(v * modulate)) for c in (r, g, b)] + [a])
    return out


# ---------- Principal ----------

def item_folders():
    for folder in sorted(ITEMS_DIR.iterdir()):
        if folder.is_dir() and (folder / "icon.png").exists() and (folder / "texture.png").exists():
            yield folder


def main():
    write = "--write" in sys.argv

    print(f"{'item':<24}{'tipo':<10}{'icon':<9}{'pixel':<7}{'texture.png':<12}")
    for folder in item_folders():
        icon = Image.open(folder / "icon.png")
        if folder.name in BLOCK_TILES:
            texture, kind, k = block_texture(folder.name), "bloco", "-"
        else:
            texture, k = item_texture(folder)
            kind = TARGET_SIZE.get(folder.name, "?")
        print(f"{folder.name:<24}{str(kind):<10}{icon.width}x{icon.height:<6}{str(k):<7}{texture.width}x{texture.height}")

        if write:
            texture.save(folder / "texture.png")
            if folder.name in BLOCK_TILES:
                texture.save(folder / "icon.png")
            for name in OLD_FILES:
                for path in (folder / name, folder / (name + ".import")):
                    path.unlink(missing_ok=True)

    if not write:
        print("\nNada gravado. Rode com --write para gravar.")


if __name__ == "__main__":
    main()
