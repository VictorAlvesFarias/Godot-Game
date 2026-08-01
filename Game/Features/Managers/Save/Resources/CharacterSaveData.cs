using Godot;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    public partial class CharacterSaveData : Resource
    {
        [Export, GodotDictionaryField]
        public string CharacterId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string OwnerProfileId { get; set; } = "";

        // "" = personagem local; caso contrario, a Chave de Multiplayer dona dele.
        [Export, GodotDictionaryField]
        public string MultiplayerKey { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Name { get; set; } = "";

        [Export, GodotDictionaryField]
        public PlayerData Data { get; set; } = new PlayerData();

        [Export, GodotDictionaryField]
        public long CreatedUtc { get; set; }

        [Export, GodotDictionaryField]
        public long LastPlayedUtc { get; set; }
    }
}
