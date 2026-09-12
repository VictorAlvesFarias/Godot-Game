using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Light
{
    /// <summary>
    /// Um grupo por tipo de iluminacao. Cada grupo tem o proprio interruptor e os proprios
    /// parametros; o interruptor e mudo, nao zera o valor ajustado. O prefixo declarado em
    /// ExportGroup some do nome exibido, entao dentro de "Luz global" le-se "Enabled"/"Intensity".
    /// </summary>
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        // ------------------------------------------------------------------ luz global
        [ExportGroup("Luz global", "GlobalLight")]
        [Export] public bool GlobalLightEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float GlobalLightIntensity { get; set; } = LightMapConstants.AMBIENT_INFLUENCE;
        [Export] public Color GlobalLightColor { get; set; } = new(0.72f, 0.83f, 1f);

        // ------------------------------------------------------------------ luz direcionada
        [ExportGroup("Luz direcionada", "Sun")]
        [Export] public bool SunEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "-180,180,0.5")] public float SunAngleDegrees { get; set; } = -35f;
        [Export(PropertyHint.Range, "0,2,0.01")] public float SunIntensity { get; set; } = 0.85f;
        [Export] public Color SunColor { get; set; } = new(1f, 0.95f, 0.84f);
        // Abertura simetrica em torno do angulo: 0 = cone aberto; 1 = projecao paralela.
        [Export(PropertyHint.Range, "0,1,0.01")] public float SunPenumbra { get; set; } = LightMapConstants.PENUMBRA;
        // Curvas de intensidade dentro da penumbra. Nao alteram largura, inicio nem fim dela.
        [Export(PropertyHint.Range, "0,1,0.01")] public float SunPenumbraShadowCurve { get; set; } = 1f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float SunPenumbraAmbientCurve { get; set; } = 1f;

        // ------------------------------------------------------------------ emissao
        [ExportGroup("Emissao de outras fontes", "Emission")]
        [Export] public bool EmissionEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,2,0.01")] public float EmissionIntensity { get; set; } = 1f;

        // ------------------------------------------------------------------ profundidade
        [ExportGroup("Calculo de profundidade", "Depth")]
        // Ceu entrando pelo eixo de profundidade onde falta parede de fundo. Unico grupo que mexe
        // no solver logico: alterna-lo reconstroi o cache de luz.
        [Export] public bool DepthEnabled { get; set; } = true;

        // ------------------------------------------------------------------ feixe de abertura
        [ExportGroup("Feixe de abertura", "Beam")]
        // Luz que entra por um furo na parede de fundo e acende a superficie onde bate. E o
        // trajeto: quem depende dele e a poeira, nao o contrario.
        [Export] public bool BeamEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0.25,24,0.25")] public float BeamReachTiles { get; set; } = 24f;

        // ------------------------------------------------------------------ poeira na luz
        [ExportGroup("Luz volumetrica", "Dust")]
        // Poeira no ar dentro do feixe. Nao ilumina nada: so torna o trajeto visivel, somando luz.
        // Depende do feixe por construcao - sem luz atravessando, nao ha o que a poeira espalhe.
        [Export] public bool DustEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float DustDensity { get; set; } = 0.12f;

        // ------------------------------------------------------------------ sombra projetada
        // Quatro alvos, um peso cada. 1 = sombra cheia; 0 = aquele alvo nao recebe sombra.
        [ExportGroup("Sombra projetada no ar", "AirShadow")]
        [Export] public bool AirShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float AirShadowStrength { get; set; } = 0.35f;

        [ExportGroup("Sombra projetada no terreno", "TerrainShadow")]
        [Export] public bool TerrainShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float TerrainShadowStrength { get; set; } = 1f;

        [ExportGroup("Sombra projetada no background", "BackgroundShadow")]
        [Export] public bool BackgroundShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float BackgroundShadowStrength { get; set; } = 1f;

        [ExportGroup("Sombra projetada em entidades", "EntityShadow")]
        [Export] public bool EntityShadowEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float EntityShadowStrength { get; set; } = 1f;

        // ------------------------------------------------------------------ iluminacao do terreno
        [ExportGroup("Iluminacao do terreno", "TerrainLight")]
        // Quanto a luz penetra a partir da face exposta ate o preto completo. Nao e sombra
        // projetada no terreno: e a recepcao da superficie para dentro do bloco.
        [Export] public bool TerrainLightEnabled { get; set; } = true;
        [Export(PropertyHint.Range, "0.25,16,0.25")] public float TerrainLightDepthTiles { get; set; } = 3f;

        // ------------------------------------------------------------------ depuracao
        [ExportGroup("Depuracao")]
        [Export] public bool ShowRawMap { get; set; } = false;
    }
}
