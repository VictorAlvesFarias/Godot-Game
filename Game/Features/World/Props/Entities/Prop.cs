using Godot;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Props
{
    [Unload(UnloadMode.Global)]
    public partial class Prop : Area2D
    {
        #region Dinamic properties

        [Save, GodotDictionaryField]
        public string PropId { get; set; } = "";

        #endregion

        #region Core - Quebra

        public void BreakClientRequest()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                ProcessBreak();

                return;
            }

            RpcId(1, nameof(BreakServerReceive));
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakServerReceive()
        {
            if (!Multiplayer.IsServer())
            {
                return;
            }

            ProcessBreak();
        }

        private void ProcessBreak()
        {
            Rpc(nameof(BreakBroadcast));

            OnBeforeBreak();

            QueueFree();
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakBroadcast()
        {
            OnBeforeBreak();

            QueueFree();
        }

        protected virtual void OnBeforeBreak()
        {
        }

        #endregion
   }
}
