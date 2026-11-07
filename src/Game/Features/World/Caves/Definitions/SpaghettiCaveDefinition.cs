using Godot;

namespace Jogo25D.Caves
{
    // Tuneis: faixa em volta da isolinha zero do ruido. Podem romper a superficie onde a mascara de entradas deixa.
    public class SpaghettiCaveDefinition : CaveDefinition
    {
        #region Dinamic properties

        public float Frequency { get; init; }
        public float Width { get; init; }
        public int MinDepth { get; init; }

        public float EntranceFrequency { get; init; }
        public float EntranceThreshold { get; init; }
        public float EntranceFade { get; init; }

        #endregion

        #region CaveDefinition

        public override bool IsOpen(CaveGeneratorSystem caves, int x, int y, int depth)
        {
            if (depth < GetMinDepth(caves, x))
            {
                return false;
            }

            var warped = caves.Warp(this, x, y);

            return Mathf.Abs(caves.Noise(Id, "spaghetti", Frequency, 0).GetNoise2D(warped.X, warped.Y)) < Width;
        }

        #endregion

        #region Utils

        private int GetMinDepth(CaveGeneratorSystem caves, int x)
        {
            var entrance = Mathf.SmoothStep(
                EntranceThreshold,
                EntranceThreshold + EntranceFade,
                caves.Noise(Id, "entrance", EntranceFrequency, 0).GetNoise1D(x));

            return Mathf.RoundToInt(MinDepth * caves.WorldScale * (1f - entrance));
        }

        #endregion
    }
}
