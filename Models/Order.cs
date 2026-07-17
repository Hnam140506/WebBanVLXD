using System.ComponentModel.DataAnnotations;

namespace WebBanVLXD.Models {
    public class Order {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; } // Có thể null nếu khách vãng lai
        [Required] public string CustomerName { get; set; } = null!;
        [Required] public string Phone { get; set; } = null!;
        [Required] public string Address { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Chờ xử lý";
        public List<OrderDetail> OrderDetails { get; set; } = new();
    }

    public class OrderDetail {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; } = null!;
        public string ProductId { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public Product Product { get; set; } = null!;
    }
}