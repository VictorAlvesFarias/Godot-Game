using Godot;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    public partial class WorldSaveData : Resource
    {
        [Export, GodotDictionaryField]
        public string WorldId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Name { get; set; } = "";

        [Export, GodotDictionaryField]
        public long Seed { get; set; }

        [Export, GodotDictionaryField]
        public WorldCharacterMode CharacterMode { get; set; } = WorldCharacterMode.LocalCharacters;

        [Export, GodotDictionaryField]
        public string MultiplayerKey { get; set; } = "";

        [Export, GodotDictionaryField]
        public int AutosaveIntervalMinutes { get; set; } = 5;

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<PortalSaveData> Portals { get; set; } = new();

        [Export, GodotDictionaryField]
        public long CreatedUtc { get; set; }

        [Export, GodotDictionaryField]
        public long LastPlayedUtc { get; set; }
    }
}
