using Godot;
using Jogo25D.Core;
using Jogo25D.Chunks;
using System;

namespace Jogo25D.Testing
{
    public partial class LightingIntegration : Node
    {
        public override async void _Ready()
        {
            try
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Game.Managers.WorldManager.Node.SpawnWorld();
                foreach (var child in GetParent().GetNode("Ui").GetChildren())
                    if (child is CanvasItem canvas) canvas.Visible = false;
                    else if (child is CanvasLayer layer) layer.Visible = false;
                bool procedural = Array.IndexOf(OS.GetCmdlineUserArgs(), "--procedural") >= 0;
                var dimensions = Game.Managers.DimensionManager.Node;
                var shown = dimensions.ResolveParent("overworld");
                var viewport = (SubViewport)shown.GetViewport();
                ((CanvasItem)viewport.GetParent()).Visible = true;
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                ((CanvasItem)dimensions.ResolveParent("upsidedown").GetViewport().GetParent()).Visible = false;
                var camera = shown.GetNode<Camera2D>("Camera2D");
                camera.SetPhysicsProcess(false);
                camera.Position = new(0, -80);
                if (procedural)
                {
                    dimensions.ClearLayers();
                    Game.Managers.TileStreamingManager.Node.SetWorldSeed(424242);
                    var generator = new ChunkGeneratorSystem();
                    for (int y = -2; y <= 1; y++) for (int x = -2; x <= 2; x++)
                        await generator.PaintTilesAsync(dimensions.ResolveLayer("overworld"), dimensions.ResolveBaseLayer("overworld"), 424242, "overworld", new(x, y), 32);
                    var logical = Game.Managers.LightMapManager.Node.GetWorld("overworld");
                    var rendered = dimensions.ResolveLayer("overworld");
                    for (int y = -64; y < 64; y++) for (int x = -64; x < 96; x++)
                        if ((logical.Opacity(x, y) > 0) != (rendered.GetCellSourceId(new(x, y)) >= 0))
                            throw new Exception($"Logical/rendered geometry mismatch {x},{y}");
                }
                else
                {
                    Game.Managers.LightMapManager.Node.UseAuthoredWorlds();
                    int highest = 0;
                    var layer = dimensions.ResolveLayer("overworld");
                    foreach (var cell in layer.GetUsedCells())
                        if (Math.Abs(cell.X) < 20) highest = Math.Min(highest, cell.Y);
                    camera.Position = layer.MapToLocal(new(0, highest + 12));
                }
                var light = Game.Managers.LightMapManager.Node.Nodes["overworld"];
                int frames = 0;
                do { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                while (!light.PresentationReady && ++frames < 600);
                if (!light.PresentationReady) throw new Exception("Light presentation did not settle");
                long revision = light.World.Field.Revision;
                for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (light.World.Field.Revision != revision) throw new Exception("Static world lighting did not stay idle");
                GD.Print($"LIGHTING INTEGRATION {(procedural ? "procedural" : "authored")}: ready in {frames} frames, {light.World.Field.ResidentChunks} light chunks");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    string folder = ProjectSettings.GlobalizePath("res://../.images");
                    DirAccess.MakeDirRecursiveAbsolute(folder);
                    GetViewport().GetTexture().GetImage().SavePng(folder + (procedural ? "/lighting-procedural.png" : "/lighting-authored.png"));
                }
                var terrainLayer = dimensions.ResolveLayer("overworld");
                var editCell = terrainLayer.LocalToMap(camera.Position) + new Vector2I(0, -4);
                while (terrainLayer.GetCellSourceId(editCell) >= 0) editCell += Vector2I.Up;
                Jogo25D.Blocks.BlockDB.TryGet("wood", out var wood);
                long processed = light.World.Field.ProcessedCells;
                terrainLayer.PlaceBlock(editCell, wood);
                frames = 0;
                do { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                while (!light.PresentationReady && ++frames < 180);
                if (!light.PresentationReady || light.World.Opacity(editCell.X, editCell.Y) != 255)
                    throw new Exception("Placed block was not reflected in lighting");
                GD.Print($"LIGHTING EDIT: {frames} frames, {light.World.Field.ProcessedCells - processed} processed cells");
                terrainLayer.EraseBlockAndReconnect(editCell);
                frames = 0;
                do { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                while (!light.PresentationReady && ++frames < 180);
                if (!light.PresentationReady || light.World.Opacity(editCell.X, editCell.Y) != 0)
                    throw new Exception("Removed block remained in lighting");
                if (procedural)
                {
                    byte obstruction = light.World.Opacity(0, 20);
                    revision = light.World.Field.Revision;
                    await new ChunkGeneratorSystem().EraseTilesAsync(terrainLayer, dimensions.ResolveBaseLayer("overworld"), Vector2I.Zero, 32);
                    if (light.World.Opacity(0, 20) != obstruction || light.World.Field.Revision != revision)
                        throw new Exception("Render chunk unload modified logical lighting");
                }
                var settings = (Jogo25D.Light.LightMapData)light.Settings;
                float oldAngle = settings.SunAngleDegrees;
                long solarUpdates = light.SolarUpdates;
                for (int i = 0; i < 30; i++)
                {
                    settings.SunAngleDegrees += 0.2f;
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (light.SolarUpdates == solarUpdates) throw new Exception("Moving sun starved presentation");
                settings.SunAngleDegrees = oldAngle;
                GD.Print("LIGHTING INTEGRATION PASS");
                GetTree().Quit();
            }
            catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        }
    }
}
