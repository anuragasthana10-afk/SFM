namespace ZohoBooksSync.Models
{
    public sealed class InventoryItem
    {
        public int LocalId { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public decimal Rate { get; set; }
        public int Quantity { get; set; }
    }
}
