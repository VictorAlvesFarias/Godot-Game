using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Light;
using System;
using System.Collections.Generic;

namespace Jogo25D.Testing
{
    // Imprime o valor da luz descendo por colunas a partir da superficie, e quanto do terreno
    // visivel e solido. Serve pra comparar dois projetos sem depender de olhar foto.
    public partial class LightDepthProfile : Node
    {
        public override void _Ready()
        {
            try
            {
                var caminho = OS.GetCmdlineUserArgs().Length > 0
                    ? OS.GetCmdlineUserArgs()[0]
                    : "res://Scenes/World/Levels/Upsidedown.tscn";
                var root = GD.Load<PackedScene>(caminho).Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var compose = root.GetNode<TerrainLayer>("Compose");
                var baseLayer = root.GetNode<TerrainLayer>("Base");

                var used = compose.GetUsedRect().Merge(baseLayer.GetUsedRect());
                int padding = LightingConstants.CHUNK_PADDING;
                var region = new Rect2I(used.Position - new Vector2I(padding, padding),
                    used.Size + new Vector2I(padding * 2, padding * 2));

                bool IsSolid(Vector2I cell) =>
                    compose.GetCellSourceId(cell) != -1 || baseLayer.GetCellSourceId(cell) != -1;

                int solidas = 0, total = used.Size.X * used.Size.Y;
                for (int x = used.Position.X; x < used.End.X; x++)
                for (int y = used.Position.Y; y < used.End.Y; y++)
                    if (IsSolid(new Vector2I(x, y))) solidas++;

                GD.Print("CENA " + caminho);
                GD.Print("AREA " + used + "  solidas=" + solidas + " de " + total
                    + " (" + (100.0 * solidas / total).ToString("F1") + "%)");
                GD.Print("CONSTANTES ar=" + LightingConstants.AIR_FALLOFF + " solido=" + LightingConstants.SOLID_FALLOFF
                    + " limiar=" + LightingConstants.MIN_LIGHT_THRESHOLD + " ambiente=" + LightingConstants.AMBIENT_MIN);

                var sources = LightSourceScanner.CollectSources(compose, baseLayer, region,
                    LightSourceScanner.BuildLightEmittingBlockIndex(), IsSolid);
                var grid = LightPropagationSystem.Compute(region, IsSolid, sources);

                // Profundidade media ate a luz cair abaixo do piso de ambiente.
                var somaProfundidade = 0;
                var colunas = 0;
                for (int x = used.Position.X; x < used.End.X; x += 7)
                {
                    int topo = int.MinValue;
                    for (int y = used.Position.Y; y < used.End.Y; y++)
                        if (IsSolid(new Vector2I(x, y))) { topo = y; break; }
                    if (topo == int.MinValue) continue;

                    int profundidade = 0;
                    var linha = "";
                    for (int d = 0; d < 10; d++)
                    {
                        var local = new Vector2I(x, topo + d) - region.Position;
                        if (local.Y >= region.Size.Y) break;
                        float v = grid[local.X, local.Y].R;
                        if (d < 7) linha += v.ToString("F3") + " ";
                        if (v >= LightingConstants.AMBIENT_MIN) profundidade = d + 1;
                    }
                    if (colunas < 6) GD.Print("   coluna x=" + x + " topo_y=" + topo + " : " + linha);
                    somaProfundidade += profundidade;
                    colunas++;
                }
                GD.Print("PROFUNDIDADE media ate o piso de ambiente: "
                    + (somaProfundidade / (double)Math.Max(colunas, 1)).ToString("F2") + " tiles  (" + colunas + " colunas)");
                GD.Print("PERFIL OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
