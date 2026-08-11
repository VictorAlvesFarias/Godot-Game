#!/usr/bin/env python3
"""
Gera dezenas de arvores com algoritmos DIFERENTES de silhueta, renderizadas como pixel art
(cada celula = um bloco solido de 16x16px, tronco marrom / folha rosa), pra escolher visualmente
quais formatos usar como base da geracao real (TreeStructureDefinition.cs).

Uso:
    python .dev/generate_tree_variations.py

Saida:
    .dev/tree_previews/<NN>_<algoritmo>_seed<S>.png   - uma arvore por arquivo
    .dev/tree_previews/_contact_sheet.png              - todas juntas, rotuladas, pra comparar rapido

Cada arvore e representada como uma grade de celulas (0=vazio, 1=tronco, 2=folha), do jeito que o
jogo representa (uma celula = um tile). O algoritmo escolhido so precisa devolver essa grade -
constants de cor/tamanho de pixel ficam isoladas no renderer, pra facilitar trocar depois.
"""

import random
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

CELL_PX = 16                # cada celula da arvore vira um bloco de 16x16px (tile do jogo)
GRID_W = 21                  # largura do grid, em celulas (impar pra ter centro exato)
GRID_H = 28                  # altura do grid, em celulas

TRUNK_COLOR = (121, 74, 43)      # marrom
TRUNK_COLOR_SHADE = (98, 58, 33)  # marrom mais escuro, pra variar um pouco por celula
LEAF_COLOR = (235, 120, 170)      # rosa
LEAF_COLOR_SHADE = (214, 92, 148)  # rosa mais escuro, pra variar um pouco por celula

BG_COLOR = (30, 30, 34)
GRID_LINE_COLOR = (48, 48, 54)
GROUND_LINE_COLOR = (90, 90, 60)

EMPTY, TRUNK, LEAF = 0, 1, 2

OUT_DIR = Path(__file__).parent / "tree_previews"

VARIANTS_PER_ALGO = 6  # dezenas no total = len(ALGORITHMS) * VARIANTS_PER_ALGO


# ---------------------------------------------------------------------------
# Grid helper - toda arvore desenha nisso (origem = base do tronco, centro X do grid)
# ---------------------------------------------------------------------------

class TreeGrid:
    def __init__(self, width=GRID_W, height=GRID_H):
        self.width = width
        self.height = height
        self.cells = [[EMPTY] * width for _ in range(height)]
        self.origin_x = width // 2
        self.origin_y = height - 1  # base do tronco fica perto do fundo do grid

    def set(self, x, y, value):
        """x,y relativos a base do tronco (x=0 e o centro, y=0 e o chao, y negativo sobe)."""
        gx = self.origin_x + x
        gy = self.origin_y - y
        if 0 <= gx < self.width and 0 <= gy < self.height:
            # Nao deixa folha sobrescrever tronco ja desenhado.
            if self.cells[gy][gx] != TRUNK:
                self.cells[gy][gx] = value

    def set_trunk(self, x, y):
        gx = self.origin_x + x
        gy = self.origin_y - y
        if 0 <= gx < self.width and 0 <= gy < self.height:
            self.cells[gy][gx] = TRUNK  # tronco sempre pode sobrescrever folha


# ---------------------------------------------------------------------------
# Algoritmos de silhueta - cada um recebe um random.Random ja semeado e devolve um TreeGrid
# ---------------------------------------------------------------------------

def algo_pine(rng: random.Random) -> TreeGrid:
    """Pinheiro classico: afunila so no topo, base da copa fica 'cheia' no raio maximo."""
    grid = TreeGrid()
    trunk_h = rng.randint(4, 9)
    canopy_rows = rng.randint(5, 9)
    max_radius = rng.randint(2, 4)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    for row in range(canopy_rows):
        row_from_top = canopy_rows - 1 - row
        radius = min(row_from_top, max_radius)
        y = trunk_h + row
        for x in range(-radius, radius + 1):
            grid.set(x, y, LEAF)

    return grid


def algo_round(rng: random.Random) -> TreeGrid:
    """Copa arredondada tipo bolha - afunila em cima E embaixo (elipse)."""
    grid = TreeGrid()
    trunk_h = rng.randint(5, 10)
    canopy_rows = rng.randint(6, 11)
    max_radius = rng.randint(3, 6)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    center_row = (canopy_rows - 1) / 2
    half_h = max(0.5, canopy_rows / 2)

    for row in range(canopy_rows):
        dist = abs(row - center_row)
        t = max(0.0, 1.0 - (dist * dist) / (half_h * half_h))
        radius = max(1, round(max_radius * math.sqrt(t)))
        y = trunk_h + row
        for x in range(-radius, radius + 1):
            grid.set(x, y, LEAF)

    return grid


