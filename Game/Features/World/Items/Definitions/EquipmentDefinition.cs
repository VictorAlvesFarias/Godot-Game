using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    // Peca de equipamento (armadura ou acessorio): fica num slot de equipamento e soma as Properties dela aos stats.
    // Nao e usada na mao.
    public class EquipmentDefinition : ItemDefinition
    {
        #region Properties

        public EquipmentKind Kind { get; init; }

        #endregion

        #region Core - Virtuals

        public override void Use(Player player, ItemData data)
        {
        }

        #endregion
    }
}
