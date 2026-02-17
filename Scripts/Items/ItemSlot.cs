namespace Jogo25D.Items
{
    public class ItemSlot
    {
        public Item Item { get; set; }
        public int Quantity { get; set; } = 0;
        
        public bool IsEmpty => Item == null || Quantity <= 0;
        
        public ItemSlot() { }
        
        public ItemSlot(Item item, int quantity = 1)
        {
            Item = item;
            Quantity = quantity;
        }

        public void Clear()
        {
            Item = null;
            Quantity = 0;
        }

        public bool CanAddMore()
        {
            if (IsEmpty || Item == null) return true;
            return Item.IsStackable && Quantity < Item.MaxStackSize;
        }
    }
}
