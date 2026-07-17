using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;

var builder = WebApplication.CreateBuilder(args);

// Thêm dịch vụ Controller, Views và Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 1. Cấu hình SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// 2. CẤU HÌNH SESSION (BẮT BUỘC CHO GIỎ HÀNG)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Giỏ hàng tồn tại trong 30 phút chờ
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 3. Cấu hình Cookie Authentication & OAuth (Google/Facebook)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddGoogle(options =>
    {
        options.ClientId = "150062087659-oidc11vout40qk77gjhp55o2jnhigpbu.apps.googleusercontent.com";
        options.ClientSecret = "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";
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
            return Task.CompletedTask;
        };
    })
    .AddFacebook(options =>
    {
        options.AppId = "2710948109288781";
        options.AppSecret = "2b0980baa91e5cfe3c5d474f8a9ee3b2";
    });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Cấu hình Pipeline xử lý yêu cầu HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// BẬT SESSION TRƯỚC AUTHENTICATION
app.UseSession(); 

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();