using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebBanVLXD.Pages.Contact
{
    public class IndexModel : PageModel
    {
        [TempData]
        public string? SuccessMessage { get; set; }

        // Các biến này sẽ tự động hứng dữ liệu từ Form do có [BindProperty]
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

        public void OnGet()
        {
            // Xử lý logic tải trang ban đầu
        }

        // Đã chuyển thành IActionResult
        public IActionResult OnPost()
        {
            // Xử lý logic gửi email hoặc lưu DB ở đây...

            // Cài đặt thông báo thành công hiển thị cho người dùng
            SuccessMessage = $"Cảm ơn {FullName}! Tin nhắn của bạn đã được gửi thành công. Đội ngũ BuildSmart sẽ liên hệ lại qua số điện thoại {Phone} trong thời gian sớm nhất.";

            // Tải lại trang hiện tại
            return RedirectToPage();
        }
    }
}