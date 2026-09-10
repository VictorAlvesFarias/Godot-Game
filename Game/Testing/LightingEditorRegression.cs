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
                _light.Settings = (Resource)_light.Settings.Duplicate();
                _updates = _light.SolarUpdates;
                _light.Settings.Set(nameof(LightMapData.SunAngleDegrees), 24f);
                _stage++;
            }
            else if (_stage == 1 && _light.SolarUpdates > _updates)
            {
                var pass = _light.GetNode<SubViewport>("LightMapGpuPass");
                var material = (ShaderMaterial)pass.GetChild<ColorRect>(0).Material;
                if (Math.Abs(material.GetShaderParameter("sun_angle").AsDouble() - Mathf.DegToRad(24)) > 0.001) return;
                _light.Settings.Set(nameof(LightMapData.AmbientInfluence), 0.12f);
                _stage++;
            }
            else if (_stage == 2)
            {
                var material = (ShaderMaterial)_light.GetNode<Sprite2D>("LightMapOverlay").Material;
                if (Math.Abs(material.GetShaderParameter("ambient_energy").AsDouble() - 0.12) > 0.001) return;
                _updates = _light.SolarUpdates;
                _light.Settings.Set(nameof(LightMapData.Penumbra), 0.25f);
                _light.Settings.Set(nameof(LightMapData.PenumbraGradient), 2f);
                _light.Settings.Set(nameof(LightMapData.PenumbraShadowSoftness), 3f);
                _light.Settings.Set(nameof(LightMapData.PenumbraAmbientSoftness), 0.5f);
                _light.Settings.Set(nameof(LightMapData.TerrainTransitionTiles), 6f);
                _stage++;
            }
            else if (_stage == 3 && _light.SolarUpdates > _updates)
            {
                var pass = _light.GetNode<SubViewport>("LightMapGpuPass");
                var material = (ShaderMaterial)pass.GetChild<ColorRect>(0).Material;
                if (Math.Abs(material.GetShaderParameter("penumbra").AsDouble() - 0.25) > 0.001) return;
                if (Math.Abs(material.GetShaderParameter("penumbra_gradient").AsDouble() - 2) > 0.001
                    || Math.Abs(material.GetShaderParameter("terrain_transition_tiles").AsDouble() - 6) > 0.001) return;
                var presentation = (ShaderMaterial)_light.GetNode<Sprite2D>("LightMapOverlay").Material;
                if (Math.Abs(presentation.GetShaderParameter("penumbra_gradient").AsDouble() - 2) > 0.001
                    || Math.Abs(presentation.GetShaderParameter("terrain_transition_tiles").AsDouble() - 6) > 0.001) return;
                foreach (var shader in new[] { material, presentation })
                    if (Math.Abs(shader.GetShaderParameter("penumbra_shadow_softness").AsDouble() - 3) > 0.001
                        || Math.Abs(shader.GetShaderParameter("penumbra_ambient_softness").AsDouble() - 0.5) > 0.001) return;
                if (Math.Abs(material.GetShaderParameter("sun_angle").AsDouble() - Mathf.DegToRad(24)) > 0.001)
                    throw new Exception("Penumbra changed SunAngle");
                foreach (var node in GetParent().GetChildren())
                    if (node is TileMapLayer layer && layer.Name != "Base" && layer.GetUsedCells().Count > 0)
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
                GD.Print("EDITOR LIGHTING PASS: angle preserved, penumbra updated, TileMap erase/restore incremental");
                _stage++;
                GetTree().Quit();
            }
        }
    }
}
