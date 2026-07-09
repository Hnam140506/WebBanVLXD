using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace WebBanVLXD.ViewComponents
{
    public class ProductFilterViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string currentCategory)
        {
            // Truyền danh mục hiện tại đang chọn ra ngoài View để CSS sáng (active) lên
            ViewBag.CurrentCategory = string.IsNullOrEmpty(currentCategory) ? "Tất cả sản phẩm" : currentCategory;
            return View();
        }
    }
}