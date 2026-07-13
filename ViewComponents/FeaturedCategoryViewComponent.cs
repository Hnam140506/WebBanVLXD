using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebBanVLXD.ViewComponents
{
    public class FeaturedCategoryViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Danh sách danh mục tĩnh đồng bộ với trường Category trong Database của bạn
            var categories = new List<CategoryItem>
            {
                new CategoryItem { Name = "Cát xây dựng", Icon = "fa-solid fa-mound", Color = "#003366" },
                new CategoryItem { Name = "Đá xây dựng", Icon = "fa-solid fa-gem", Color = "#ffc107" },
                new CategoryItem { Name = "Gạch tuynel", Icon = "fa-solid fa-cubes", Color = "#dc3545" },
                new CategoryItem { Name = "Xi măng", Icon = "fa-solid fa-bag-shopping", Color = "#198754" },
                new CategoryItem { Name = "Sắt thép", Icon = "fa-solid fa-link", Color = "#6c757d" },
                new CategoryItem { Name = "Sơn & Chống thấm", Icon = "fa-solid fa-paint-roller", Color = "#0dcaf0" }
            };

            return View(categories);
        }
    }

    public class CategoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
}