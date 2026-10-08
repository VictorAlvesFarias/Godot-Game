using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Utils.Coordinates
{
    public static class CoordinateUtilities
    {
        public static Vector2I WorldToCell(Vector2 globalPosition, int tileSize)
        {
            return new Vector2I(
                Mathf.FloorToInt(globalPosition.X / tileSize),
                Mathf.FloorToInt(globalPosition.Y / tileSize)
            );
        }

        public static Vector2I CellToChunk(Vector2I cell)
        {
            return new Vector2I(
                Mathf.FloorToInt(cell.X / (float)ChunkStreamingConstants.CHUNK_SIZE),
                Mathf.FloorToInt(cell.Y / (float)ChunkStreamingConstants.CHUNK_SIZE)
            );
        }

        public static Vector2I WorldToChunk(Vector2 globalPosition, int tileSize)
        {
            return CellToChunk(WorldToCell(globalPosition, tileSize));
        }

        public static Vector2I ChunkToCell(Vector2I chunkCoord)
        {
            return chunkCoord * ChunkStreamingConstants.CHUNK_SIZE;
        }

        public static int CellToChunk(int cell)
        {
            return (int)System.Math.Floor(cell / (double)ChunkStreamingConstants.CHUNK_SIZE);
        }

        public static int ChunkDistance(Vector2I a, Vector2I b)
        {
            return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }
    }
}
