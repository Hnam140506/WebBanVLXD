using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebBanVLXD.Pages.Contact
{
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
            // Xử lý logic tải trang (nếu cần load dữ liệu bản đồ, thông tin liên hệ từ DB)
        }

        public void OnPost()
        {
            // Xử lý logic khi người dùng bấm "Gửi tin nhắn ngay"
            // Ví dụ: Gửi email thông báo, lưu database...
        }
    }
}