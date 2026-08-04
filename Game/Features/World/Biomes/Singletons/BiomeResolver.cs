using Godot;

namespace Jogo25D.Biomes
{
    public static class BiomeResolver
    {
        private const float BiomeNoiseFrequency = 0.004f;

        // O ruido bruto (so sinal positivo/negativo) cruza o zero de forma irregular - as vezes
        // gera uma faixa de bioma enorme, as vezes uma bem fina (ex: um olive minusculo
        // espremido entre dois limes). Pra garantir uma largura minima de bioma, em vez de usar
        // o valor bruto, usamos a MEDIA de varias amostras espalhadas numa janela de
        // MinBiomeBandWidth tiles (um filtro passa-baixa simples) - isso apaga qualquer
        // oscilacao menor que a janela, sem achatar a forma organica das bordas maiores.
        private const float MinBiomeBandWidth = 64f;
        private const int BiomeSmoothingSampleCount = 5;

        // Ruido usado pra DESLOCAR a posicao X da fronteira conforme a altura (Y), em vez de
        // decidir cada celula independente - isso empena a linha divisoria numa curva continua
        // que serpenteia, mas nunca quebra em ilhas soltas (estruturalmente continua sendo "um
        // lado e um bioma, o outro lado e outro", so que a linha nao e mais reta). Parametros
        // validados via Tools/biome_noise_preview.py antes de portar pra ca.
        // So 2 oitavas (nao 4) - com Lacunarity 2.3, a 3a/4a oitava chegava a oscilar a cada
        // 1-2 tiles, formas pequenas demais pra existir tile de autotile compativel (o
        // tileset foi desenhado pra relevo continuo, nao pra bolsoes minusculos) - isso gerava
        // os "pontinhos pretos" no jogo, mesmo o preview em Python parecendo limpo (la e so
        // cor, nao depende de arte de tile).
        private const float WarpNoiseFrequency = 0.04f;
        private const float WarpAmplitude = 48f;
        private const int WarpFractalOctaves = 2;
        private const float WarpFractalLacunarity = 2.3f;
        private const float WarpFractalGain = 0.55f;

        // O quanto o warp desvanece suavemente conforme o valor do ruido base se afasta de
        // zero (0 = exatamente no cruzamento, 1 = bem fundo em um dos dois biomas) - sem corte
        // brusco em lugar nenhum do mapa.
        private const float FadeRange = 0.2f;

        public static BiomeType Resolve(long worldSeed, string dimensionId, int worldX, int worldY)
        {
            var baseValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX);
            var proximity = Mathf.Clamp(1f - Mathf.Abs(baseValue) / FadeRange, 0f, 1f);

            if (proximity <= 0f)
            {
                return baseValue < 0f ? BiomeType.LimeGround : BiomeType.OliveGround;
            }

            var warpNoise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome_warp"),
                Frequency = WarpNoiseFrequency,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = WarpFractalOctaves,
                FractalLacunarity = WarpFractalLacunarity,
                FractalGain = WarpFractalGain,
            };

            var warpOffset = Mathf.RoundToInt(warpNoise.GetNoise1D(worldY) * WarpAmplitude * proximity);
            var shiftedValue = GetSmoothedBaseNoiseValue(worldSeed, dimensionId, worldX + warpOffset);

            return shiftedValue < 0f ? BiomeType.LimeGround : BiomeType.OliveGround;
        }

        private static float GetSmoothedBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var half = BiomeSmoothingSampleCount / 2;
            var step = MinBiomeBandWidth / BiomeSmoothingSampleCount;
            var sum = 0f;

            for (int i = -half; i <= half; i++)
            {
                sum += GetBaseNoiseValue(worldSeed, dimensionId, worldX + Mathf.RoundToInt(i * step));
            }

            return sum / BiomeSmoothingSampleCount;
        }

        private static float GetBaseNoiseValue(long worldSeed, string dimensionId, int worldX)
        {
            var noise = new FastNoiseLite
            {
                Seed = (int)CombineBiomeSeed(worldSeed, dimensionId, "biome"),
                Frequency = BiomeNoiseFrequency,
            };

            return noise.GetNoise1D(worldX);
        }

        private static long CombineBiomeSeed(long worldSeed, string dimensionId, string tag)
        {
            unchecked
            {
                long hash = worldSeed;

                hash = hash * 397 ^ StableStringHash(dimensionId);
                hash = hash * 397 ^ StableStringHash(tag);

                return hash;
            }
        }

        private static long StableStringHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603;

                foreach (var c in value)
                {
                    hash ^= c;
                    hash *= 1099511628211;
                }

                return hash;
            }
        }
    }
}
