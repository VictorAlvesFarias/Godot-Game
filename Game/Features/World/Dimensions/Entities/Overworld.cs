using Jogo25D.Constants;
using Jogo25D.Save;

namespace Jogo25D.Dimensions
{
    [SaveScene("overworld", "res://Scenes/World/Levels/Overworld.tscn")]
    public partial class Overworld : Dimension
    {
        public override string DimensionId => ChunkStreamingConstants.OVERWORLD_ID;
    }
}
