using System.ComponentModel.DataAnnotations;

namespace WebBanVLXD.Models
{
    public class Review
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProductId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public int Rating { get; set; } // 1-5 sao
        public string Comment { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? AdminReply { get; set; } 
        public DateTime? ReplyDate { get; set; }
    }
}