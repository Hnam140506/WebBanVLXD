using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebBanVLXD.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;

namespace WebBanVLXD.Pages.Product
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<WebBanVLXD.Models.Product> Products { get; set; } = new List<WebBanVLXD.Models.Product>();
        
        [BindProperty(SupportsGet = true)]
        public string Category { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public string SortOrder { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int TotalPages { get; set; }

        // ĐỊNH NGHĨA BIẾN NÀY ĐỂ HẾT LỖI GẠCH ĐỎ
        public bool HasMore { get; set; } 

        private IQueryable<WebBanVLXD.Models.Product> GetProductQuery()
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(Category) && Category != "Tất cả sản phẩm")
            {
                query = query.Where(p => p.Category == Category);
            }
            else
            {
                Category = "Tất cả sản phẩm";
            }

            switch (SortOrder)
            {
                case "price_asc": query = query.OrderBy(p => p.Price); break;
                case "price_desc": query = query.OrderByDescending(p => p.Price); break;
                case "name_asc": query = query.OrderBy(p => p.Name); break;
                default: query = query.OrderByDescending(p => p.CreatedAt); break;
            }

            return query;
        }

        public void OnGet()
        {
            var query = GetProductQuery();
            int pageSize = 20; // Hiện 20 cái mỗi lần
            
            int totalCount = query.Count();
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            
            if (PageNumber < 1) PageNumber = 1;

            // Lấy 20 sản phẩm đầu tiên
            Products = query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToList();

            // Cập nhật HasMore để ẩn/hiện nút Xem thêm
            HasMore = totalCount > (PageNumber * pageSize);
        }

        // Handler dành riêng cho AJAX bấm nút "Xem thêm"
        public IActionResult OnGetLoadMore(int pageNumber, string category, string sortOrder)
        {
            Category = category;
            SortOrder = sortOrder;
            
            var query = GetProductQuery();
            int pageSize = 20;
            var products = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            // Trả về Partial View để dán thêm sản phẩm vào trang
            return Partial("_ProductGridPartial", products);
        }
    }
}