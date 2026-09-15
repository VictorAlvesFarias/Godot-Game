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

                var b = porCamada["Base"]; var c2 = porCamada["Compose"]; var w = porCamada["BackgroundWalls"];
                int ambos = 0; foreach (var cell in b) if (c2.Contains(cell)) ambos++;
                int soBase = 0; foreach (var cell in b) if (!c2.Contains(cell)) soBase++;
                int soCompose = 0; foreach (var cell in c2) if (!b.Contains(cell)) soCompose++;
                GD.Print("SOBREPOSICAO Base/Compose: nas duas=" + ambos + "  so Base=" + soBase + "  so Compose=" + soCompose);
                int wallSobreTerra = 0; foreach (var cell in w) if (b.Contains(cell)) wallSobreTerra++;
                GD.Print("Wall sobre celula de Base: " + wallSobreTerra + " de " + w.Count);

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
                // Arvore completa = copa (sheet) + o tronco de wood grudado nela, como uma peca so.
                var sheet = porFonte.GetValueOrDefault(7, new HashSet<Vector2I>());
                var madeira = porFonte.GetValueOrDefault(6, new HashSet<Vector2I>());
                var copas = Componentes(sheet).OrderByDescending(g => g.Count).ToList();
                var madeiras = Componentes(madeira).ToList();
                GD.Print("ARVORES: " + copas.Count + " copas, " + madeiras.Count + " grupos de madeira");
                foreach (var copa in copas)
                {
                    var copaSet = new HashSet<Vector2I>(copa);
                    var tronco = new List<Vector2I>();
                    foreach (var grupo in madeiras)
                    {
                        bool encosta = false;
                        foreach (var c in grupo)
                            for (int dx = -1; dx <= 1 && !encosta; dx++) for (int dy = -1; dy <= 1 && !encosta; dy++)
                                if (copaSet.Contains(new Vector2I(c.X + dx, c.Y + dy))) encosta = true;
                        if (encosta && grupo.Count < 30) tronco.AddRange(grupo);
                    }
                    var todas = new List<Vector2I>(copa); todas.AddRange(tronco);
                    int x0 = todas.Min(c => c.X), x1 = todas.Max(c => c.X);
                    int y0 = todas.Min(c => c.Y), y1 = todas.Max(c => c.Y);
                    int baseX = tronco.Count > 0 ? (int)Math.Round(tronco.Average(c => (double)c.X)) : (x0 + x1) / 2;
                    GD.Print("   --- arvore " + (x1 - x0 + 1) + "x" + (y1 - y0 + 1)
                        + "  ancora_x=" + (baseX - x0) + " ---");
                    for (int y = y0; y <= y1; y++)
                    {
                        var linha = "   ";
                        for (int x = x0; x <= x1; x++)
                        {
                            var c = new Vector2I(x, y);
                            linha += copaSet.Contains(c) ? "S" : tronco.Contains(c) ? "W" : ".";
                        }
                        GD.Print(linha);
                    }
                }
                GD.Print("INSPECT OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
