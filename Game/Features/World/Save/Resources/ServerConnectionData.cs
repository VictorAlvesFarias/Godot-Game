using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    [SaveType("server_connection")]
    public partial class ServerConnectionData : Resource
    {
        #region Dinamic properties

        [Export, GodotDictionaryField]
        public string ConnectionId { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Description { get; set; } = "";

        [Export, GodotDictionaryField]
        public string Ip { get; set; } = "";

        [Export, GodotDictionaryField]
        public int Port { get; set; }

        #endregion
    }
}