def algo_organic_blob(rng: random.Random) -> TreeGrid:
    """Silhueta organica - raio de cada linha faz um 'random walk' (+-1) em vez de curva
    matematica perfeita, e a borda tem chance de perder celula, pra ficar irregular tipo
    desenhada a mao."""
    grid = TreeGrid()
    trunk_h = rng.randint(4, 9)
    canopy_rows = rng.randint(6, 10)
    max_radius = rng.randint(3, 6)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    # Radio sobe ate o pico e desce, com passo aleatorio (nao suave).
    peak_row = canopy_rows // 2
    radius = 1
    radii = []
    for row in range(canopy_rows):
        target = max_radius if row == peak_row else max_radius - abs(row - peak_row)
        target = max(1, target)
        radius += rng.choice([-1, 0, 0, 1])
        radius = max(1, min(target + rng.choice([-1, 0, 1]), max_radius))
        radii.append(radius)

    for row, radius in enumerate(radii):
        y = trunk_h + row
        for x in range(-radius, radius + 1):
            # borda tem chance de "esburacar", miolo sempre fica cheio
            if abs(x) >= radius - 1 and rng.random() < 0.28:
                continue
            grid.set(x, y, LEAF)

    return grid


def algo_clusters(rng: random.Random) -> TreeGrid:
    """Varios 'tufos' circulares sobrepostos perto do topo do tronco - estilo cartoon/nuvem."""
    grid = TreeGrid()
    trunk_h = rng.randint(5, 10)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    cluster_count = rng.randint(3, 5)
    top_y = trunk_h + rng.randint(3, 5)

    for _ in range(cluster_count):
        cx = rng.randint(-3, 3)
        cy = top_y + rng.randint(-3, 3)
        r = rng.randint(2, 4)

        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + rng.choice([-1, 0, 1]):
                    grid.set(cx + dx, cy + dy, LEAF)

    return grid


def algo_willow(rng: random.Random) -> TreeGrid:
    """Chorao - copa larga em cima, 'fios' de folha pendurados descendo pelas bordas."""
    grid = TreeGrid()
    trunk_h = rng.randint(6, 10)
    canopy_rows = rng.randint(4, 6)
    max_radius = rng.randint(4, 6)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    top_y = trunk_h + canopy_rows - 1

    # Copa densa em cima (elipse achatada).
    for row in range(canopy_rows):
        t = 1.0 - (row / canopy_rows) * 0.4
        radius = max(2, round(max_radius * t))
        y = trunk_h + row
        for x in range(-radius, radius + 1):
            grid.set(x, y, LEAF)

    # Fios pendurados nas bordas, descendo alguns tiles.
    strand_count = rng.randint(5, 9)
    for _ in range(strand_count):
        strand_x = rng.randint(-max_radius, max_radius)
        strand_len = rng.randint(2, 5)
        start_y = trunk_h + canopy_rows - 1 - rng.randint(0, 1)
        for i in range(strand_len):
            if rng.random() < 0.85:
                grid.set(strand_x, start_y - i, LEAF)

    return grid


def algo_layered(rng: random.Random) -> TreeGrid:
    """Camadas separadas empilhadas (tipo pagode) - 2 ou 3 discos de folha com um gap vazio
    entre cada um, ficando com a copa 'flutuando' em niveis."""
    grid = TreeGrid()
    trunk_h = rng.randint(6, 12)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    layer_count = rng.randint(2, 3)
    y = trunk_h
    for layer in range(layer_count):
        radius = rng.randint(2, 5) - layer  # camadas de cima mais estreitas
        radius = max(1, radius)
        layer_h = rng.randint(2, 3)

        for row in range(layer_h):
            row_radius = radius if row < layer_h - 1 else max(1, radius - 1)
            for x in range(-row_radius, row_radius + 1):
                grid.set(x, y + row, LEAF)

        y += layer_h + 1  # gap vazio entre camadas

    return grid


