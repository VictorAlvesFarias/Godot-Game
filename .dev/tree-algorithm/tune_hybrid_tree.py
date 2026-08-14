#!/usr/bin/env python3
"""
Espelha (em Python, pra iterar rapido) o algoritmo hibrido que esta hoje em
TreeStructureDefinition.cs: canopy base (RadiusByRow com afunilamento + variacao) + AddLeafClusters
(tufos redondos espalhados) + AddBranches (galhos diagonais saindo do tronco, cada um com seu
proprio tufo pequeno - da o efeito de "graveto marrom aparecendo por dentro da folhagem rosa" que
dominou a selecao).

Os PARAMS abaixo sao os mesmos ranges usados no C# (comentado ao lado de cada um) - mexe aqui,
roda de novo, compara com .dev/tree_previews/selected/, e so depois de bater o olho eu aplico a
mudanca de volta no TreeStructureDefinition.cs.

Nota: 1 celula do preview = 1 tile no jogo. O C# NAO multiplica esses ranges pelo worldScale
do ChunkGenerator (tile_size=16 -> worldScale=2); o preview e o jogo devem bater 1:1 em celulas.

Uso:
    python .dev/tune_hybrid_tree.py [--count 30] [--seed-start 0] [--out .temp/tree_previews/hybrid]
"""

import argparse
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

CELL_PX = 16
GRID_W = 25
GRID_H = 30

TRUNK_COLOR = (121, 74, 43)
TRUNK_COLOR_SHADE = (98, 58, 33)
LEAF_COLOR = (235, 120, 170)
LEAF_COLOR_SHADE = (214, 92, 148)
BG_COLOR = (30, 30, 34)
GRID_LINE_COLOR = (48, 48, 54)
GROUND_LINE_COLOR = (90, 90, 60)

EMPTY, TRUNK, LEAF = 0, 1, 2

# --------------------------------------------------------------------------------------
# PARAMS - mesmos ranges do C# atual (TreeStructureDefinition.cs), pra tunar aqui primeiro
# --------------------------------------------------------------------------------------

PARAMS = dict(
    trunk_height=(7, 12),      # GenerateShape salt 0 - ERA (4,8): um pouco baixo demais.
                                # Sobe pra dar mais tronco visivel embaixo da copa, sem voltar
                                # a virar talo fino com bolinha em cima.
    canopy_height=(5, 9),      # salt 1
    max_radius=(3, 6),         # salt 2 - ERA (2,5): copa maior/mais densa, dominando o
                                # visual (a selecao tem copa BEM maior que o tronco).
    trunk_lean=(-1, 1),        # salt 3 - ERA (-2,2): lean menor, senao o tronco foge de
                                # baixo da copa e sobra pra fora dela.
    row_variation=(-1, 1),     # salt 100, por linha
    edge_skip_chance=1 / 6,    # edgeRandom == 0 em (0,4) - um pouco menos vazado

    branch_count=(2, 4),       # salt 700
    branch_attach_drop=(0, 2),  # ERA fracao (0.5,0.95) da altura do TRONCO TODO - mas a copa
                                  # so comeca em 100% dessa altura, entao 50% ficava bem longe
                                  # dela (o galho nao alcancava, sobrava vao vazio no meio -
                                  # exatamente o bug reportado). Agora "attach_drop" e quantas
                                  # celulas ABAIXO DO TOPO do tronco o galho nasce (0,1 ou 2) -
                                  # sempre colado onde a copa ja comeca.
    branch_length=(2, 4),      # salt +2
    branch_leaf_radius=(1, 2),  # raio do tufo na ponta do galho - ERA fixo em 1 (pequeno
                                 # demais pra se fundir com a copa principal)

    cluster_count=(3, 7),      # salt 800 - mais tufos extra, copa fica mais cheia/organica
    cluster_radius=(1, 3),     # salt +2
)


