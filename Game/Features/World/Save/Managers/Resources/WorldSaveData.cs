using Godot;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    [SaveType("world")]
    public partial class WorldSaveData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string WorldId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Name { get; set; } = "";

        [Export, GodotDictionaryField]
        public long Seed { get; set; }

        [Export, GodotDictionaryField]
        public WorldCharacterMode CharacterMode { get; set; } = WorldCharacterMode.LocalCharacters;

        // Mundo procedural gera terreno por seed e faz streaming de chunk; mundo nao procedural usa
        // so o mapa desenhado a mao nas cenas de nivel, com streaming desligado.
        [Export, GodotDictionaryField]
        public bool IsProcedural { get; set; } = true;

        [Export, GodotDictionaryField]
        public string MultiplayerKey { get; set; } = "";

        [Export, GodotDictionaryField]
        public int AutosaveIntervalMinutes { get; set; } = SavesConstants.DEFAULT_AUTOSAVE_INTERVAL_MINUTES;


        [Export, GodotDictionaryField]
        public long CreatedUtc { get; set; }

        [Export, GodotDictionaryField]
        public long LastPlayedUtc { get; set; }

        #endregion
    }
}
