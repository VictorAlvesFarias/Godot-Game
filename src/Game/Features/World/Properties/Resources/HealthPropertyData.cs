using Godot;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.World.Properties.Resources
{
    public partial class HealthPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public int MaxHealth { get; set; } = 50;

        #endregion
    }
}
