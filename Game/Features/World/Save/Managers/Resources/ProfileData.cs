using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    [SaveType("profile")]
    public partial class ProfileData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string ProfileId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string DisplayName { get; set; } = "Jogador";

        #endregion
    }
}
