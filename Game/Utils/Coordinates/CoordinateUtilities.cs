using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Utils.Coordinates
{
    // Conversao entre posicao do mundo, celula do tilemap e chunk. Estava privado dentro do
    // streaming de tile; virou utility porque o streaming de entidade precisa das mesmas contas
    // pra saber em que chunk uma entidade esta.
    public static class CoordinateUtilities
    {
        public static Vector2I WorldToCell(Vector2 globalPosition, int tileSize)
        {
            return new Vector2I(
                Mathf.FloorToInt(globalPosition.X / tileSize),
                Mathf.FloorToInt(globalPosition.Y / tileSize));
        }

        public static Vector2I CellToChunk(Vector2I cell)
        {
            return new Vector2I(
                Mathf.FloorToInt(cell.X / (float)ChunkStreamingConstants.CHUNK_SIZE),
                Mathf.FloorToInt(cell.Y / (float)ChunkStreamingConstants.CHUNK_SIZE));
        }

        public static Vector2I WorldToChunk(Vector2 globalPosition, int tileSize)
        {
            return CellToChunk(WorldToCell(globalPosition, tileSize));
        }

        // Canto superior esquerdo do chunk, em celula.
        public static Vector2I ChunkToCell(Vector2I chunkCoord)
        {
            return chunkCoord * ChunkStreamingConstants.CHUNK_SIZE;
        }

        // Distancia de Chebyshev: e a metrica certa aqui porque o raio de carga e um
        // quadrado ao redor do player, nao um circulo.
        public static int ChunkDistance(Vector2I a, Vector2I b)
        {
            return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }
    }
}
