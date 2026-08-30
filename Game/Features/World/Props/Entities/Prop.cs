using Godot;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Save;
using Jogo25D.Utils.GodotDictionaryParser;
using Jogo25D.Features.Managers.Save.Resources;

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

        // Ponto de entrada de quem quebra: pede pro servidor, ou resolve direto se for autoritativo.
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

        // Gancho pra subclasse: dropar item, tocar efeito, o que for dela.
        protected virtual void OnBeforeBreak()
        {
        }

        #endregion

   }
}
