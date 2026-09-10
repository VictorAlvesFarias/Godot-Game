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
                var walls = shown.GetNode<Jogo25D.Blocks.BackgroundWallLayer>("BackgroundWalls");
                if (walls.CollisionEnabled || walls.NavigationEnabled || walls.OcclusionEnabled)
                    throw new Exception("Background walls enabled foreground physics/occlusion");
                var wallCell = new Vector2I(-3, -40);
                walls.RestoreChunk(new Vector2I(-1, -2));
                long opticalRevision = light.World.Revision;
                if (!walls.EditAuthoritative(wallCell, "wall_wood", false)) throw new Exception("Wall placement failed");
                if (walls.GetCellSourceId(wallCell) < 0) throw new Exception("Wall item not painted");
                var wallChunk = new Vector2I(-1, -2);
                walls.UnloadChunk(wallChunk);
                if (walls.GetCellSourceId(wallCell) >= 0) throw new Exception("Wall chunk stayed rendered");
                walls.RestoreChunk(wallChunk);
                if (walls.GetCellSourceId(wallCell) < 0) throw new Exception("Wall lost during chunk unload");
                if (!walls.EditAuthoritative(wallCell, "", true)) throw new Exception("Wall hammer removal failed");
                walls.UnloadChunk(wallChunk); walls.RestoreChunk(wallChunk);
                if (walls.GetCellSourceId(wallCell) >= 0) throw new Exception("Removed wall came back");
                if (light.World.Revision != opticalRevision) throw new Exception("Background wall blocked foreground light");
                bool savedPlace = false, savedBreak = false;
                foreach (var value in Game.Managers.TileStreamingManager.Node.ExportMutations("overworld"))
                {
                    var record = value.AsGodotDictionary();
                    savedPlace |= record["type"].AsString() == "wall_place";
                    savedBreak |= record["type"].AsString() == "wall_break";
                }
                if (!savedPlace || !savedBreak) throw new Exception("Wall edits missing from save mutations");
                var savedCell = wallCell + Vector2I.Right;
                walls.EditAuthoritative(savedCell, "wall_dirt", false);
                var savedWalls = Game.Managers.TileStreamingManager.Node.ExportMutations("overworld");
                walls.ReceiveEdit(savedCell, "", true);
                Game.Managers.TileStreamingManager.Node.ImportMutations("overworld", savedWalls);
                if (walls.GetCellSourceId(savedCell) < 0 || walls.GetCellSourceId(wallCell) >= 0)
                    throw new Exception("Wall save replay failed");
                walls.ClearRenderedForStreaming();
                walls.ReceiveEdit(new Vector2I(32000, 32000), "wall_wood", false);
                if (walls.GetCellSourceId(new Vector2I(32000, 32000)) >= 0)
                    throw new Exception("Remote wall rendered outside resident chunks");
                walls.RestoreChunk(new Vector2I(1000, 1000));
                if (walls.GetCellSourceId(new Vector2I(32000, 32000)) < 0)
                    throw new Exception("Remote logical wall was lost");
                walls.UnloadChunk(new Vector2I(1000, 1000));

                if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--walls") >= 0)
                {
                    var center = walls.LocalToMap(camera.Position);
                    for (int cy = (int)Math.Floor((center.Y - 10) / 32.0); cy <= (int)Math.Floor((center.Y + 10) / 32.0); cy++)
                    for (int cx = (int)Math.Floor((center.X - 14) / 32.0); cx <= (int)Math.Floor((center.X + 14) / 32.0); cx++)
                        walls.RestoreChunk(new Vector2I(cx, cy));
                    for (int y = center.Y - 10; y < center.Y + 10; y++)
                    for (int x = center.X - 14; x < center.X + 14; x++)
                        walls.ApplyMutation(new Jogo25D.Features.World.Chunks.Resources.ChunkMutationData
                        { Type = "wall_place", Position = new Vector2(x,y), ExtraData = "wall_wood" });
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                GD.Print("BACKGROUND WALLS PASS: placement, removal, chunk restore, save records, no optical obstruction");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    string folder = ProjectSettings.GlobalizePath("res://../.images");
                    DirAccess.MakeDirRecursiveAbsolute(folder);
                    GetViewport().GetTexture().GetImage().SavePng(folder + (Array.IndexOf(OS.GetCmdlineUserArgs(), "--walls") >= 0 ? "/lighting-background-walls.png" : procedural ? "/lighting-procedural.png" : "/lighting-authored.png"));
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
