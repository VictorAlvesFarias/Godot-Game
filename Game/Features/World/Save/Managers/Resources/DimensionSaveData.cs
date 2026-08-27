using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    [SaveType("dimension")]
    public partial class DimensionSaveData : Resource
    {
        #region Dinamic properties

        // Onde este estado mora. E o que permite o SaveManager gravar so olhando o tipo,
        // sem perguntar nada a ninguem.
        [Export, GodotDictionaryField]
        public string WorldId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string DimensionId { get; set; } = "";

        [Export, GodotDictionaryField]
        public Godot.Collections.Array<ChunkEntryData> Chunks { get; set; } = new();

        // Entidades da dimensao. Cada uma e o node serializado: os campos marcados dele mais
        // cena, identidade e posicao. Nao ha classe de dado por entidade.
        [Export, GodotDictionaryField]
        public Godot.Collections.Array<Godot.Collections.Dictionary> Entities { get; set; } = new();

        #endregion
    }
}
