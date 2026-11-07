using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class BasePropertyData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public long InstanceId { get; set; }

        #endregion
    }
}