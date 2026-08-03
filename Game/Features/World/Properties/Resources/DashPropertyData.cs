using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class DashPropertyData : BasePropertyData
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public float DashSpeed { get; set; } = 800f;

        [Export, GodotDictionaryField]
        public float MovementInfluence { get; set; } = 0.4f;

        #endregion
    }
}