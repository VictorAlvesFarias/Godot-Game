namespace Jogo25D.Structures
{
    public struct StructureBounds
    {
        public int Left { get; set; }
        public int Right { get; set; }
        public int Top { get; set; }

        public StructureBounds(int left, int right, int top)
        {
            Left = left;
            Right = right;
            Top = top;
        }
    }
}
