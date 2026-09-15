using Godot;
using System;
using Godot.Collections;

namespace Jogo25D.Testing
{
    // Pinta um bloco de cada terrain_set do tileset principal, para saber como cada um preenche.
    public partial class TerrainPalette : Node
    {
        public override async void _Ready()
        {
            try
            {
                var tileset = GD.Load<TileSet>("res://Assets/Textures/Tiles/world_biomes_tileset.tres");
                var layer = new TileMapLayer { TileSet = tileset, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
                var pass = new SubViewport { Size = new(1400, 320), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = new Color(0.35f, 0.55f, 0.8f) });
                pass.AddChild(layer);
                for (int set = 0; set < tileset.GetTerrainSetsCount(); set++)
                {
                    var cells = new Array<Vector2I>();
                    for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
                        cells.Add(new Vector2I(set * 8 + x, y));
                    layer.SetCellsTerrainConnect(cells, set, 0, false);
                }
                var camera = new Camera2D { Zoom = new Vector2(1.5f, 1.5f), Position = new Vector2(760, 48), Enabled = true };
                pass.AddChild(camera);
                camera.MakeCurrent();
                for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var frame = pass.GetTexture().GetImage();
                frame.SavePng(ProjectSettings.GlobalizePath("res://../.images/paleta-terrenos.png"));
                for (int set = 0; set < tileset.GetTerrainSetsCount(); set++)
                    GD.Print("set " + set + " = " + tileset.GetTerrainName(set, 0));
                GD.Print("PALETA OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
