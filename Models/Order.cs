using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Bổ sung thư viện này

namespace WebBanVLXD.Models {
    public class Order {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; } // Có thể null nếu khách vãng lai
        [Required] public string CustomerName { get; set; } = null!;
        [Required] public string Phone { get; set; } = null!;
        [Required] public string Address { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Chờ xử lý";
        // --- THÊM CÁC TRƯỜNG NÀY ---
        public string PaymentMethod { get; set; } = "COD"; // COD hoặc BankTransfer
        public string? CouponCode { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }
        public List<OrderDetail> OrderDetails { get; set; } = new();
    }

    public class OrderDetail {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; } = null!;
        public string ProductId { get; set; } = null!;
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public Product Product { get; set; } = null!;
    }
}