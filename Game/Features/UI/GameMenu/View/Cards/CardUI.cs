using Godot;
using Jogo25D.Characters;

namespace Jogo25D.UI
{
    // Base dos cards da tela de inventario. A tela entrega o jogador local e avisa quando abre, fecha e quando o
    // inventario muda; cada card cuida so do proprio conteudo.
    public partial class CardUI : VBoxContainer
    {
        #region Dinamic properties

        public Player LocalPlayer { get; private set; }

        // A tela esta aberta (os cards so atualizam a cada quadro enquanto ela esta aberta).
        public bool IsOpen { get; private set; }

        protected bool HasPlayer => LocalPlayer != null && IsInstanceValid(LocalPlayer);

        #endregion

        #region Core - Contrato do card

        public virtual void Bind(Player player)
        {
            LocalPlayer = player;
        }

        public virtual void OnOpened()
        {
            IsOpen = true;
        }

        public virtual void OnClosed()
        {
            IsOpen = false;
        }

        public virtual void OnInventoryChanged()
        {
        }

        #endregion
    }
}
