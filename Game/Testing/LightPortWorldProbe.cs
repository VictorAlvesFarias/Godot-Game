using Godot;
using Jogo25D.Core;
using System;
using System.Linq;

namespace Jogo25D.Testing
{
    // Sobe o mundo autoral de verdade e reporta o que o LightingManager produziu: raiz de overlay,
    // quantos chunks viraram sprite, e um print da tela. Sem isso so da pra adivinhar por que a luz
    // nao aparece.
    public partial class LightPortWorldProbe : Node
    {
        public override async void _Ready()
        {
            try
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Game.Managers.WorldManager.Node.SpawnWorld();
                Game.Managers.LightMapManager.Node.UseAuthoredWorlds();

                var streaming = Game.Managers.TileStreamingManager.Node;
                if (streaming != null) streaming.Enabled = false;

                var dims = Game.Managers.DimensionManager.Node;
                GD.Print("DIMENSOES: " + string.Join(", ", dims.Ids));
                GD.Print("TILE_SIZE=" + dims.TileSize + "  streaming_enabled=" + (streaming?.Enabled.ToString() ?? "null"));
                GD.Print("LIGHTING_MANAGER=" + (Game.Managers.LightingManager.Node?.Name.ToString() ?? "NULO"));

                for (int i = 0; i < 240; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                foreach (var id in dims.Ids)
                {
                    var parent = dims.ResolveParent(id);
                    var overlayRoot = parent?.GetNodeOrNull<Node2D>("LightOverlay");
                    var layer = dims.ResolveLayer(id);
                    GD.Print("--- " + id
                        + " | parent=" + (parent?.Name.ToString() ?? "NULO")
                        + " | LightOverlay=" + (overlayRoot == null ? "NAO EXISTE" : overlayRoot.GetChildCount() + " sprites")
                        + " | compose=" + (layer == null ? "NULO" : layer.GetUsedRect().ToString()));

                    if (overlayRoot != null && overlayRoot.GetChildCount() > 0)
                    {
                        var primeiro = (Sprite2D)overlayRoot.GetChild(0);
                        GD.Print("      sprite0 nome=" + primeiro.Name + " pos=" + primeiro.Position
                            + " escala=" + primeiro.Scale + " visivel=" + primeiro.Visible
                            + " textura=" + (primeiro.Texture == null ? "NULA" : primeiro.Texture.GetSize().ToString())
                            + " material=" + (primeiro.Material == null ? "NULO" : "ok")
                            + " indice_na_arvore=" + primeiro.GetParent().GetIndex() + "/" + parent.GetChildCount());
                    }
                }

                var mostrar = dims.ResolveParent("upsidedown");
                var viewport = (SubViewport)mostrar.GetViewport();
                ((CanvasItem)viewport.GetParent()).Visible = true;
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                ((CanvasItem)dims.ResolveParent("overworld").GetViewport().GetParent()).Visible = false;
                foreach (var child in GetParent().GetNode("Ui").GetChildren())
                {
                    if (child is CanvasItem ci) ci.Visible = false;
                    else if (child is CanvasLayer cl) cl.Visible = false;
                }

                var grid = dims.ResolveLayer("upsidedown");
                var camera = mostrar.GetNode<Camera2D>("Camera2D");
                camera.SetPhysicsProcess(false);
                camera.GlobalPosition = grid.ToGlobal(grid.MapToLocal(new Vector2I(2, -25)));
                camera.Zoom = new Vector2(2, 2);

                var overlayRootUp = mostrar.GetNodeOrNull<Node2D>("LightOverlay");
                var lightMap = mostrar.GetNodeOrNull<Jogo25D.Light.LightMap2D>("LightMap");

                async System.Threading.Tasks.Task Capturar(string nome)
                {
                    for (int i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage()
                        .SavePng(ProjectSettings.GlobalizePath("res://../.images/" + nome + ".png"));
                    GD.Print("GRAVADO " + nome + ".png");
                }

                // 1) so a luz portada da copia
                if (overlayRootUp != null) overlayRootUp.Visible = true;
                if (lightMap != null) lightMap.LightOverlayEnabled = false;
                await Capturar("comparacao-copia-portada");

                // 2) so a luz antiga deste projeto
                if (overlayRootUp != null) overlayRootUp.Visible = false;
                if (lightMap != null) lightMap.LightOverlayEnabled = true;
                await Capturar("comparacao-sistema-antigo");

                // 3) sem luz nenhuma, pra ver o terreno cru
                if (lightMap != null) lightMap.LightOverlayEnabled = false;
                await Capturar("comparacao-sem-luz");
                GD.Print("PROBE OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
