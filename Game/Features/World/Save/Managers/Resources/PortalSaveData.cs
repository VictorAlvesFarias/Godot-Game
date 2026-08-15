using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    public partial class PortalSaveData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public float PositionX { get; set; }

        [Export, GodotDictionaryField]
        public float PositionY { get; set; }

        [Export, GodotDictionaryField]
        public string DimensionId { get; set; } = "";

        #endregion
    }
}
