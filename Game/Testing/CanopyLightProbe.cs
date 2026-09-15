using Godot;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    // Mede o CAMPO DE LUZ dentro de uma copa solida 15x15, que e o que o layered_light desenha.
    // O preto do miolo vem daqui, nao da sombra projetada.
    public partial class CanopyLightProbe : Node
    {
        public override void _Ready()
        {
            try
            {
                foreach (float depth in new[] { 3f, 8f, 12f, 16f })
                {
                    var world = new LogicalLightWorld(0, "canopy-light", 1, false);
                    for (int y = 4; y < 19; y++) for (int x = 4; x < 19; x++) world.SetTerrain(x, y, 0);
                    world.Field.SetRegion(0, 0, 32, 32);
                    for (int i = 0; i < 4000 && !world.Field.Settled; i++) world.Field.Process(10000);

                    var computer = new LightMapComputer();
                    computer.Begin(world, Vector2I.Zero, new Vector2I(32, 32), 0, 1, depth, buildShadow: false);
                    while (!computer.Complete) computer.Process(10000);
                    using var image = computer.LightImage();
                    // Perfil horizontal no meio da copa (y=11), em subdivisoes de 2 por tile.
                    var perfil = "";
                    for (int x = 2; x < 22; x++)
                        perfil += ((int)(image.GetPixel(x * LightMapComputer.Subdivisions, 11 * LightMapComputer.Subdivisions).R * 99)).ToString("D2") + " ";
                    GD.Print("TerrainLightDepthTiles=" + depth + "  (00=preto, 99=claro)");
                    GD.Print("   " + perfil);
                }
                GD.Print("LIGHT PROBE OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
