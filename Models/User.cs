using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanVLXD.Models
{
    public class User
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [Required]
        public string UserName { get; set; } = null!;
        [Required]
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        
        public string? AuthProvider { get; set; }
        public string ThemePreference { get; set; } = "system";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiry { get; set; }
    }
}