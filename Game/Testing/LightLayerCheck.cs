using Godot;
using System;

namespace Jogo25D.Testing
{
    // Confere de qual camada a sombra tira a geometria, resolvendo o export como o jogo resolve.
    public partial class LightLayerCheck : Node
    {
        public override void _Ready()
        {
            try
            {
                var packed = GD.Load<PackedScene>("res://Scenes/World/Levels/Upsidedown.tscn");
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var lightMap = root.GetNode("LightMap");
                var camadas = lightMap.Get("Layers").As<Godot.Collections.Array<NodePath>>();
                GD.Print("Layers declaradas: " + camadas.Count);
                foreach (var caminho in camadas)
                {
                    var alvo = lightMap.GetNodeOrNull<TileMapLayer>(caminho);
                    if (alvo == null) { GD.Print("  " + caminho + " -> NAO RESOLVE"); continue; }
                    int madeira = 0, folha = 0;
                    foreach (var c in alvo.GetUsedCells())
                    {
                        int fonte = alvo.GetCellSourceId(c);
                        if (fonte == 6) madeira++; else if (fonte == 7) folha++;
                    }
                    GD.Print("  " + caminho + " -> " + alvo.Name + ", " + alvo.GetUsedCells().Count
                        + " celulas (madeira=" + madeira + " folha=" + folha + ")");
                }
                GD.Print("CHECK OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
