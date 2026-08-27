using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
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

        // O Player serializado: os campos marcados dele. Nao ha classe espelho - o estado
        // do personagem E o no, e isto e o retrato dele.
        [Export, GodotDictionaryField]
        public Godot.Collections.Dictionary State { get; set; } = new();

        [Export, GodotDictionaryField]
        public long CreatedUtc { get; set; }

        [Export, GodotDictionaryField]
        public long LastPlayedUtc { get; set; }

        #endregion
    }
}
