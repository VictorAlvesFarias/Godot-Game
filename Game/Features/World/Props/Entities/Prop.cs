using Godot;
using Jogo25D.Core;
using Jogo25D.Entities;
using Jogo25D.Utils.GodotDictionaryParser;
using Jogo25D.Features.Managers.Save.Resources;

namespace Jogo25D.Props
{
    // Base de tudo que e colocado no mundo como objeto: portal hoje, o resto depois.
    // O ciclo de vida - colocar, quebrar, replicar e persistir - vive aqui e serve qualquer prop.
    // Subclasse so implementa o que e especifico dela (o portal, por exemplo, so a interacao).
    [Unload(UnloadMode.Global)]
    public partial class Prop : Area2D
    {
        #region Dinamic properties

        // Id da PropDefinition que gerou este no. Marcado = vai pro save e trafega por RPC,
        // e e o que faz este no ser encontrado pela varredura do WorldStreaming.
        [GodotDictionaryField]
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
