using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebBanVLXD.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WebBanVLXD.Pages.Product
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public WebBanVLXD.Models.Product Product { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Lấy dữ liệu vào một biến tạm trước
            var productData = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(m => m.Id == id);

            // Kiểm tra nếu không thấy sản phẩm thì thoát ngay
            if (productData == null)
            {
                return NotFound();
            }

            // Nếu có dữ liệu mới gán vào thuộc tính Product
            Product = productData;

            return Page();
        }
    }
}