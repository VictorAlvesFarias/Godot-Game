"""Gera uma imagem com cada item ao lado do jogador, na escala do jogo, com a grade de pixels.

Cada cartao mostra o primeiro quadro do idle do jogador e a texture.png do item apoiados no chao, cada um
na escala em que o jogo desenha (itens comuns na grade do jogador, blocos na grade do tileset).
A grade fina marca cada pixel da arte (2 px do mundo); a grade forte marca cada tile (16 px do mundo).

Uso:
  py Tools/item_scale_sheet.py [saida.png]
"""

import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from normalize_items import BLOCK_TILES

ROOT = Path(__file__).resolve().parent.parent / "Game"
ITEMS_DIR = ROOT / "Assets" / "Textures" / "Items"
PLAYER_SCENE = ROOT / "Scenes" / "World" / "Characters" / "Player.tscn"
ITEM_DEFINITION = ROOT / "Features" / "World" / "Items" / "Abstractions" / "ItemDefinition.cs"
BLOCK_DEFINITION = ROOT / "Features" / "World" / "Items" / "Definitions" / "BlockItemDefinition.cs"

ZOOM = 3          # pixels da imagem por pixel do mundo
TILE = 16         # pixels do mundo por tile
COLUMNS = 6
PAD = 12
GAP = 8
LABEL_HEIGHT = 22

SKY = (156, 196, 240, 255)
GROUND = (74, 56, 40, 255)
FINE = (0, 0, 0, 28)
STRONG = (0, 0, 0, 80)


def player_frame():
    """Primeiro quadro do idle do jogador e a escala do sprite, lidos da Player.tscn."""
    scene = PLAYER_SCENE.read_text(encoding="utf-8")
    regions = dict(re.findall(r'\[sub_resource type="AtlasTexture" id="([^"]+)"\]\s*atlas = [^\n]+\s*region = Rect2\(([^)]+)\)', scene))
    idle = re.search(r'"frames": \[\{\s*"duration": [^,]+,\s*"texture": SubResource\("([^"]+)"\)(?:(?!"name").)*"name": &"idle"', scene, re.S)
    x, y, w, h = (int(float(v)) for v in regions[idle.group(1)].split(","))
    scale = float(re.search(r'\[node name="Sprite" type="AnimatedSprite2D"[^\]]*\][^\[]*?scale = Vector2\(([^,]+),', scene).group(1))
    atlas = Image.open(ROOT / "Assets" / "Textures" / "Characters" / "character.png").convert("RGBA")
    frame = atlas.crop((x, y, x + w, y + h))
    return frame.crop(frame.getbbox()), scale


def item_scales():
    """Pixels do mundo por pixel da arte: (itens comuns, blocos), lidos do codigo do jogo."""
    def read(path):
        return float(re.search(r"WorldPixelScale => ([\d.]+)f", path.read_text(encoding="utf-8")).group(1))

    return read(ITEM_DEFINITION), read(BLOCK_DEFINITION)


def enlarge(image, world_per_pixel):
    factor = int(round(world_per_pixel * ZOOM))
    return image.resize((image.width * factor, image.height * factor), Image.NEAREST)


def main():
    output = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent / "item_scale_sheet.png"
    player, player_scale = player_frame()
    pixel, block_pixel = item_scales()
    big_player = enlarge(player, player_scale)

    items = []
    for folder in sorted(ITEMS_DIR.iterdir()):
        if (folder / "texture.png").exists():
            scale = block_pixel if folder.name in BLOCK_TILES else pixel
            # Como no jogo, o que apoia no chao e a parte visivel da arte (o tile de bloco tem sobra transparente).
            texture = Image.open(folder / "texture.png").convert("RGBA")
            items.append((folder.name, enlarge(texture.crop(texture.getbbox()), scale)))

    art_height = max([big_player.height] + [image.height for _, image in items])
    art_width = big_player.width + GAP + max(image.width for _, image in items)
    ground = int(TILE * ZOOM)
    card_w = PAD * 2 + art_width
    card_h = PAD + art_height + ground + LABEL_HEIGHT
    rows = (len(items) + COLUMNS - 1) // COLUMNS
    sheet = Image.new("RGBA", (COLUMNS * card_w, rows * card_h), (40, 44, 60, 255))
    font = ImageFont.load_default(size=14)
    step = int(round(pixel * ZOOM))
    tile = TILE * ZOOM

    for index, (name, image) in enumerate(items):
        left = (index % COLUMNS) * card_w
        top = (index // COLUMNS) * card_h
        base = top + PAD + art_height

        card = Image.new("RGBA", (card_w, card_h - LABEL_HEIGHT), SKY)
        draw = ImageDraw.Draw(card)
        draw.rectangle((0, base - top, card_w, card_h), fill=GROUND)
        card.alpha_composite(big_player, (PAD, base - top - big_player.height))
        card.alpha_composite(image, (PAD + big_player.width + GAP, base - top - image.height))

        # Grade alinhada ao chao e a borda esquerda do jogador: fina por pixel da arte, forte por tile.
        grid = Image.new("RGBA", card.size, (0, 0, 0, 0))
        lines = ImageDraw.Draw(grid)
        for x in range(PAD % step, card.width, step):
            lines.line((x, 0, x, card.height), fill=STRONG if (x - PAD) % tile == 0 else FINE)
        for y in range((base - top) % step, card.height, step):
            lines.line((0, y, card.width, y), fill=STRONG if (base - top - y) % tile == 0 else FINE)
        card.alpha_composite(grid)

        sheet.alpha_composite(card, (left, top))
        texture = Image.open(ITEMS_DIR / name / "texture.png")
        ImageDraw.Draw(sheet).text((left + PAD, base + ground + 3), f"{name}  {texture.width}x{texture.height}", font=font, fill=(230, 232, 240, 255))
        ImageDraw.Draw(sheet).rectangle((left, top, left + card_w - 1, top + card_h - 1), outline=(20, 22, 30, 255))

    sheet.save(output)
    print(f"{output}  ({len(items)} itens, jogador {player.width}x{player.height} em escala {player_scale:g}, itens em escala {pixel:g}, blocos em escala {block_pixel:g})")


if __name__ == "__main__":
    main()
