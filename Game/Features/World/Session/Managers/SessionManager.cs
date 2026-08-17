using Godot;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.Session
{
    // Estado entre telas: o que foi escolhido no WorldSelect/CharacterSelect e ainda nao entrou em
    // jogo, e o caminho de volta pro menu. E o unico manager com que a UI conversa pra entrar num
    // mundo; quem executa a entrada e o WorldManager.
    public partial class SessionManager : Node
    {
        #region Dinamic properties

        private WorldSaveData _pendingWorld;

        // Escolher o mundo define o modo de personagem da sessao - a tela de personagem abre
        // depois disso e ja encontra o SaveManager sabendo se e local ou de servidor.
        public WorldSaveData PendingWorld
        {
            get => _pendingWorld;
            set
            {
                _pendingWorld = value;

                Game.Managers.SaveManager.Node.CharacterMode = value?.CharacterMode ?? WorldCharacterMode.LocalCharacters;
            }
        }
        public CharacterSaveData PendingCharacter { get; set; }

        #endregion

        #region Core - Entrada

        public void EnterPendingWorld()
        {
            // Mundo nao procedural roda o mapa desenhado a mao, com streaming desligado.
            if (PendingWorld != null && !PendingWorld.IsProcedural)
            {
                Game.Managers.WorldManager.Node.SpawnLocalWorldAndPlayer(PendingWorld);
            }
            else
            {
                Game.Managers.WorldManager.Node.CreateProceduralWorldAndPlayer(PendingWorld);
            }

            PendingWorld = null;
        }

        public string SpawnWorldAndJoin(string textAddress)
        {
            Game.Managers.WorldManager.Node.SpawnWorld();

            return Game.Managers.NetworkManager.Node.JoinServer(textAddress);
        }

        #endregion

        #region Core - Saida

        public void ReturnToMainMenu()
        {
            GetTree().Paused = false;

            Game.Managers.RouterManager.Node.Close(Game.Ui.PauseUI.Node);

            Game.Managers.WorldManager.Node.LeaveWorld();

            Game.Managers.RouterManager.Node.Replace(Game.Ui.StartUI.Node);
        }

        #endregion
    }
}
