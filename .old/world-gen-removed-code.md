# Código removido da geração de mundo

Arquivado em `2026-08-13`, junto com a resolução da revisão em `.dev/world-gen-review.md`.
Cada bloco abaixo é código que existia no projeto e foi removido — motivo explicado antes de
cada um. `TreeDebugConstants.cs` (arquivo inteiro) está preservado ao lado deste `.md`, em
`.old/TreeDebugConstants.cs`.

---

## `TreeStructureDefinition.cs` — `TreeRandom` (Mersenne Twister próprio)

Removido: substituído por `WorldRandom` (hash determinístico por seed+coluna+salt), que já é
o mecanismo usado pelo resto do subsistema de geração (`BiomeResolver`, `ChunkGenerator`,
ruído de altura). Duas formas de aleatoriedade determinística no mesmo subsistema sem
necessidade funcional pra isso. O CONCEITO de geração (ranges de tronco/copa/galho/tufo, a
lógica de `GenerateShape`/`AddLeafClusters`/`AddBranches`) foi mantido igual — só o gerador de
número aleatório por baixo mudou.

```csharp
private sealed class TreeRandom
{
    private readonly uint[] _state = new uint[624];
    private int _index = 624;

    public TreeRandom(uint seed)
    {
        Initialize(seed);
    }

    private void Initialize(uint seed)
    {
        _state[0] = seed;
        for (var i = 1; i < _state.Length; i++)
        {
            var previous = _state[i - 1];
            _state[i] = unchecked(1812433253u * (previous ^ (previous >> 30)) + (uint)i);
        }

        _index = _state.Length;
    }

    private void Twist()
    {
        for (var i = 0; i < _state.Length; i++)
        {
            var y = (_state[i] & 0x80000000u) | (_state[(i + 1) % _state.Length] & 0x7fffffffu);
            var next = _state[(i + 397) % _state.Length] ^ (y >> 1);

            if ((y & 1u) != 0)
            {
                next ^= 0x9908b0dfu;
            }

            _state[i] = next;
        }

        _index = 0;
    }

    private uint NextUInt32()
    {
        if (_index >= _state.Length)
        {
            Twist();
        }

        var value = _state[_index++];
        value ^= value >> 11;
        value ^= (value << 7) & 0x9d2c5680u;
        value ^= (value << 15) & 0xefc60000u;
        value ^= value >> 18;

        return value;
    }

    public double NextDouble()
    {
        return (((NextUInt32() >> 5) * 67108864.0) + (NextUInt32() >> 6)) / 9007199254740992.0;
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            return minInclusive;
        }

        var span = maxInclusive - minInclusive + 1;
        return minInclusive + Mathf.Clamp((int)(NextDouble() * span), 0, span - 1);
    }

    public bool NextBool()
    {
        return NextInt(0, 1) == 1;
    }
}

private static uint CombineTreeSeed(long worldSeed, string dimensionId, string structureId, int worldX)
{
    unchecked
    {
        ulong hash = (ulong)worldSeed;
        hash = hash * 397u ^ (ulong)WorldRandom.StableStringHash(dimensionId);
        hash = hash * 397u ^ (ulong)WorldRandom.StableStringHash(structureId);
        hash = hash * 397u ^ (ulong)worldX;

        return (uint)(hash ^ (hash >> 32));
    }
}

private static int RoundToEven(float value)
{
    return (int)Math.Round(value, MidpointRounding.ToEven);
}
```

## `TreeStructureDefinition.cs` — export de preview em PNG (`ExportTreePreview` + `DrawOverlayText`)

Removido: chamado de dentro de `CollectCells()`, o MESMO método que `ChunkGenerator.
PlaceStructures` usa pra gerar árvore no jogo de verdade — não era só ferramenta de debug,
rodava durante o jogo normal (guardado só por `EnableTreePreviewExport && OS.IsDebugBuild()`,
com a flag `true` por padrão). Toda árvore gerada numa build de debug gravava um `.png` em
`res://.temp/godot/tree/`. Também duplicava o que `.dev/tune_hybrid_tree.py` já faz, sem custo
de I/O em produção.

