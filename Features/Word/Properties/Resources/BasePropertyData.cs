using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class BasePropertyData : Resource
    {
        [Export, GodotDictionaryField]
        public bool Transmit { get; set; } = false;
    }
}