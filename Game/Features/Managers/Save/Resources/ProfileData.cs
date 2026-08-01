using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    public partial class ProfileData : Resource
    {
        [Export, GodotDictionaryField]
        public string ProfileId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string DisplayName { get; set; } = "Jogador";
    }
}