```csharp
private const int PreviewTileSize = 16;

private static void ExportTreePreview(List<Vector2I> trunkCells, List<Vector2I> canopyCells, long worldSeed, string dimensionId, int worldX)
{
    if (!TreeDebugConstants.EnableTreePreviewExport || !OS.IsDebugBuild())
    {
        return;
    }

    var exportDirectory = ProjectSettings.GlobalizePath("res://.temp/godot/tree");
    Directory.CreateDirectory(exportDirectory);

    var left = 0;
    var right = 0;
    var top = 0;

    void Measure(Vector2I cell)
    {
        if (cell.X < 0) left = Mathf.Max(left, -cell.X);
        if (cell.X > 0) right = Mathf.Max(right, cell.X);
        top = Mathf.Max(top, cell.Y);
    }

    foreach (var cell in trunkCells) Measure(cell);
    foreach (var cell in canopyCells) Measure(cell);

    var widthTiles = left + right + 1;
    var heightTiles = top + 1;
    var imageWidth = widthTiles * PreviewTileSize;
    var imageHeight = heightTiles * PreviewTileSize;

    using var image = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
    image.Fill(new Color(0.117f, 0.117f, 0.133f));

    var trunkColor = new Color(121f / 255f, 74f / 255f, 43f / 255f);
    var trunkShade = new Color(98f / 255f, 58f / 255f, 33f / 255f);
    var leafColor = new Color(235f / 255f, 120f / 255f, 170f / 255f);
    var leafShade = new Color(214f / 255f, 92f / 255f, 148f / 255f);

    var shadeRng = new Random((int)(worldSeed * 7919 + worldX));
    foreach (var cell in trunkCells)
    {
        var fill = shadeRng.NextDouble() < 0.75 ? trunkColor : trunkShade;
        var px = (cell.X + left) * PreviewTileSize;
        var py = (top - cell.Y) * PreviewTileSize;
        image.FillRect(new Rect2I(px, py, PreviewTileSize, PreviewTileSize), fill);
    }

    foreach (var cell in canopyCells)
    {
        var fill = shadeRng.NextDouble() < 0.7 ? leafColor : leafShade;
        var px = (cell.X + left) * PreviewTileSize;
        var py = (top - cell.Y) * PreviewTileSize;
        image.FillRect(new Rect2I(px, py, PreviewTileSize, PreviewTileSize), fill);
    }

    var overlayText = worldX.ToString();
    var textPosition = new Vector2I(4, 4);
    DrawOverlayText(image, overlayText, textPosition, Colors.White, Colors.Black with { A = 0.75f }, 2);

    var safeDimensionId = dimensionId.Replace('/', '_').Replace('\\', '_');
    var fileName = $"tree_{safeDimensionId}_{worldSeed}_{worldX}.png";
    var path = Path.Combine(exportDirectory, fileName);
    var error = image.SavePng(path);

    if (error != Error.Ok)
    {
        GD.PrintErr($"[TreeStructureDefinition] Falha ao salvar preview {path}: {error}");
    }
}

private static void DrawOverlayText(Image image, string text, Vector2I position, Color color, Color background, int scale)
{
    var digitPatterns = new Dictionary<char, string[]>
    {
        ['0'] = new[] { "111", "101", "101", "101", "111" },
        ['1'] = new[] { "010", "110", "010", "010", "111" },
        ['2'] = new[] { "111", "001", "111", "100", "111" },
        ['3'] = new[] { "111", "001", "111", "001", "111" },
        ['4'] = new[] { "101", "101", "111", "001", "001" },
        ['5'] = new[] { "111", "100", "111", "001", "111" },
        ['6'] = new[] { "111", "100", "111", "101", "111" },
        ['7'] = new[] { "111", "001", "010", "010", "010" },
        ['8'] = new[] { "111", "101", "111", "101", "111" },
        ['9'] = new[] { "111", "101", "111", "001", "111" },
    };

    var digitWidth = 3 * scale;
    var digitHeight = 5 * scale;
    var spacing = scale;
    var textWidth = text.Length * digitWidth + Math.Max(0, text.Length - 1) * spacing;
    var textHeight = digitHeight;

    image.FillRect(new Rect2I(position.X - 2, position.Y - 2, textWidth + 4, textHeight + 4), background);

    var x = position.X;
    foreach (var character in text)
    {
        if (!digitPatterns.TryGetValue(character, out var pattern))
        {
            x += digitWidth + spacing;
            continue;
        }

        for (var row = 0; row < pattern.Length; row++)
        {
            for (var col = 0; col < pattern[row].Length; col++)
            {
                if (pattern[row][col] != '1')
                {
                    continue;
                }

                var px = x + col * scale;
                var py = position.Y + row * scale;
                image.FillRect(new Rect2I(px, py, scale, scale), color);
            }
        }

        x += digitWidth + spacing;
    }
}
```

## `ChunkGenerator.cs` — chamada de overlay de debug dentro de `PlaceStructures`

Removido junto com o sistema de anotação em `TerrainLayer` (próximo bloco). Rodava pra toda
estrutura colocada, sempre (guardado só por `TreeDebugConstants.EnableTreeDebugOverlay`,
`true` por padrão).

