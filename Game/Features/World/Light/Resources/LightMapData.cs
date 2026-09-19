using Godot;
namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        private float _terrainLightDepthTiles = 3f;
        private bool _debugShadow = false;
        private Color _sunColor = Colors.White;
        private float _sunAngleDegrees = -10f;
        private float _sunPenumbra = 0f;
        private float _sunPenumbraShadowCurve = 1f;
        private float _sunPenumbraAmbientCurve = 1f;
        private float _ambientLightInfluence=0.75f;
        [Export(PropertyHint.Range,"0,1,0.01")] public float AmbientLightInfluence { get => _ambientLightInfluence; set { if(_ambientLightInfluence==value) return; _ambientLightInfluence=value; EmitChanged(); } }
        private float _shadowStrength = 0.65f;
        [Export(PropertyHint.Range,"0.25,16,0.25")] public float TerrainLightDepthTiles { get => _terrainLightDepthTiles; set { if (_terrainLightDepthTiles == value) return; _terrainLightDepthTiles=value; EmitChanged(); } }
        [Export] public bool DebugShadow { get => _debugShadow; set { if (_debugShadow == value) return; _debugShadow=value; EmitChanged(); } }
        [Export] public Color SunColor { get => _sunColor; set { if (_sunColor == value) return; _sunColor=value; EmitChanged(); } }
        [Export(PropertyHint.Range,"-89,89,0.5")] public float SunAngleDegrees { get => _sunAngleDegrees; set { if (_sunAngleDegrees == value) return; _sunAngleDegrees=value; EmitChanged(); } }
        // Existing convention: zero opens the cone; one makes rays parallel.
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbra { get => _sunPenumbra; set { if (_sunPenumbra == value) return; _sunPenumbra=value; EmitChanged(); } }
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbraShadowCurve { get => _sunPenumbraShadowCurve; set { if (_sunPenumbraShadowCurve == value) return; _sunPenumbraShadowCurve=value; EmitChanged(); } }
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbraAmbientCurve { get => _sunPenumbraAmbientCurve; set { if (_sunPenumbraAmbientCurve == value) return; _sunPenumbraAmbientCurve=value; EmitChanged(); } }
        [Export(PropertyHint.Range,"0,1,0.01")] public float ShadowStrength { get => _shadowStrength; set { if (_shadowStrength == value) return; _shadowStrength=value; EmitChanged(); } }
    }
}
