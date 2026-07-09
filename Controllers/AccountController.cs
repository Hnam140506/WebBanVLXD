using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebBanVLXD.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.Tasks;

namespace WebBanVLXD.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AccountController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // --- ĐĂNG KÝ ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string username, string email, string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu quá yếu! Vui lòng nhập ít nhất 6 ký tự.");
                return View();
            }

            if (_context.Users.Any(u => u.Email == email || u.UserName == username))
            {
                ModelState.AddModelError("", "Email hoặc Tên tài khoản đã tồn tại trên hệ thống!");
                return View();
            }

            var newUser = new User
            {
                UserName = username,
                Email = email,
                ThemePreference = "system",
                CreatedAt = DateTime.UtcNow
            };
            
            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, password);
            _context.Users.Add(newUser);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        // --- ĐĂNG NHẬP THƯỜNG ---
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác!");
                return View();
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tài khoản này được đăng ký qua Mạng xã hội. Vui lòng chọn nút Đăng nhập tương ứng hoặc bấm 'Quên mật khẩu'.");
                return View();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác!");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        // --- ĐĂNG XUẤT ---
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // --- ĐĂNG NHẬP MXH BÊN THỨ 3 (GOOGLE & FACEBOOK) ---
        [HttpPost]
        public IActionResult ExternalLogin(string provider)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var localEmail = User.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrEmpty(localEmail))
                {
                    properties.Items["LocalUserEmail"] = localEmail;
                }
            }

            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal.Identity?.Name;
            var provider = result.Principal.Identity?.AuthenticationType;

            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Không thể lấy địa chỉ email từ nhà cung cấp này.");
                return View("Login");
            }

            var providerTarget = $"{provider}:{email}";

            if (result.Properties.Items.TryGetValue("LocalUserEmail", out var localEmail) && !string.IsNullOrEmpty(localEmail))
            {
                var currentUser = _context.Users.FirstOrDefault(u => u.Email == localEmail);
                if (currentUser != null)
                {
                    var duplicateUser = _context.Users.FirstOrDefault(u => u.Email != localEmail &&
                        (u.Email == email || (u.AuthProvider != null && u.AuthProvider.Contains(providerTarget))));
                    
                    if (duplicateUser != null)
                    {
                        TempData["ErrorMessage"] = $"Tài khoản {provider} ({email}) đã được liên kết với một thành viên khác!";
                        return RedirectToAction("ChangePassword", "Account");
                    }
                    else
                    {
                        var currentProviders = new HashSet<string>((currentUser.AuthProvider ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
                        currentProviders.Add(providerTarget);

                        currentUser.AuthProvider = string.Join(",", currentProviders);
                        _context.Users.Update(currentUser);
                        _context.SaveChanges();

                        var claims1 = new List<Claim> {
                            new Claim(ClaimTypes.NameIdentifier, currentUser.Id),
                            new Claim(ClaimTypes.Name, currentUser.UserName),
                            new Claim(ClaimTypes.Email, currentUser.Email)
                        };
                        var claimsIdentity1 = new ClaimsIdentity(claims1, CookieAuthenticationDefaults.AuthenticationScheme);
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity1));

                        TempData["SuccessMessage"] = $"Liên kết tài khoản {provider} ({email}) thành công!";
                        return RedirectToAction("ChangePassword", "Account");
                    }
                }
            }

            var user = _context.Users.FirstOrDefault(u => u.AuthProvider != null && u.AuthProvider.Contains(providerTarget));
            if (user == null)
            {
                user = _context.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                {
                    user = new User
                    {
                        Email = email,
                        UserName = name ?? email.Split('@')[0],
                        ThemePreference = "system",
                        CreatedAt = DateTime.UtcNow,
                        PasswordHash = "",
                        AuthProvider = providerTarget
                    };
                    _context.Users.Add(user);
                    _context.SaveChanges();
                }
                else
                {
                    var currentProviders = new HashSet<string>((user.AuthProvider ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
                    currentProviders.Add(providerTarget);

                    user.AuthProvider = string.Join(",", currentProviders);
                    _context.Users.Update(user);
                    _context.SaveChanges();
                }
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        // --- ĐỔI MẬT KHẨU ---
        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (!User.Identity!.IsAuthenticated) return RedirectToAction("Login");
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            ViewBag.AuthProvider = user?.AuthProvider ?? "";

            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (!User.Identity!.IsAuthenticated) return RedirectToAction("Login");
            
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu mới phải có ít nhất 6 ký tự.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp!");
                return View();
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (user == null) return RedirectToAction("Login");
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError("", "Tài khoản mạng xã hội chưa thiết lập mật khẩu cục bộ.");
                return View();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, oldPassword);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Mật khẩu cũ không chính xác!");
                return View();
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            _context.Users.Update(user);
            _context.SaveChanges();
            
            return RedirectToAction("Logout");
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            string fromEmail = "nam1452000@gmail.com";
            string appPassword = "sbbb xitb zgij cnog";

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "BuildMat Admin"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
        }

        // --- QUÊN MẬT KHẨU ---
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email không tồn tại trên hệ thống.");
                return View();
            }

            user.ResetPasswordToken = Guid.NewGuid().ToString();
            user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var resetLink = Url.Action("ResetPassword", "Account", new { email = user.Email, token = user.ResetPasswordToken }, Request.Scheme);
            var subject = "Khôi phục mật khẩu - BuildMat";
            var body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 600px; border: 1px solid #ddd;'>
                    <h3 style='color: #333;'>Yêu cầu khôi phục/tạo mật khẩu hệ thống vật liệu</h3>
                    <p>Vui lòng click vào nút bên dưới để tiến hành thiết lập mật khẩu mới (Đường link có hiệu lực trong 15 phút):</p>
                    <div style='text-align: center; margin: 30px 0;'>
                         <a href='{resetLink}' style='display:inline-block; padding:12px 25px; background-color:#0d6efd; color:#fff; text-decoration:none; border-radius:5px; font-weight:bold;'>Thiết lập mật khẩu</a>
                    </div>
                </div>";
            
            await SendEmailAsync(user.Email, subject, body);
            ViewBag.Message = "Đường link thiết lập mật khẩu đã được gửi thành công. Vui lòng kiểm tra hộp thư.";
            return View();
        }

        // --- ĐẶT LẠI MẬT KHẨU ---
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu mới phải có ít nhất 6 ký tự.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận mới không trùng khớp!");
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại!");
                return View();
            }

            if (user.ResetPasswordToken != token)
            {
                ModelState.AddModelError("", "Mã xác thực khôi phục không hợp lệ!");
                return View();
            }
            if (user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Đường link khôi phục này đã hết hạn!");
                return View();
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            ViewBag.SuccessMessage = "Mật khẩu đã được thiết lập thành công! Hãy đăng nhập lại bằng Form Email.";
            return View();
        }
    }
}