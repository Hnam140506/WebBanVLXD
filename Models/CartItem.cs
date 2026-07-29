namespace WebBanVLXD.Models
{
    public class CartItem
    {
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public string? VariantId { get; set; }
        public string? VariantName { get; set; }
        public string? ImageUrl { get; set; } 
        public decimal Price { get; set; }
    }
}