using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        [ExportGroup("Sol")]
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;

        // Abertura simetrica em torno de SunAngle: 0 = aberta; 1 = paralela.
        [Export(PropertyHint.Range, "0,1,0.01")] public float Penumbra { get; set; } = LightMapConstants.PENUMBRA;

        // Curva de intensidade em toda a faixa: 0 = borda marcada; 1 = chegada suave.
        // Nao altera largura, inicio ou fim da penumbra.
        [Export(PropertyHint.Range, "0,1,0.01")] public float PenumbraShadowTransition { get; set; } = 1f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PenumbraAmbientTransition { get; set; } = 1f;

        [ExportGroup("Transicao do terreno")]
        // Distancia visual ate escurecer totalmente. Vale para todos os blocos.
        [Export(PropertyHint.Range, "0.25,16,0.25")] public float TerrainTransitionTiles { get; set; } = 3f;

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
