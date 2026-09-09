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
        public override void _Process(double delta)
        {
            if (!Engine.IsEditorHint() || OS.GetEnvironment("LIGHTING_EDITOR_REGRESSION") != "1") return;
            if (++_frames > 1200) { GD.PushError("EDITOR LIGHTING TIMEOUT"); GetTree().Quit(1); return; }
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
                GD.Print("EDITOR LIGHTING PASS: inspector properties updated sun and ambient uniforms");
                _stage++;
                GetTree().Quit();
            }
        }
    }
}
