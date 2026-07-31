using Microsoft.AspNetCore.Mvc;
using WebBanVLXD.Models;
using System.Diagnostics;
using System.Linq;

namespace WebBanVLXD.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string query, string category)
        {
            var products = _context.Products.AsQueryable();

            // Lọc theo tìm kiếm (Tên, Thương hiệu hoặc Danh mục)
            if (!string.IsNullOrEmpty(query))
            {
                string q = query.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(q)
                                            || p.Brand.ToLower().Contains(q)
                                            || p.Category.ToLower().Contains(q));
                ViewBag.Query = query;
            }

            // Lọc theo danh mục từ View Component
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
                ViewBag.Category = category;
            }

            return View(products.OrderByDescending(p => p.CreatedAt).ToList());
        }

        public IActionResult Search(string query)
        {
            return RedirectToAction("Index", new { query = query });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}