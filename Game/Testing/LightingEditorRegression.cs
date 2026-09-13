using Godot;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    [Tool]
    public partial class LightingEditorRegression : Node
    {
        private int _stage, _frames;
        private long _updates;
        private LightMap2D _light;
        private TileMapLayer _editLayer;
        private Vector2I _cell, _atlas;
        private int _source, _alternative, _terrain;
        private LogicalLightWorld _world;
        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint() || OS.GetEnvironment("LIGHTING_EDITOR_REGRESSION") != "1") return;
            if (++_frames > 1200) { GD.PushError($"EDITOR LIGHTING TIMEOUT stage={_stage}, updates={_light?.SolarUpdates}, expected>{_updates}, ready={_light?.PresentationReady}"); GetTree().Quit(1); return; }
            _light ??= GetParent().GetNodeOrNull<LightMap2D>("LightMap");
            if (_light == null || !_light.PresentationReady) return;
            if (_stage == 0)
            {
                var walls = GetParent().GetNodeOrNull<Jogo25D.Blocks.BackgroundWallLayer>("BackgroundWalls");
                if (walls == null || walls.CollisionEnabled) throw new Exception("Editor background layer missing or solid");
                walls.SetCell(new Vector2I(1000, -1000), 0, new Vector2I(1,1));
                _light.Settings = (Resource)_light.Settings.Duplicate();
                _updates = _light.SolarUpdates;
                _light.Settings.Set(nameof(LightMapData.SunAngleDegrees), 24f);
                _stage++;
            }
            else if (_stage == 1 && _light.SolarUpdates > _updates)
            {
                if (_light.World.Opacity(1000, -1000) != 0) throw new Exception("Editor wall became foreground occluder");
                var pass = _light.GetNode<SubViewport>("LightMapGpuPass");
                var material = (ShaderMaterial)pass.GetChild<ColorRect>(0).Material;
                if (Math.Abs(material.GetShaderParameter("sun_angle").AsDouble() - Mathf.DegToRad(24)) > 0.001) return;
                _light.Settings.Set(nameof(LightMapData.GlobalLightIntensity), 0.12f);
                _stage++;
            }
            else if (_stage == 2)
            {
                var material = (ShaderMaterial)_light.GetNode<Sprite2D>("LightMapOverlay").Material;
                if (Math.Abs(material.GetShaderParameter("ambient_energy").AsDouble() - 0.12) > 0.001) return;
                _updates = _light.SolarUpdates;
                _light.Settings.Set(nameof(LightMapData.SunPenumbra), 0.25f);
                _light.Settings.Set(nameof(LightMapData.SunPenumbraShadowCurve), 0.25f);
                _light.Settings.Set(nameof(LightMapData.SunPenumbraAmbientCurve), 0.5f);
                _light.Settings.Set(nameof(LightMapData.TerrainLightDepthTiles), 6f);
                _light.Settings.Set(nameof(LightMapData.AirShadowStrength), 0.6f);
                _light.Settings.Set(nameof(LightMapData.BeamReachTiles), 18f);
                _light.Settings.Set(nameof(LightMapData.DustDensity), 0.3f);
                _stage++;
            }
            else if (_stage == 3 && _light.SolarUpdates > _updates)
            {
                var pass = _light.GetNode<SubViewport>("LightMapGpuPass");
                var material = (ShaderMaterial)pass.GetChild<ColorRect>(0).Material;
                if (Math.Abs(material.GetShaderParameter("penumbra").AsDouble() - 0.25) > 0.001) return;
                if (Math.Abs(material.GetShaderParameter("terrain_transition_tiles").AsDouble() - 6) > 0.001) return;
                var presentation = (ShaderMaterial)_light.GetNode<Sprite2D>("LightMapOverlay").Material;
                if (Math.Abs(presentation.GetShaderParameter("terrain_transition_tiles").AsDouble() - 6) > 0.001) return;
                var volume = (ShaderMaterial)_light.GetNode<Sprite2D>("WindowVolume").Material;
                foreach (var shader in new[] { presentation, volume })
                    if (Math.Abs(shader.GetShaderParameter("shadow_air").AsDouble() - 0.6) > 0.001
                        || Math.Abs(shader.GetShaderParameter("volumetric_reach").AsDouble() - 18) > 0.001
                        || Math.Abs(shader.GetShaderParameter("volume_density").AsDouble() - 0.3) > 0.001) return;
                foreach (var shader in new[] { material, presentation })
                    if (Math.Abs(shader.GetShaderParameter("penumbra_shadow_transition").AsDouble() - 0.25) > 0.001
                        || Math.Abs(shader.GetShaderParameter("penumbra_ambient_transition").AsDouble() - 0.5) > 0.001) return;
                if (Math.Abs(material.GetShaderParameter("sun_angle").AsDouble() - Mathf.DegToRad(24)) > 0.001)
                    throw new Exception("Penumbra changed SunAngle");
                foreach (var node in GetParent().GetChildren())
                    if (node is TileMapLayer layer && layer.Name != "Base" && layer is not Jogo25D.Blocks.BackgroundWallLayer && layer.GetUsedCells().Count > 0)
                    { _editLayer = layer; break; }
                if (_editLayer == null) throw new Exception("No editor tile fixture");
                _cell = _editLayer.GetUsedCells()[0];
                _source = _editLayer.GetCellSourceId(_cell);
                _atlas = _editLayer.GetCellAtlasCoords(_cell);
                _alternative = _editLayer.GetCellAlternativeTile(_cell);
                _world = _light.World;
                _terrain = _world.Terrain(_cell.X, _cell.Y);
                _updates = _light.SolarUpdates;
                _editLayer.EraseCell(_cell);
                _stage++;
            }
            else if (_stage == 4 && _light.SolarUpdates > _updates)
            {
                if (_light.World != _world || _world.Terrain(_cell.X, _cell.Y) != -1)
                    throw new Exception("TileMap erase did not incrementally update editor lighting");
                _updates = _light.SolarUpdates;
                _editLayer.SetCell(_cell, _source, _atlas, _alternative);
                _stage++;
            }
            else if (_stage == 5 && _light.SolarUpdates > _updates)
            {
                if (_light.World != _world || _world.Terrain(_cell.X, _cell.Y) != _terrain)
                    throw new Exception("TileMap restore did not update editor lighting");
                foreach (string name in new[] { nameof(LightMapData.GlobalLightEnabled), nameof(LightMapData.SunEnabled),
                    nameof(LightMapData.EmissionEnabled), nameof(LightMapData.DepthEnabled),
                    nameof(LightMapData.BeamEnabled), nameof(LightMapData.DustEnabled), nameof(LightMapData.AirShadowEnabled),
                    nameof(LightMapData.TerrainShadowEnabled), nameof(LightMapData.BackgroundShadowEnabled),
                    nameof(LightMapData.EntityShadowEnabled), nameof(LightMapData.TerrainLightEnabled),
                    nameof(LightMapData.EdgeFadeEnabled) })
                    _light.Settings.Set(name, false);
                _stage++;
            }
            else if (_stage == 6)
            {
                // A cena procedural nao tem fonte local nem abertura de fundo, entao emissao, feixe
                // e volume nao aparecem la. A fiacao deles se verifica aqui.
                var presentation = (ShaderMaterial)_light.GetNode<Sprite2D>("LightMapOverlay").Material;
                if (presentation.GetShaderParameter("ambient_energy").AsDouble() != 0
                    || presentation.GetShaderParameter("sun_energy").AsDouble() != 0
                    || presentation.GetShaderParameter("emission_energy").AsDouble() != 0
                    || presentation.GetShaderParameter("shadow_air").AsDouble() != 0
                    || presentation.GetShaderParameter("shadow_terrain").AsDouble() != 0
                    || presentation.GetShaderParameter("shadow_background").AsDouble() != 0
                    || presentation.GetShaderParameter("edge_fade_tiles").AsDouble() != 0
                    || presentation.GetShaderParameter("terrain_light_enabled").AsBool()
                    || presentation.GetShaderParameter("depth_beam_enabled").AsBool()) return;
                if (_light.GetNode<Sprite2D>("WindowVolume").Visible)
                    throw new Exception("DustEnabled did not hide the additive overlay");
                if (_light.World.DepthLightEnabled)
                    throw new Exception("DepthEnabled did not reach the logical world");
                GD.Print("EDITOR LIGHTING PASS: angle preserved, penumbra updated, TileMap erase/restore incremental, every light toggle wired");
                _stage++;
                GetTree().Quit();
            }
        }
    }
}