```csharp
if (target != null)
{
    if (structureId != "tree" || TreeDebugConstants.EnableTreeDebugOverlay)
    {
        var overlayText = structureId == "tree" ? column.WorldX.ToString() : $"{structureId}:{column.WorldX}";
        target.AddDebugOverlayAnnotation(new Vector2I(column.WorldX, column.GroundHeight), overlayText, Colors.White);
    }
}
```

E em `ChunkGenerator.Erase`:

```csharp
if (baseTarget is TerrainLayer terrainBaseLayer)
{
    terrainBaseLayer.RemoveDebugOverlayAnnotationsInRegion(
        new Vector2I(baseCellX, baseCellY),
        new Vector2I(baseCellX + chunkSize - 1, baseCellY + chunkSize - 1));
}
```

## `TerrainLayer.cs` — sistema de anotação de debug por estrutura

Removido: o toggle manual que já existia (`ShowTerrainSetDebug`, `[Export] bool` desligado por
padrão) tinha virado decorativo, porque `RedrawDebugOverlay` desenhava o overlay mesmo com ele
desligado, contanto que existisse qualquer anotação registrada — e `EnableTreeDebugOverlay`
sendo `true` por padrão garantia que sempre existia uma. `RedrawDebugOverlay` voltou a
depender só de `_showTerrainSetDebug`.

```csharp
private struct DebugOverlayAnnotation
{
    public Vector2I Cell;
    public string Text;
    public Color Color;
}

private readonly List<DebugOverlayAnnotation> _debugOverlayAnnotations = new();

public void AddDebugOverlayAnnotation(Vector2I cell, string text, Color color)
{
    _debugOverlayAnnotations.Add(new DebugOverlayAnnotation
    {
        Cell = cell,
        Text = text,
        Color = color,
    });

    RedrawDebugOverlay();
}

public void RemoveDebugOverlayAnnotationsInRegion(Vector2I minCell, Vector2I maxCell)
{
    if (_debugOverlayAnnotations.Count == 0)
    {
        return;
    }

    _debugOverlayAnnotations.RemoveAll(annotation =>
        annotation.Cell.X >= minCell.X &&
        annotation.Cell.X <= maxCell.X &&
        annotation.Cell.Y >= minCell.Y &&
        annotation.Cell.Y <= maxCell.Y);

    _debugOverlay?.QueueRedraw();
}

public void ClearDebugOverlayAnnotations()
{
    if (_debugOverlayAnnotations.Count == 0)
    {
        return;
    }

    _debugOverlayAnnotations.Clear();
    _debugOverlay?.QueueRedraw();
}

private void DrawStructureDebugAnnotations(CanvasItem target, Font font)
{
    if (_debugOverlayAnnotations.Count == 0)
    {
        return;
    }

    foreach (var annotation in _debugOverlayAnnotations)
    {
        var position = MapToLocal(annotation.Cell);
        target.DrawCircle(position, 4f, annotation.Color);
        target.DrawString(font, position + new Vector2(8f, 8f), annotation.Text, HorizontalAlignment.Left, -1, 14, annotation.Color);
    }
}
```

`RedrawDebugOverlay` (antes/depois) e `DebugOverlay._Draw()` (antes/depois) — a condição e o
draw voltaram a depender só do toggle:

```csharp
// ANTES
public void RedrawDebugOverlay()
{
    if (!_showTerrainSetDebug && _debugOverlayAnnotations.Count == 0)
    {
        _debugOverlay?.QueueRedraw();
        return;
    }
    ...
}

private partial class DebugOverlay : Node2D
{
    public override void _Draw()
    {
        if (TerrainLayerOwner == null) return;

        if (TerrainLayerOwner._showTerrainSetDebug)
        {
            TerrainLayerOwner.DrawTerrainSetDebug(this);
        }
        else
        {
            TerrainLayerOwner.DrawStructureDebugAnnotations(this, ThemeDB.FallbackFont);
        }
    }
}
```

## `ChunkGenerator.cs` — `CombineSeed` (codigo morto, nunca chamado)

Removido: `grep -rn "CombineSeed"` no projeto inteiro so encontrava a propria declaracao -
nenhum chamador. O seed do ruido de altura ja e calculado inline em
`ResolveSolidCellsByBiome` (`unchecked((long)worldSeed * 397 ^ WorldRandom.StableStringHash(dimensionId))`),
que cobre a mesma necessidade.

```csharp
private static long CombineSeed(long worldSeed, string dimensionId, Vector2I chunkCoord)
{
    unchecked
    {
        long hash = worldSeed;

        hash = hash * 397 ^ WorldRandom.StableStringHash(dimensionId);
        hash = hash * 397 ^ chunkCoord.X;
        hash = hash * 397 ^ chunkCoord.Y;

        return hash;
    }
}
```
