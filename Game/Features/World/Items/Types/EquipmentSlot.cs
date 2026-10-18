namespace Jogo25D.Items
{
    // Posicoes de equipamento do personagem (separadas do item da mao).
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Accessory1,
        Accessory2,
    }

    // Tipo de peca: em que posicao ela cabe (acessorio cabe em qualquer posicao de acessorio).
    public enum EquipmentKind
    {
        Head,
        Chest,
        Legs,
        Accessory,
    }
}
