using CafeDash.Data;
using CafeDash.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeDash.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // HOMEPAGE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Title = "Welcome to Cafe Dash";
            var viewModel = new CustomerHomeViewModel();

            // 1. Get Top 3 Restaurants for Recommendations using EF Core
            viewModel.TopRestaurants = await _context.Set<Restaurant>()
                .OrderByDescending(r => r.Rating)
                .Take(3)
                .ToListAsync();

            // 2. Get All Restaurants for the main list using EF Core
            viewModel.AllRestaurants = await _context.Set<Restaurant>()
                .ToListAsync();

            return View(viewModel);
        }

        // ==========================================
        // CAFE MENU PAGE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Cafe(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            var viewModel = new CafeViewModel();

            // 1. Get Restaurant Details via EF Core
            var restaurant = await _context.Restaurants
                .FirstOrDefaultAsync(r => r.Restaurant_ID == id);

            if (restaurant == null) return RedirectToAction("Index");

            viewModel.Restaurant = restaurant;

            // 2. Get Menu Items directly from the Foods table via EF Core
            var dbFoods = await _context.Foods
                .Where(f => f.Restaurant_ID == id)
                .ToListAsync();

            var menuList = new List<MenuItem>();

            foreach (var f in dbFoods)
            {
                string type = f.Food_type ?? "Others";
                if (string.IsNullOrWhiteSpace(type)) type = "Others";

                string typeLower = type.ToLower();
                bool isDrink = typeLower.Contains("drink") || typeLower.Contains("beverage") || typeLower.Contains("coffee") || typeLower.Contains("tea");

                menuList.Add(new MenuItem
                {
                    Id = f.Food_ID,
                    Name = f.Name,
                    Detail = f.Detail,
                    Type = type,
                    Amount = f.Amount ?? 0,
                    IsDrink = isDrink,
                    Image = $"/material/{viewModel.Restaurant.Name}/{f.Name}.jpg"
                });
            }

            // Group by type for the UI headers
            viewModel.GroupedMenuItems = menuList
                .GroupBy(m => m.Type!)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View(viewModel);
        }

        // ==========================================
        // CONTACT US PAGE
        // ==========================================
        // 1. This loads the page when you click the menu link (GET)
        [HttpGet]
        public IActionResult Contact()
        {
            ViewBag.Title = "Contact Us - Cafe Dash";
            return View();
        }

        // 2. This saves the data when you click submit (POST)
        [HttpPost]
        public async Task<IActionResult> Contact(string name, string phone, string email, string message)
        {
            ViewBag.Title = "Contact Us - Cafe Dash";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
            {
                ViewBag.Error = "Please fill in all required fields.";
                return View();
            }

            // Using GETDATE() to safely fill the NOT NULL Created_at column
            int result = await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO ContactUs (Name, Phone, Email, Message, Created_at) VALUES ({0}, {1}, {2}, {3}, GETDATE())",
                name, phone, email, message);

            if (result > 0) ViewBag.Success = "Message sent successfully. We will get back to you shortly.";
            else ViewBag.Error = "Unable to save your message. Please try again later.";

            return View();
        }

        // ==========================================
        // BILLS PAGE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Bills()
        {
            ViewBag.Title = "Bills - Cafe Dash";

            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return RedirectToAction("Login", "Account");

            // 1. We query using the safe BillDto that has no lists!
            var billDtos = await _context.Database.SqlQueryRaw<BillDto>(@"
        SELECT p.Payment_ID, p.Payment_amount, p.Subtotal_amount, p.SST_amount, 
               p.Payment_status, p.Created_at, p.Paid_at, r.Name AS Display_restaurant_name
        FROM Payment p
        LEFT JOIN Restaurants r ON p.Restaurant_ID = r.Restaurant_ID
        WHERE p.User_ID = {0} ORDER BY p.Created_at DESC", userId)
        .ToListAsync();

            var bills = new List<Bill>();

            // 2. We convert the DTOs back into your normal Bill model
            foreach (var dto in billDtos)
            {
                var bill = new Bill
                {
                    Payment_ID = dto.Payment_ID,
                    Payment_amount = dto.Payment_amount,
                    Subtotal_amount = dto.Subtotal_amount,
                    SST_amount = dto.SST_amount,
                    Payment_status = dto.Payment_status,
                    Created_at = dto.Created_at,
                    Paid_at = dto.Paid_at,
                    Display_restaurant_name = dto.Display_restaurant_name
                };

                // 3. Now we fetch the items safely
                var items = await _context.Database.SqlQueryRaw<BillItem>(@"
            SELECT pi.Item_name, pi.Quantity, pi.Unit_amount, pi.Line_total, pi.Sugar_level, pi.Ice_level, pi.Remark 
            FROM Payment_Items pi WHERE pi.Payment_ID = {0}", bill.Payment_ID)
                    .ToListAsync();

                bill.Items = items;
                bills.Add(bill);
            }

            return View(bills);
        }

        // ==========================================
        // SETTINGS & JSON API ENDPOINTS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            ViewBag.Title = "Settings - Cafe Dash";

            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return RedirectToAction("Login", "Account");

            // Look up the user's unique avatar directly from the folder by their user ID
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "material", "avatars");
            string? avatarPath = null;
            if (Directory.Exists(uploadsFolder))
            {
                var userFile = Directory.GetFiles(uploadsFolder, $"avatar_{userId}_*")
                                        .OrderByDescending(f => System.IO.File.GetCreationTime(f))
                                        .FirstOrDefault();
                if (userFile != null)
                {
                    avatarPath = $"/material/avatars/{Path.GetFileName(userFile)}";
                }
            }

            ViewBag.ProfileImage = avatarPath ?? "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 150 150%22%3E%3Crect width=%22150%22 height=%22150%22 fill=%22%23D3D3D3%22/%3E%3Ccircle cx=%2275%22 cy=%2250%22 r=%2230%22 fill=%22white%22/%3E%3Cpath d=%22M 30 90 Q 30 80 75 80 Q 120 80 120 90 L 120 150 Q 120 150 75 150 Q 30 150 30 150 Z%22 fill=%22white%22/%3E%3C/svg%3E";

            var user = await _context.Users
                .Where(u => u.User_ID == userId)
                .Select(u => new User { User_name = u.User_name, Email = u.Email, Contain_number = u.Contain_number, Address = u.Address })
                .FirstOrDefaultAsync() ?? new User();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> SaveProfile([FromForm] string full_name, [FromForm] string email, [FromForm] string phone, [FromForm] string address)
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Unauthorized. Please log in." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.User_ID == userId);
            if (user != null)
            {
                user.User_name = full_name;
                user.Email = email;
                user.Contain_number = phone;
                user.Address = address;

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Database update failed." });
        }

        [HttpPost]
        [HttpPost]
        public IActionResult UploadAvatar(IFormFile profileImage)
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Unauthorized. Please log in." });

            if (profileImage != null && profileImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "material", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Explicitly use System.IO.File to prevent naming conflicts with the controller method
                var oldFiles = Directory.GetFiles(uploadsFolder, $"avatar_{userId}_*");
                foreach (var oldFile in oldFiles)
                {
                    try { System.IO.File.Delete(oldFile); } catch { }
                }

                string ext = Path.GetExtension(profileImage.FileName);
                string fileName = $"avatar_{userId}_{DateTimeOffset.Now.ToUnixTimeSeconds()}{ext}";
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    profileImage.CopyTo(stream);
                }

                string relativePath = $"/material/avatars/{fileName}";

                return Json(new
                {
                    success = true,
                    message = "Upload successful",
                    filepath = relativePath,
                    filename = fileName
                });
            }

            return Json(new { success = false, message = "No image file selected." });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromForm] string current_password, [FromForm] string new_password, [FromForm] string confirm_password)
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Not logged in" });

            if (string.IsNullOrEmpty(current_password)) return Json(new { success = false, message = "Please enter current password" });
            if (string.IsNullOrEmpty(new_password)) return Json(new { success = false, message = "Please enter new password" });
            if (string.IsNullOrEmpty(confirm_password)) return Json(new { success = false, message = "Please confirm new password" });
            if (new_password != confirm_password) return Json(new { success = false, message = "New password and confirm password do not match" });
            if (new_password.Length < 6) return Json(new { success = false, message = "New password must be at least 6 characters long" });
            if (current_password == new_password) return Json(new { success = false, message = "New password cannot be the same as current password" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.User_ID == userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            bool passwordVerified = false;
            if (current_password == user.Password)
            {
                passwordVerified = true;
            }
            else
            {
                try { passwordVerified = BCrypt.Net.BCrypt.Verify(current_password, user.Password); }
                catch { passwordVerified = false; }
            }

            if (!passwordVerified) return Json(new { success = false, message = "Current password is incorrect" });

            user.Password = BCrypt.Net.BCrypt.HashPassword(new_password);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Password updated successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> SearchCafe(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(new { success = false, found = false, message = "Search keyword is required." });
            }

            string keyword = q.Trim();

            // Try exact match first
            var exactCafe = await _context.Set<Restaurant>()
                .FirstOrDefaultAsync(r => r.Name!.ToLower() == keyword.ToLower());

            if (exactCafe != null)
            {
                return Json(new
                {
                    success = true,
                    found = true,
                    cafeId = exactCafe.Restaurant_ID,
                    cafeName = exactCafe.Name
                });
            }

            // Fall back to partial match
            var partialCafe = await _context.Set<Restaurant>()
                .Where(r => r.Name!.Contains(keyword))
                .OrderBy(r => r.Name)
                .FirstOrDefaultAsync();

            if (partialCafe != null)
            {
                return Json(new
                {
                    success = true,
                    found = true,
                    cafeId = partialCafe.Restaurant_ID,
                    cafeName = partialCafe.Name
                });
            }

            return Json(new { success = true, found = false, message = "No cafe found." });
        }
    }

    // Helper DTO for mapping raw queries safely
    // Helper DTO for mapping raw queries safely
    public class FoodItemDTO
    {
        public int Food_ID { get; set; }
        public string? Name { get; set; }
        public string? Food_type { get; set; }
        public string? detail { get; set; }
        public decimal? amount { get; set; }
    }

    // BillDto goes right here!
    public class BillDto
    {
        public int Payment_ID { get; set; }
        public decimal Payment_amount { get; set; }
        public decimal Subtotal_amount { get; set; }
        public decimal SST_amount { get; set; }
        public string? Payment_status { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime? Paid_at { get; set; }
        public string? Display_restaurant_name { get; set; }
    }

} // <--- THIS MUST BE THE LAST LINE OF THE FILE (It closes the namespace)