using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanVLXD.Models
{
    public class Coupon
    {
        [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString(); 
    
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } // Số tiền được giảm
        
        public DateTime ExpiryDate { get; set; } // Hạn sử dụng
        
        public bool IsActive { get; set; } = true; // Trạng thái kích hoạt
        
        public int UsageLimit { get; set; } // Giới hạn tổng số lần sử dụng
        
        public int UsedCount { get; set; } = 0; // Số lần khách đã dùng mã này
    }
}