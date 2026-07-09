using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebBanVLXD.Models;
using System.Collections.Generic;
using System.Linq;
using System;

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
        public string Category { get; set; } = string.Empty; // Fix CS8618
        
        [BindProperty(SupportsGet = true)]
        public string SortOrder { get; set; } = string.Empty; // Fix CS8618
        
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int TotalPages { get; set; }

        public void OnGet()
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(Category))
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

            int pageSize = 9;
            TotalPages = (int)Math.Ceiling(query.Count() / (double)pageSize);
            
            if (PageNumber < 1) PageNumber = 1;
            Products = query.Skip((PageNumber - 1) * pageSize).Take(pageSize).ToList();
        }
    }
}