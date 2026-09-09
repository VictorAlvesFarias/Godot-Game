using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        [ExportGroup("Sol")]
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;

        // Distancia da fonte: 0 = 64 tiles da referencia; 1 = infinito (raios paralelos).
        [Export(PropertyHint.Range, "0,1,0.01")] public float Penumbra { get; set; } = LightMapConstants.PENUMBRA;
        [Export] public Vector2 SunReferenceTiles { get; set; } = Vector2.Zero;
        // Tamanho fisico da fonte, independente da distancia e da resolucao da sombra.
        [Export(PropertyHint.Range, "0,8,0.05")] public float SunRadiusTiles { get; set; } = 1.5f;

        [ExportGroup("Ceu")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float AmbientInfluence { get; set; } = LightMapConstants.AMBIENT_INFLUENCE;

        [ExportGroup("Energia e cor")]
        [Export(PropertyHint.Range, "0,2,0.01")] public float SunIntensity { get; set; } = 0.85f;
        [Export] public Color SkyColor { get; set; } = new(0.72f, 0.83f, 1f);
        [Export] public Color SunColor { get; set; } = new(1f, 0.95f, 0.84f);

        [ExportGroup("Depuracao")]
        [Export] public bool ShowRawMap { get; set; } = false;
    }
}
