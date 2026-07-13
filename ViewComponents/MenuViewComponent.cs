using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebBanVLXD.ViewComponents
{
    public class MenuViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var menuItems = new List<MenuItem>
            {
                new MenuItem { Name = "Trang chủ", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "Sản phẩm", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "Dự án", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "Tin tức", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "Liên hệ", Controller = "Home", Action = "Index" }
            };

            return View(menuItems);
        }
    }

    public class MenuItem
    {
        public string Name { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}