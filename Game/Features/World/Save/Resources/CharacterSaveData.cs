using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Save.Resources
{
    [SaveType("character")]
    public partial class CharacterSaveData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string CharacterId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string OwnerProfileId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string MultiplayerKey { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Name { get; set; } = "";

        [Export, GodotDictionaryField]
        public Godot.Collections.Dictionary State { get; set; } = new();

        [Export, GodotDictionaryField]
        public long CreatedUtc { get; set; }

        [Export, GodotDictionaryField]
        public long LastPlayedUtc { get; set; }

        #endregion
    }
}
