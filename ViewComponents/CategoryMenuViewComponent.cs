using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebBanVLXD.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Trong thực tế, bạn sẽ query từ DB. Ở đây mình hardcode ví dụ:
            var categories = new List<string> { "Xi măng", "Sắt thép", "Gạch ngói", "Sơn - Chống thấm", "Thiết bị vệ sinh" };
            return View(categories);
        }
    }
}