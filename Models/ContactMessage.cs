using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanVLXD.Models
{
    public class ContactMessage
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [Required]
        public string FullName { get; set; } = null!;
        [Required]
        public string Phone { get; set; } = null!;
        public string? Email { get; set; }
        public string? Subject { get; set; }
        [Required]
        public string Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false; // Trạng thái đã xem hay chưa
    }
}