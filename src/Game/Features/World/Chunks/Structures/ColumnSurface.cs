namespace Jogo25D.Chunks
{
    internal readonly struct ColumnSurface
    {
        public readonly int WorldX;
        public readonly int GroundHeight;
        public readonly string Biome;

        public ColumnSurface(int worldX, int groundHeight, string biome)
        {
            WorldX = worldX;
            GroundHeight = groundHeight;
            Biome = biome;
        }
    }
}
