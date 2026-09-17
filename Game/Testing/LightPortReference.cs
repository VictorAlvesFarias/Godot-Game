using Godot;
using Jogo25D.Biomes;
using Jogo25D.Constants;
using Jogo25D.Light;
using System;
using System.IO;

namespace Jogo25D.Testing
{
    // Referencia exata do porte: roda o pipeline da copia (CPU) sobre a Upsidedown e grava o grid
    // de luz cru. A versao GPU tem que reproduzir este arquivo; e o criterio de "nao mudei regra".
    public partial class LightPortReference : Node
    {
        public override void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var compose = root.GetNode<TerrainLayer>("Compose");
                var baseLayer = root.GetNode<TerrainLayer>("Base");

                var used = compose.GetUsedRect().Merge(baseLayer.GetUsedRect());
                int padding = LightingConstants.CHUNK_PADDING;
                var region = new Rect2I(used.Position - new Vector2I(padding, padding),
                    used.Size + new Vector2I(padding * 2, padding * 2));
                GD.Print("REGIAO " + region);

                bool IsSolid(Vector2I cell) =>
                    compose.GetCellSourceId(cell) != -1 || baseLayer.GetCellSourceId(cell) != -1;

                var index = LightSourceScanner.BuildLightEmittingBlockIndex();
                var relogio = Time.GetTicksUsec();
                var sources = LightSourceScanner.CollectSources(compose, baseLayer, region, index, IsSolid);
                var grid = LightPropagationSystem.Compute(region, IsSolid, sources);
                GD.Print("CPU " + ((Time.GetTicksUsec() - relogio) / 1000.0).ToString("F1") + " ms, fontes=" + sources.Count);

                var image = Image.CreateEmpty(region.Size.X, region.Size.Y, false, Image.Format.Rgba8);
                for (int x = 0; x < region.Size.X; x++)
                for (int y = 0; y < region.Size.Y; y++)
                {
                    var v = grid[x, y];
                    image.SetPixel(x, y, new Color(
                        Mathf.Max(v.R, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(v.G, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(v.B, LightingConstants.AMBIENT_MIN)));
                }
                var destino = ProjectSettings.GlobalizePath("res://../.images/luz-referencia-cpu.png");
                image.SavePng(destino);
                GD.Print("GRAVADO " + destino + " " + region.Size);

                // Assinatura numerica, pra comparar sem depender de visualizacao.
                double soma = 0; float maximo = 0;
                for (int x = 0; x < region.Size.X; x++)
                for (int y = 0; y < region.Size.Y; y++)
                { var c = image.GetPixel(x, y); soma += c.R; maximo = Math.Max(maximo, c.R); }
                GD.Print("SOMA_R=" + soma.ToString("F3") + " MAX_R=" + maximo.ToString("F4"));
                GD.Print("REFERENCIA OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
