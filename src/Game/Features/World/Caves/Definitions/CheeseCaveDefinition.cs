using Godot;

namespace Jogo25D.Caves
{
    // Saloes: ruido fractal acima de um limiar que cai com a profundidade (mais e maiores quanto mais fundo).
    public class CheeseCaveDefinition : CaveDefinition
    {
        #region Dinamic properties

        public float Frequency { get; init; }
        public int FractalOctaves { get; init; }
        public float ThresholdShallow { get; init; }
        public float ThresholdDeep { get; init; }
        public int MinDepth { get; init; }
        public int FullDepth { get; init; }

        #endregion

        #region CaveDefinition

        public override bool IsOpen(CaveGeneratorSystem caves, int x, int y, int depth)
        {
            if (depth < MinDepth * caves.WorldScale)
            {
                return false;
            }

            var warped = caves.Warp(this, x, y);
            var t = Mathf.SmoothStep(MinDepth * caves.WorldScale, FullDepth * caves.WorldScale, depth);

            return caves.Noise(Id, "cheese", Frequency, FractalOctaves).GetNoise2D(warped.X, warped.Y) > Mathf.Lerp(ThresholdShallow, ThresholdDeep, t);
        }

        #endregion
    }
}
