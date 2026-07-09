using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanVLXD.Models
{
    public class Product
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [Required]
        public string Name { get; set; } = null!; // VD: Xi măng, Sắt thép
        public string Description { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string Unit { get; set; } = "Cái"; // Bao, Tấn, Viên...
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public string Category { get; set; } = "Chung";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal OldPrice { get; set; } // Giá cũ để gạch ngang
        public string Brand { get; set; } = string.Empty;    // Thương hiệu
        public string Specifications { get; set; } = string.Empty; // Thông số kỹ thuật
    }
}