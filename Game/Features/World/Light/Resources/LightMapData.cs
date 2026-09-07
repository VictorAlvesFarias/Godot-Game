using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    [GlobalClass]
    public partial class LightMapData : Resource
    {
        [ExportGroup("Sol")]
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;

        // Distancia aparente da fonte. 1 e sol distante com sombra seca; 0 abre a penumbra.
        [Export(PropertyHint.Range, "0,1,0.01")] public float Penumbra { get; set; } = LightMapConstants.PENUMBRA;

        [ExportGroup("Ceu")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float AmbientInfluence { get; set; } = LightMapConstants.AMBIENT_INFLUENCE;

        [ExportGroup("Sombra")]
        [Export] public bool AirShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float AirShadowOpacity { get; set; } = LightMapConstants.AIR_SHADOW_OPACITY;

        [ExportGroup("Depuracao")]
        [Export] public bool ShowRawMap { get; set; } = false;
    }
}
