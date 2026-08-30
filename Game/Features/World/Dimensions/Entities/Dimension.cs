using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Save;

namespace Jogo25D.Dimensions
{
    public partial class Dimension : Node2D
    {
        #region Dinamic properties

        public virtual string DimensionId => ChunkStreamingConstants.UPSIDEDOWN_ID;

        [Save("mutations")]
        public Godot.Collections.Array Mutations
        {
            get => Game.Managers.TileStreamingManager.Node?.ExportMutations(DimensionId) ?? new Godot.Collections.Array();
            set => Game.Managers.TileStreamingManager.Node?.ImportMutations(DimensionId, value);
        }

        #endregion
    }
}