def algo_branching(rng: random.Random) -> TreeGrid:
    """Tronco com 2-3 galhos diagonais saindo pro lado, cada um terminando num tufo de folha -
    silhueta mais 'de verdade', menos simetrica."""
    grid = TreeGrid()
    trunk_h = rng.randint(7, 12)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    branch_count = rng.randint(2, 4)
    branch_starts = sorted(rng.sample(range(trunk_h // 2, trunk_h), min(branch_count, trunk_h - trunk_h // 2)))

    tips = [(0, trunk_h)]  # topo do tronco tambem ganha tufo

    for start_y in branch_starts:
        direction = rng.choice([-1, 1])
        length = rng.randint(2, 4)
        x, y = 0, start_y
        for _ in range(length):
            x += direction
            y += 1
            grid.set_trunk(x, y)
        tips.append((x, y))

    for cx, cy in tips:
        r = rng.randint(2, 4)
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r + rng.choice([-1, 0, 1]):
                    grid.set(cx + dx, cy + dy, LEAF)

    return grid


def algo_mushroom(rng: random.Random) -> TreeGrid:
    """Guarda-chuva/palmeira - tronco fino e alto, copa so aparece bem no topo, larga."""
    grid = TreeGrid()
    trunk_h = rng.randint(9, 15)
    max_radius = rng.randint(4, 7)
    canopy_rows = rng.randint(3, 5)

    for step in range(1, trunk_h + 1):
        grid.set_trunk(0, step)

    for row in range(canopy_rows):
        # estreito -> largo -> um pouco mais estreito no topo (arco)
        t = row / max(1, canopy_rows - 1)
        radius = max(1, round(max_radius * math.sin(t * math.pi * 0.85 + 0.15)))
        y = trunk_h + row
        for x in range(-radius, radius + 1):
            grid.set(x, y, LEAF)

    return grid


ALGORITHMS = [
    ("pine", algo_pine),
    ("round", algo_round),
    ("blob", algo_organic_blob),
    ("clusters", algo_clusters),
    ("willow", algo_willow),
    ("layered", algo_layered),
    ("branching", algo_branching),
    ("mushroom", algo_mushroom),
]


# ---------------------------------------------------------------------------
# Renderer - grid de celulas -> PNG (blocos solidos de CELL_PX x CELL_PX)
# ---------------------------------------------------------------------------

def render_tree(grid: TreeGrid, seed: int) -> Image.Image:
    img = Image.new("RGB", (grid.width * CELL_PX, grid.height * CELL_PX), BG_COLOR)
    draw = ImageDraw.Draw(img)

    # grade de fundo, pra dar nocao de escala/tile (igual ao overlay do jogo)
    for gx in range(grid.width + 1):
        px = gx * CELL_PX
        draw.line([(px, 0), (px, img.height)], fill=GRID_LINE_COLOR, width=1)
    for gy in range(grid.height + 1):
        py = gy * CELL_PX
        draw.line([(0, py), (img.width, py)], fill=GRID_LINE_COLOR, width=1)

    # linha do chao, na base do tronco
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


def make_contact_sheet(entries):
    """entries: lista de (label, PIL.Image). Monta um grid com legenda embaixo de cada uma."""
    cols = 6
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


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

# Indice fixo de cada algoritmo na lista completa - usado como base do seed, pra um algoritmo
# sempre gerar as MESMAS arvores independente de quais outros algoritmos estao sendo filtrados
# na rodada (rodar so "--algos round" da as mesmas seeds 9000..9005/10000.. de sempre).
ALGO_BASE_INDEX = {name: i for i, (name, _fn) in enumerate(ALGORITHMS)}


def main():
    import argparse

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--algos", type=str, default="",
        help=f"Nomes de algoritmo separados por virgula (default: todos). Opcoes: {', '.join(n for n, _ in ALGORITHMS)}"
    )
    parser.add_argument("--variants", type=int, default=VARIANTS_PER_ALGO, help="Quantas variacoes por algoritmo")
    parser.add_argument("--seed-start", type=int, default=0, help="Pula as N primeiras variacoes (pra gerar uma leva NOVA sem repetir seed ja vista)")
    parser.add_argument("--out", type=str, default=str(OUT_DIR), help="Pasta de saida")
    parser.add_argument("--keep-existing", action="store_true", help="Nao apaga PNGs ja existentes na pasta de saida")
    args = parser.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    if not args.keep_existing:
        for old in out_dir.glob("*.png"):
            old.unlink()

    algo_names = [a.strip() for a in args.algos.split(",") if a.strip()] if args.algos else None
    algos = [(n, f) for n, f in ALGORITHMS if algo_names is None or n in algo_names]

    if not algos:
        raise SystemExit(f"Nenhum algoritmo bate com --algos={args.algos!r}. Opcoes: {', '.join(n for n, _ in ALGORITHMS)}")

    entries = []
    file_index = 0

    for algo_name, algo_fn in algos:
        base = ALGO_BASE_INDEX[algo_name]
        for variant in range(args.seed_start, args.seed_start + args.variants):
            seed = base * 1000 + variant
            rng = random.Random(seed)

            grid = algo_fn(rng)
            img = render_tree(grid, seed)

            filename = f"{file_index:02d}_{algo_name}_seed{seed}.png"
            img.save(out_dir / filename)

            label = f"{algo_name} #{variant}"
            entries.append((label, img))

            file_index += 1

    sheet = make_contact_sheet(entries)
    sheet.save(out_dir / "_contact_sheet.png")

    print(f"Geradas {file_index} arvores ({len(algos)} algoritmos x {args.variants} variacoes, seed_start={args.seed_start}).")
    print(f"Arquivos individuais: {out_dir}")
    print(f"Folha de contato (todas juntas, pra olhar rapido): {out_dir / '_contact_sheet.png'}")


if __name__ == "__main__":
    main()
