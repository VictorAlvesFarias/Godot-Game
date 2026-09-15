using Godot;
namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        [Export] public bool DebugShadow { get; set; } = false;
        [Export(PropertyHint.Range,"-89,89,0.5")] public float SunAngleDegrees { get; set; } = -10f;
        // Existing convention: zero opens the cone; one makes rays parallel.
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbra { get; set; } = 0f;
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbraShadowCurve { get; set; } = 1f;
        [Export(PropertyHint.Range,"0,1,0.01")] public float SunPenumbraAmbientCurve { get; set; } = 1f;
        [Export(PropertyHint.Range,"0,1,0.01")] public float ShadowStrength { get; set; } = 0.65f;
    }
}
