using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Testing
{
    // Le a cena antiga e reporta o que cada camada realmente contem, e as formas de arvore e casa.
    public partial class UpsidedownInspect : Node
    {
        static string Fonte(int id) => id switch
        {
            0 => "lime_grass", 1 => "lime_bordercap", 2 => "lime_base",
            3 => "olive_grass", 4 => "olive_bordercap", 5 => "olive_base",
            6 => "wood", 7 => "sheet", _ => "?" + id
        };

        public override void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/UpsidedownOriginal.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var porCamada = new Dictionary<string, HashSet<Vector2I>>();
                foreach (var nome in new[] { "BackgroundWalls", "Base", "Compose" })
                {
                    var layer = root.GetNode<TileMapLayer>(nome);
                    var cells = layer.GetUsedCells();
                    var contagem = new Dictionary<int, int>();
                    var conjunto = new HashSet<Vector2I>();
                    foreach (var c in cells)
                    {
                        int fonte = layer.GetCellSourceId(c);
                        contagem[fonte] = contagem.GetValueOrDefault(fonte) + 1;
                        conjunto.Add(c);
                    }
                    porCamada[nome] = conjunto;
                    var resumo = string.Join("  ", contagem.OrderByDescending(p => p.Value)
                        .Select(p => Fonte(p.Key) + "=" + p.Value));
                    GD.Print("CAMADA " + nome + ": " + cells.Count + " celulas | " + resumo);
                }

                // Formas: componentes conexos de sheet (copas) no Compose, e de wood (casas/troncos).
                var compose = root.GetNode<TileMapLayer>("Compose");
                var porFonte = new Dictionary<int, HashSet<Vector2I>>();
                foreach (var c in compose.GetUsedCells())
                {
                    int f = compose.GetCellSourceId(c);
                    if (!porFonte.TryGetValue(f, out var s)) porFonte[f] = s = new();
                    s.Add(c);
                }
                List<List<Vector2I>> Componentes(HashSet<Vector2I> cells)
                {
                    var vistos = new HashSet<Vector2I>();
                    var saida = new List<List<Vector2I>>();
                    foreach (var inicio in cells)
                    {
                        if (!vistos.Add(inicio)) continue;
                        var fila = new Queue<Vector2I>();
                        var grupo = new List<Vector2I>();
                        fila.Enqueue(inicio);
                        while (fila.Count > 0)
                        {
                            var c = fila.Dequeue();
                            grupo.Add(c);
                            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                            {
                                var v = new Vector2I(c.X + dx, c.Y + dy);
                                if (cells.Contains(v) && vistos.Add(v)) fila.Enqueue(v);
                            }
                        }
                        saida.Add(grupo);
                    }
                    return saida;
                }
                foreach (var fonte in new[] { 7, 6 })
                {
                    if (!porFonte.TryGetValue(fonte, out var cells)) { GD.Print("sem " + Fonte(fonte)); continue; }
                    var grupos = Componentes(cells).OrderByDescending(g => g.Count).ToList();
                    GD.Print("FORMAS de " + Fonte(fonte) + ": " + grupos.Count + " grupos");
                    foreach (var g in grupos)
                    {
                        int x0 = g.Min(c => c.X), x1 = g.Max(c => c.X);
                        int y0 = g.Min(c => c.Y), y1 = g.Max(c => c.Y);
                        GD.Print("   " + g.Count + " celulas, caixa " + (x1 - x0 + 1) + "x" + (y1 - y0 + 1)
                            + " em (" + x0 + "," + y0 + ")");
                    }
                    // Desenha as tres maiores formas em texto, para eu copiar o formato exato.
                    foreach (var g in grupos)
                    {
                        int x0 = g.Min(c => c.X), x1 = g.Max(c => c.X);
                        int y0 = g.Min(c => c.Y), y1 = g.Max(c => c.Y);
                        var mapa = new HashSet<Vector2I>(g);
                        GD.Print("   --- forma " + Fonte(fonte) + " " + (x1 - x0 + 1) + "x" + (y1 - y0 + 1) + " ---");
                        for (int y = y0; y <= y1; y++)
                        {
                            var linha = "   ";
                            for (int x = x0; x <= x1; x++) linha += mapa.Contains(new Vector2I(x, y)) ? "#" : ".";
                            GD.Print(linha);
                        }
                    }
                }
                GD.Print("INSPECT OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
