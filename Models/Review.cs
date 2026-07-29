namespace WebBanVLXD.Models
{
    public class Review
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProductId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public int Rating { get; set; }
        public string Comment { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Hai cột này mới thêm vào:
        public string? AdminReply { get; set; } 
        public DateTime? ReplyDate { get; set; }
        public string? OrderId { get; set; } 
    }
}