class TreeGrid:
    def __init__(self, width=GRID_W, height=GRID_H):
        self.width = width
        self.height = height
        self.cells = [[EMPTY] * width for _ in range(height)]
        self.origin_x = width // 2
        self.origin_y = height - 1

    def paint(self, x, y, value, overwrite_trunk=False):
        gx = self.origin_x + x
        gy = self.origin_y - y
        if 0 <= gx < self.width and 0 <= gy < self.height:
            if overwrite_trunk or self.cells[gy][gx] != TRUNK:
                self.cells[gy][gx] = value


def generate_shape(rng: random.Random):
    trunk_height = rng.randint(*PARAMS["trunk_height"])
    canopy_height = rng.randint(*PARAMS["canopy_height"])
    max_radius = rng.randint(*PARAMS["max_radius"])
    trunk_lean = rng.randint(*PARAMS["trunk_lean"])

    radius_by_row = []
    for row in range(canopy_height):
        normalized = 0.0 if canopy_height <= 1 else row / (canopy_height - 1)

        if normalized < 0.20:
            shape = normalized / 0.20
        elif normalized > 0.80:
            shape = (1.0 - normalized) / 0.20
        else:
            shape = 1.0

        variation = rng.randint(*PARAMS["row_variation"])
        radius = round(max_radius * shape) + variation
        radius_by_row.append(max(0, radius))

    return dict(
        trunk_height=trunk_height,
        canopy_height=canopy_height,
        max_radius=max_radius,
        trunk_lean=trunk_lean,
        radius_by_row=radius_by_row,
    )


def trunk_position(step, trunk_height, trunk_lean):
    """Retorna (x, h) - h = altura acima do chao (positivo = pra cima), igual ao
    GetTrunkPosition do C# (la e groundCell + Vector2I(x, -step); como Godot Y cresce pra
    BAIXO, -step ali equivale a 'step' de altura aqui)."""
    progress = 0.0 if trunk_height <= 1 else step / trunk_height
    x = round(trunk_lean * progress)
    return x, step


def add_branches(grid: TreeGrid, shape, rng: random.Random):
    branch_count = rng.randint(*PARAMS["branch_count"])

    for _ in range(branch_count):
        drop = rng.randint(*PARAMS["branch_attach_drop"])
        step = max(1, shape["trunk_height"] - drop)
        direction = rng.choice([-1, 1])
        length = rng.randint(*PARAMS["branch_length"])

        start_x, start_h = trunk_position(step, shape["trunk_height"], shape["trunk_lean"])

        for i in range(1, length + 1):
            # O galho sobe (fica mais alto) conforme i cresce, mirando de volta pra dentro
            # da copa - nasce perto do topo do tronco entao ja entra direto na regiao onde a
            # copa existe, em vez de ficar pendurado longe dela.
            vertical_offset = round(i * 0.35)
            x = start_x + direction * i
            h = start_h + vertical_offset

            grid.paint(x, h, TRUNK, overwrite_trunk=True)

            leaf_radius = rng.randint(*PARAMS["branch_leaf_radius"])
            for lx in range(-leaf_radius, leaf_radius + 1):
                for ly in range(-leaf_radius, leaf_radius + 1):
                    if abs(lx) + abs(ly) > leaf_radius + 1:
                        continue
                    grid.paint(x + lx, h + ly, LEAF)


def add_leaf_clusters(grid: TreeGrid, shape, rng: random.Random):
    cluster_count = rng.randint(*PARAMS["cluster_count"])

    for _ in range(cluster_count):
        row = rng.randint(0, shape["canopy_height"] - 1)
        radius = shape["radius_by_row"][row]
        x = rng.randint(-radius, radius) if radius > 0 else 0
        cluster_radius = rng.randint(*PARAMS["cluster_radius"])

        center_x = shape["trunk_lean"] + x
        center_y = shape["trunk_height"] + row

        for dx in range(-cluster_radius, cluster_radius + 1):
            for dy in range(-cluster_radius, cluster_radius + 1):
                if dx * dx + dy * dy > cluster_radius * cluster_radius:
                    continue
                grid.paint(center_x + dx, center_y + dy, LEAF)


