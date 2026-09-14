using CafeDash.Data;
using CafeDash.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CafeDash.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private const int MaxFailedAttempts = 3;
        private const int BlockDurationMinutes = 2;

        public AccountController(ApplicationDbContext context, IMemoryCache cache, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _cache = cache;
            _config = config;
            _env = env;
        }

        private (bool isBlocked, int remainingSeconds) CheckLoginBlocked(string key)
        {
            if (_cache.TryGetValue($"login_blocked_{key}", out DateTime blockUntil))
            {
                if (blockUntil > DateTime.UtcNow)
                {
                    int remaining = (int)Math.Ceiling((blockUntil - DateTime.UtcNow).TotalSeconds);
                    return (true, remaining);
                }
                _cache.Remove($"login_blocked_{key}");
                _cache.Remove($"login_attempts_{key}");
            }
            return (false, 0);
        }

        private void IncrementFailedAttempts(string key)
        {
            int attempts = _cache.GetOrCreate($"login_attempts_{key}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(BlockDurationMinutes);
                return 0;
            }) + 1;

            _cache.Set($"login_attempts_{key}", attempts, TimeSpan.FromMinutes(BlockDurationMinutes));

            if (attempts >= MaxFailedAttempts)
            {
                _cache.Set($"login_blocked_{key}", DateTime.UtcNow.AddMinutes(BlockDurationMinutes),
                    TimeSpan.FromMinutes(BlockDurationMinutes));
            }
        }

        private void ResetLoginAttempts(string key)
        {
            _cache.Remove($"login_attempts_{key}");
            _cache.Remove($"login_blocked_{key}");
        }

        // Shows the Welcome Landing Page
        [HttpGet]
        public IActionResult Welcome()
        {
            return View();
        }

        // 1. Shows the Login Page
        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.Title = "Login - Cafe Dash";
            return View();
        }

        // 2. Handles the Login Submission using Entity Framework Core
        [HttpPost]
        public async Task<IActionResult> Login(string User_name, string password)
        {
            string loginKey = (User_name ?? "").Trim().ToLower();

            var (isBlocked, remainingSeconds) = CheckLoginBlocked(loginKey);
            if (isBlocked)
            {
                int mins = remainingSeconds / 60;
                int secs = remainingSeconds % 60;
                ViewBag.Error = $"Too many failed login attempts. Please try again in {mins} minute(s) and {secs} second(s).";
                return View();
            }

            // ==========================================
            // 1. CAPTCHA SECURITY CHECK
            // ==========================================
            string captchaResponse = Request.Form["g-recaptcha-response"];
            bool isCaptchaValid = await VerifyCaptcha(captchaResponse);

            if (!isCaptchaValid)
            {
                if (_env.IsDevelopment())
                {
                    Console.WriteLine("[DEV] CAPTCHA check bypassed in development mode (result: " +
                        (string.IsNullOrEmpty(captchaResponse) ? "not submitted" : "verification failed (likely offline/network)") + ").");
                }
                else
                {
                    ViewBag.Error = "Please complete the CAPTCHA to prove you are human.";
                    return View();
                }
            }

            // ==========================================
            // 2. NORMAL LOGIN LOGIC (EF CORE)
            // ==========================================
            if (string.IsNullOrEmpty(User_name) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter both username and password.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.User_name == User_name || u.Email == User_name);

            if (user != null)
            {
                if (user.Suspend == 1)
                {
                    ViewBag.Error = "Your account has been suspended. Please contact the administrator.";
                    return View();
                }

                bool isValidPassword = false;

                if (password == user.Password)
                {
                    isValidPassword = true;
                }
                else
                {
                    try { isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.Password); }
                    catch { isValidPassword = false; }
                }

                if (isValidPassword)
                {
                    ResetLoginAttempts(loginKey);
                    HttpContext.Session.SetInt32("user_id", user.User_ID);
                    HttpContext.Session.SetString("User_name", user.User_name ?? "");

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    IncrementFailedAttempts(loginKey);
                    int remaining = MaxFailedAttempts - (_cache.Get<int>($"login_attempts_{loginKey}"));
                    if (remaining > 0)
                        ViewBag.Error = $"Incorrect Password! {remaining} attempt(s) remaining before temporary lock.";
                    else
                    {
                        int mins = BlockDurationMinutes;
                        ViewBag.Error = $"Incorrect Password! Too many failed attempts. Account locked for {mins} minute(s).";
                    }
                }
            }
            else
            {
                IncrementFailedAttempts(loginKey);
                int remaining = MaxFailedAttempts - (_cache.Get<int>($"login_attempts_{loginKey}"));
                if (remaining > 0)
                    ViewBag.Error = $"User not found! {remaining} attempt(s) remaining before temporary lock.";
                else
                {
                    int mins = BlockDurationMinutes;
                    ViewBag.Error = $"User not found! Too many failed attempts. Login locked for {mins} minute(s).";
                }
            }

            return View();
        }

        // 3. Handles Logging Out
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // 4. Shows the Registration Page
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        private async Task<(bool emailSent, string verifyLink)> SendVerificationEmailAsync(User user)
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
            string tokenHash = HashToken(token);
            DateTime now = DateTime.UtcNow;

            var existingTokens = await _context.EmailVerifications
                .Where(v => v.user_id == user.User_ID)
                .ToListAsync();
            _context.EmailVerifications.RemoveRange(existingTokens);

            _context.EmailVerifications.Add(new EmailVerification
            {
                user_id = user.User_ID,
                token_hash = tokenHash,
                expires_at = now.AddHours(24),
                created_at = now
            });
            await _context.SaveChangesAsync();

            string verifyLink = Url.Action("VerifyEmail", "Account", new { token = token }, Request.Scheme)!;
            Console.WriteLine($"[DEV] Email verification link for {user.Email}: {verifyLink}");

            string? smtpHost = _config["Smtp:Host"];
            string? smtpPortStr = _config["Smtp:Port"];
            string? smtpUsername = _config["Smtp:Username"];
            string? smtpPassword = _config["Smtp:Password"];
            string? smtpFromEmail = _config["Smtp:FromEmail"];
            string? smtpFromName = _config["Smtp:FromName"];

            bool hasSmtpConfig = !string.IsNullOrEmpty(smtpHost)
                                 && int.TryParse(smtpPortStr, out int _)
                                 && !string.IsNullOrEmpty(smtpUsername)
                                 && !string.IsNullOrEmpty(smtpPassword)
                                 && !string.IsNullOrEmpty(smtpFromEmail);

            if (!hasSmtpConfig)
            {
                Console.WriteLine("SMTP configuration is missing or incomplete.");
                if (_env.IsDevelopment())
                {
                    user.EmailVerified = true;
                    user.EmailVerifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[DEV] Auto-verified user {user.User_name} because SMTP is not configured.");
                }
                return (false, verifyLink);
            }

            int smtpPort = int.Parse(smtpPortStr!);

            try
            {
                using (var smtp = new SmtpClient(smtpHost!, smtpPort))
                {
                    smtp.Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword);
                    smtp.EnableSsl = true;
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpFromEmail!, smtpFromName ?? "CafeDash Support"),
                        Subject = "CafeDash - Verify Your Email Address",
                        Body = $"<h2>Welcome to CafeDash, {user.User_name}!</h2>" +
                               $"<p>Thank you for registering. Please click the link below to verify your email address and activate your account:</p>" +
                               $"<p><a href='{verifyLink}' style='background-color:#4CAF50;color:white;padding:10px 20px;text-decoration:none;border-radius:4px;display:inline-block;'>Verify Email Address</a></p>" +
                               $"<p>Or copy and paste this link into your browser:</p>" +
                               $"<p><a href='{verifyLink}'>{verifyLink}</a></p>" +
                               $"<p>This link expires in 24 hours.</p>" +
                               $"<p>If you did not create an account with CafeDash, you can safely ignore this email.</p>" +
                               $"<br/><p>Best regards,<br/>CafeDash Support Team</p>",
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(user.Email!);

                    await smtp.SendMailAsync(mailMessage);
                }
                return (true, verifyLink);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Verification email failed to send: " + ex.Message);
                if (_env.IsDevelopment())
                {
                    user.EmailVerified = true;
                    user.EmailVerifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[DEV] Auto-verified user {user.User_name} because SMTP send failed.");
                }
                return (false, verifyLink);
            }
        }

        // 5. Handles Registration Form Submission using EF Core
        [HttpPost]
        public async Task<IActionResult> Register(string user_name, string email, string phone, string address, string password, string confirm_password)
        {
            if (string.IsNullOrWhiteSpace(user_name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            if (password.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }

            if (password != confirm_password)
            {
                ViewBag.Error = "Password and Confirm Password do not match.";
                return View();
            }

            bool userExists = await _context.Users
                .AnyAsync(u => u.User_name == user_name || u.Email == email);

            if (userExists)
            {
                ViewBag.Error = "Username or email already exists. Please use a different one.";
                return View();
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var newUser = new User
            {
                User_name = user_name,
                Email = email,
                Contain_number = phone,
                Address = address,
                Password = hashedPassword,
                Suspend = 0,
                EmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            int result = await _context.SaveChangesAsync();

            if (result > 0)
            {
                TempData["msg"] = "Registration successful! You can now log in with your credentials.";
                return RedirectToAction("Login", "Account");
            }
            else
            {
                ViewBag.Error = "System error: unable to complete registration right now.";
            }

            return View();
        }

        // ==========================================
        // EMAIL VERIFICATION
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Verification token is missing.";
                return View();
            }

            string tokenHash = HashToken(token);
            var verification = await _context.EmailVerifications
                .FirstOrDefaultAsync(v => v.token_hash == tokenHash && v.expires_at > DateTime.UtcNow);

            if (verification == null)
            {
                ViewBag.Error = "This verification link is invalid or has expired. Please request a new one.";
                return View();
            }

            var user = await _context.Users.FindAsync(verification.user_id);
            if (user == null)
            {
                _context.EmailVerifications.Remove(verification);
                await _context.SaveChangesAsync();
                ViewBag.Error = "This verification link is invalid. User account not found.";
                return View();
            }

            if (user.EmailVerified)
            {
                ViewBag.Info = "Your email has already been verified. You can now log in.";
                _context.EmailVerifications.Remove(verification);
                await _context.SaveChangesAsync();
                return View();
            }

            user.EmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            _context.EmailVerifications.Remove(verification);
            await _context.SaveChangesAsync();

            ViewBag.Success = "Your email has been verified successfully! Your account is now activated. You can log in.";
            ViewBag.UserName = user.User_name;
            return View();
        }

        [HttpGet]
        public IActionResult ResendVerificationEmail()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResendVerificationEmail(string email_or_username)
        {
            if (string.IsNullOrWhiteSpace(email_or_username))
            {
                ViewBag.Error = "Please enter your registered email or username.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.User_name == email_or_username.Trim() || u.Email == email_or_username.Trim());

            if (user != null)
            {
                if (user.EmailVerified)
                {
                    ViewBag.Info = "This account's email is already verified. You can log in directly.";
                    return View();
                }

                if (user.Suspend == 1)
                {
                    ViewBag.Error = "Your account has been suspended. Please contact the administrator.";
                    return View();
                }

                var (emailSent, verifyLink) = await SendVerificationEmailAsync(user);
                ViewBag.VerifyLink = verifyLink;
                ViewBag.IsDevelopment = _env.IsDevelopment();

                if (_env.IsDevelopment())
                {
                    if (user.EmailVerified)
                    {
                        ViewBag.Success = $"Verification email was not sent but your account has been auto-verified for development. You can now log in directly. Dev link: {verifyLink}";
                    }
                    else if (emailSent)
                    {
                        ViewBag.Success = $"If the account exists and is not verified, a new verification email has been sent. Dev link: {verifyLink}";
                    }
                    else
                    {
                        ViewBag.Success = $"If the account exists and is not verified, your account has been auto-verified for development (SMTP unavailable). You can now log in directly. Dev link: {verifyLink}";
                    }
                    return View();
                }
            }

            ViewBag.Success = "If the account exists and is not verified, a new verification email has been sent.";
            return View();
        }

        // Helper to hash the token with SHA256
        private string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        // ==========================================
        // FORGOT PASSWORD
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email_or_username)
        {
            if (string.IsNullOrWhiteSpace(email_or_username))
            {
                ViewBag.Error = "Please enter your registered email or username.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.User_name == email_or_username.Trim() || u.Email == email_or_username.Trim());

            if (user != null)
            {
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
                string tokenHash = HashToken(token);
                DateTime now = DateTime.UtcNow;

                // A reset link is only useful if its hashed token is persisted.  Keep
                // one active token per user so a newer request invalidates older links.
                var existingTokens = await _context.PasswordResets
                    .Where(reset => reset.user_id == user.User_ID)
                    .ToListAsync();
                _context.PasswordResets.RemoveRange(existingTokens);
                _context.PasswordResets.Add(new PasswordReset
                {
                    user_id = user.User_ID,
                    token_hash = tokenHash,
                    expires_at = now.AddHours(1),
                    created_at = now
                });
                await _context.SaveChangesAsync();

                string resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme)!;

                Console.WriteLine($"[DEV] Password reset link for {user.Email}: {resetLink}");

                string? smtpHost = _config["Smtp:Host"];
                string? smtpPortStr = _config["Smtp:Port"];
                string? smtpUsername = _config["Smtp:Username"];
                string? smtpPassword = _config["Smtp:Password"];
                string? smtpFromEmail = _config["Smtp:FromEmail"];
                string? smtpFromName = _config["Smtp:FromName"];

                int smtpPort = 0;
                bool hasSmtpConfig = !string.IsNullOrEmpty(smtpHost)
                                     && int.TryParse(smtpPortStr, out smtpPort)
                                     && !string.IsNullOrEmpty(smtpUsername)
                                     && !string.IsNullOrEmpty(smtpPassword)
                                     && !string.IsNullOrEmpty(smtpFromEmail);

                if (hasSmtpConfig)
                {
                    try
                    {
                        using (var smtp = new SmtpClient(smtpHost!, smtpPort))
                        {
                            smtp.Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword);
                            smtp.EnableSsl = true;
                            var mailMessage = new MailMessage
                            {
                                From = new MailAddress(smtpFromEmail!, smtpFromName ?? "CafeDash Support"),
                                Subject = "CafeDash Password Reset",
                                Body = $"<p>You requested a password reset. Click the link below to reset your password:</p><p><a href='{resetLink}'>{resetLink}</a></p><p>This link expires in 1 hour.</p>",
                                IsBodyHtml = true
                            };
                            mailMessage.To.Add(user.Email!);

                            await smtp.SendMailAsync(mailMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Email failed to send: " + ex.Message);
                    }
                }
                else
                {
                    Console.WriteLine("SMTP configuration is missing or incomplete; password reset email was not sent.");
                }
            }

            ViewBag.Success = "If the account exists, a reset link has been sent to the registered email.";
            return View();
        }

        // ==========================================
        // RESET PASSWORD
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !await HasValidResetToken(token))
            {
                ViewBag.Error = "Reset token is missing or invalid.";
            }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string password, string confirm_password)
        {
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Reset token is missing or invalid.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }

            if (password != confirm_password)
            {
                ViewBag.Error = "Password and Confirm Password do not match.";
                return View();
            }

            string tokenHash = HashToken(token);
            var passwordReset = await _context.PasswordResets
                .FirstOrDefaultAsync(reset => reset.token_hash == tokenHash && reset.expires_at > DateTime.UtcNow);

            if (passwordReset == null)
            {
                ViewBag.Error = "This reset link is invalid or has expired. Please request a new one.";
                return View();
            }

            var user = await _context.Users.FindAsync(passwordReset.user_id);
            if (user == null)
            {
                _context.PasswordResets.Remove(passwordReset);
                await _context.SaveChangesAsync();
                ViewBag.Error = "This reset link is invalid. Please request a new one.";
                return View();
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(password);
            _context.PasswordResets.Remove(passwordReset);
            await _context.SaveChangesAsync();

            ViewBag.Token = null;
            ViewBag.Success = "Your password has been updated. You can now login.";
            return View();
        }

        private Task<bool> HasValidResetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult(false);
            }

            string tokenHash = HashToken(token);
            return _context.PasswordResets
                .AnyAsync(reset => reset.token_hash == tokenHash && reset.expires_at > DateTime.UtcNow);
        }

        // Helper method to verify the CAPTCHA with Google
        private async Task<bool> VerifyCaptcha(string captchaResponse)
        {
            if (string.IsNullOrEmpty(captchaResponse)) return false;

            string secretKey = "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe";
            string apiUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={captchaResponse}";

            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(apiUrl, null);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        using (JsonDocument document = JsonDocument.Parse(jsonString))
                        {
                            return document.RootElement.GetProperty("success").GetBoolean();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CAPTCHA verification failed due to network/HTTP error: " + ex.Message);
            }
            return false;
        }
    }
}
