using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    [GlobalClass]
    public partial class LightMapData : Resource
    {
        [ExportGroup("Sol")]
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;
        [Export] public Color SunLightColor { get; set; } = new(1f, 0.96f, 0.86f, 1f);
        [Export(PropertyHint.Range, "0,2,0.01")] public float SunIntensity { get; set; } = 1f;

        // Distancia aparente da fonte. 1 e sol distante com sombra seca; 0 abre a penumbra.
        [Export(PropertyHint.Range, "0,1,0.01")] public float Penumbra { get; set; } = LightMapConstants.PENUMBRA;

        [ExportGroup("Ceu")]
        [Export] public Color SkyLightColor { get; set; } = new(0.55f, 0.70f, 1f, 1f);
        [Export(PropertyHint.Range, "0,1,0.01")] public float AmbientInfluence { get; set; } = LightMapConstants.AMBIENT_INFLUENCE;

        [ExportGroup("Sombra")]
        [Export] public bool AirShadowEnabled { get; set; } = true;
        [Export] public bool SolidShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float AirShadowOpacity { get; set; } = LightMapConstants.AIR_SHADOW_OPACITY;
        [Export(PropertyHint.Range, "0,1,0.01")] public float SolidShadowOpacity { get; set; } = LightMapConstants.SOLID_SHADOW_OPACITY;

        // Quanto a luz perde ao atravessar materia ate uma face exposta. Todo tile usa o mesmo
        // material, como combinado.
        [Export(PropertyHint.Range, "0,2,0.01")] public float MaterialAbsorption { get; set; } = LightMapConstants.MATERIAL_ABSORPTION;

        [ExportGroup("Depuracao")]
        [Export] public bool ShowRawMap { get; set; } = false;
    }
}
