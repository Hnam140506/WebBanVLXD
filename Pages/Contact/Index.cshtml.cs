using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebBanVLXD.Models; // THÊM DÒNG NÀY
using System.Threading.Tasks; // THÊM DÒNG NÀY
using System; // THÊM DÒNG NÀY

namespace WebBanVLXD.Pages.Contact
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context; // THÊM DÒNG NÀY

        // THÊM CONSTRUCTOR ĐỂ KẾT NỐI DATABASE
        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Phone { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Subject { get; set; } = string.Empty;

        [BindProperty]
        public string Message { get; set; } = string.Empty;

        public void OnGet() { }

        // CHUYỂN THÀNH async Task ĐỂ LƯU DATABASE MƯỢT MÀ
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // --- PHẦN MỚI: LƯU TIN NHẮN VÀO DATABASE ---
            var contactMsg = new ContactMessage
            {
                FullName = this.FullName,
                Phone = this.Phone,
                Email = this.Email,
                Subject = this.Subject,
                Message = this.Message,
                CreatedAt = DateTime.Now
            };

            _context.ContactMessages.Add(contactMsg);
            await _context.SaveChangesAsync();
            // ------------------------------------------

            // Giữ nguyên thông báo cũ của bạn
            SuccessMessage = $"Cảm ơn {FullName}! Tin nhắn của bạn đã được gửi thành công. Đội ngũ BuildSmart sẽ liên hệ lại qua số điện thoại {Phone} trong thời gian sớm nhất.";

            return RedirectToPage();
        }
    }
}