def build_tree(rng: random.Random) -> TreeGrid:
    shape = generate_shape(rng)
    grid = TreeGrid()

    for step in range(1, shape["trunk_height"] + 1):
        x, h = trunk_position(step, shape["trunk_height"], shape["trunk_lean"])
        grid.paint(x, h, TRUNK, overwrite_trunk=True)

    canopy_center_x = shape["trunk_lean"]
    for row in range(shape["canopy_height"]):
        radius = shape["radius_by_row"][row]
        normalized = 0.0 if shape["canopy_height"] <= 1 else row / (shape["canopy_height"] - 1)
        center_offset = round(shape["trunk_lean"] * normalized)
        center_x = canopy_center_x + center_offset

        for x in range(-radius, radius + 1):
            absolute_x = center_x + x
            is_edge = abs(x) >= radius - 1
            if is_edge and rng.random() < PARAMS["edge_skip_chance"]:
                continue
            grid.paint(absolute_x, shape["trunk_height"] + row, LEAF)

    add_leaf_clusters(grid, shape, rng)
    add_branches(grid, shape, rng)

    return grid


# --------------------------------------------------------------------------------------
# Render (identico ao script principal)
# --------------------------------------------------------------------------------------

def render_tree(grid: TreeGrid, seed: int) -> Image.Image:
    img = Image.new("RGB", (grid.width * CELL_PX, grid.height * CELL_PX), BG_COLOR)
    draw = ImageDraw.Draw(img)

    for gx in range(grid.width + 1):
        px = gx * CELL_PX
        draw.line([(px, 0), (px, img.height)], fill=GRID_LINE_COLOR, width=1)
    for gy in range(grid.height + 1):
        py = gy * CELL_PX
        draw.line([(0, py), (img.width, py)], fill=GRID_LINE_COLOR, width=1)

    ground_py = (grid.origin_y + 1) * CELL_PX
    draw.line([(0, ground_py), (img.width, ground_py)], fill=GROUND_LINE_COLOR, width=2)

    shade_rng = random.Random(seed * 7919)
    for gy in range(grid.height):
        for gx in range(grid.width):
            cell = grid.cells[gy][gx]
            if cell == EMPTY:
                continue
            if cell == TRUNK:
                color = TRUNK_COLOR if shade_rng.random() < 0.75 else TRUNK_COLOR_SHADE
            else:
                color = LEAF_COLOR if shade_rng.random() < 0.7 else LEAF_COLOR_SHADE
            x0, y0 = gx * CELL_PX, gy * CELL_PX
            draw.rectangle([x0, y0, x0 + CELL_PX - 1, y0 + CELL_PX - 1], fill=color)

    return img


def make_contact_sheet(entries, cols=6):
    rows = math.ceil(len(entries) / cols)
    try:
        font = ImageFont.load_default(size=14)
    except TypeError:
        font = ImageFont.load_default()

    label_h = 20
    cell_w = max(img.width for _, img in entries) + 12
    cell_h = max(img.height for _, img in entries) + label_h + 12

    sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), (18, 18, 20))
    draw = ImageDraw.Draw(sheet)
    for i, (label, img) in enumerate(entries):
        col, row = i % cols, i // cols
        x = col * cell_w + 6
        y = row * cell_h + label_h + 6
        sheet.paste(img, (x, y))
        draw.text((col * cell_w + 6, row * cell_h + 4), label, fill=(230, 230, 230), font=font)
    return sheet


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--count", type=int, default=30)
    parser.add_argument("--seed-start", type=int, default=0)
    parser.add_argument("--out", type=str, default=".temp/tree_previews/hybrid")
    args = parser.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob("*.png"):
        old.unlink()

    entries = []
    for i in range(args.count):
        seed = args.seed_start + i
        rng = random.Random(seed)
        grid = build_tree(rng)
        img = render_tree(grid, seed)
        img.save(out_dir / f"{i:02d}_hybrid_seed{seed}.png")
        entries.append((f"hybrid #{seed}", img))

    sheet = make_contact_sheet(entries)
    sheet.save(out_dir / "_contact_sheet.png")
    print(f"Geradas {len(entries)} arvores em {out_dir}")


if __name__ == "__main__":
    main()
