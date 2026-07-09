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

            // Lọc theo tìm kiếm
            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query) || p.Description.Contains(query));
                ViewBag.Query = query;
            }

            // Lọc theo danh mục từ View Component
            if (!string.IsNullOrEmpty(category))
            {
                products = products.Where(p => p.Category == category);
                ViewBag.Category = category;
            }

            // Sắp xếp mới nhất lên đầu
            products = products.OrderByDescending(p => p.CreatedAt);

            return View(products.ToList());
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