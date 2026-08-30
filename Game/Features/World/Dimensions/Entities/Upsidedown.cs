using Jogo25D.Constants;
using Jogo25D.Save;

namespace Jogo25D.Dimensions
{
    [SaveScene("upsidedown", "res://Scenes/World/Levels/Upsidedown.tscn")]
    public partial class Upsidedown : Dimension
    {
        public override string DimensionId => ChunkStreamingConstants.UPSIDEDOWN_ID;
    }
}
