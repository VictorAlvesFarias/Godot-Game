using Godot;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Properties.Resources
{
    public partial class MovementPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public float Speed { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public float JumpVelocity { get; set; } = 0f;

        #endregion
    }
}
