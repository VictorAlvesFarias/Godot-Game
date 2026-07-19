using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class BasePropertyData : Resource
    {
        private static long _nextInstanceId { get; set; } = System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFL;

        public static long NextInstanceId()
        {
            return ++_nextInstanceId;
        }

        [Export, GodotDictionaryField]
        public long InstanceId { get; set; }
    }
}