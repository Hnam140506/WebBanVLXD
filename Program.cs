using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using WebBanVLXD.Models;
using Net.payOS;
using DinkToPdf;
using DinkToPdf.Contracts;

var builder = WebApplication.CreateBuilder(args);

//PDF Converter Service
// Đăng ký dịch vụ chuyển đổi HTML sang PDF để dùng trong OrderController
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

//SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

//Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//PayOS
var payOS = new Net.payOS.PayOS(
    builder.Configuration["PayOS:ClientId"] ?? "",
    builder.Configuration["PayOS:ApiKey"] ?? "",
    builder.Configuration["PayOS:ChecksumKey"] ?? ""
);
builder.Services.AddSingleton(payOS);

//Authentication
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

//Forward Headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
});

var app = builder.Build();

app.UseForwardedHeaders();

//Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();