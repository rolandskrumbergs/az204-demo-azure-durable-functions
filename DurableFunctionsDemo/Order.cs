namespace DurableFunctionsDemo
{
    public class Order
    {
        public Guid Id { get; set; }
        public bool CustomerChecked { get; set; }
        public bool InventoryChecked { get; set; }
        public bool CanFullfill { get; set; }

        public string Status { get; set; } = "CREATED";
    }
}
