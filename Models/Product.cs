using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace WebBanVLXD.Models
{
    public class Product
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Name { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "Chung";
        public string Brand { get; set; } = string.Empty;
        public string Unit { get; set; } = "Cái";
        public string? ImageUrl { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // PHẢI CÓ 3 DÒNG NÀY ĐỂ FIX LỖI 1061
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal OldPrice { get; set; } // Dòng này sẽ fix lỗi ở AppDbContext và EditProduct

        public int StockQuantity { get; set; }

        // Các quan hệ mới
        public List<ProductImage> Images { get; set; } = new();
        public List<ProductVariant> Variants { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();

        [NotMapped]
        public double AverageRating => Reviews.Any() ? Reviews.Average(r => r.Rating) : 5.0;
    }

    public class ProductImage {
        [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProductId { get; set; } = null!;
        public string Url { get; set; } = null!;
    }

    public class ProductVariant {
        [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProductId { get; set; } = null!;
        public string Name { get; set; } = null!; 
        [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
    }
}