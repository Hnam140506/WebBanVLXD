using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Bổ sung thư viện này

namespace WebBanVLXD.Models
{
    public class Coupon
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = null!; // VD: GIAM20
        [Column(TypeName = "decimal(18,2)")] // Bổ sung dòng này
        public decimal DiscountPercent { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}