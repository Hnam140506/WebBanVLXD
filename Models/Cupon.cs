using System.ComponentModel.DataAnnotations;

namespace WebBanVLXD.Models
{
    public class Coupon
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = null!; // VD: GIAM20
        public decimal DiscountPercent { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}