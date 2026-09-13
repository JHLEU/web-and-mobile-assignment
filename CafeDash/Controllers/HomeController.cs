using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using CafeDash.Models;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;

namespace CafeDash.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ==========================================
        // HOMEPAGE
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Title = "Welcome to Cafe Dash";
            var viewModel = new CustomerHomeViewModel();

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Get Top 3 Restaurants for Recommendations
                var cmdTop = new MySqlCommand("SELECT * FROM Restaurant ORDER BY Rating DESC LIMIT 3", conn);
                using (var reader = cmdTop.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.TopRestaurants.Add(new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString(),
                            Rating = reader["Rating"] != DBNull.Value ? Convert.ToDecimal(reader["Rating"]) : 0
                        });
                    }
                }

                // 2. Get All Restaurants for the main list
                var cmdAll = new MySqlCommand("SELECT * FROM Restaurant", conn);
                using (var reader = cmdAll.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.AllRestaurants.Add(new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString(),
                            Restaurant_type = reader["Restaurant_type"].ToString(),
                            Address = reader["Address"].ToString(),
                            Rating = reader["Rating"] != DBNull.Value ? Convert.ToDecimal(reader["Rating"]) : 0
                        });
                    }
                }
            }
            return View(viewModel);
        }

        // ==========================================
        // CAFE MENU PAGE
        // ==========================================
        [HttpGet]
        public IActionResult Cafe(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            var viewModel = new CafeViewModel();

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                // 1. Get Restaurant Details
                var cmdRes = new MySqlCommand("SELECT * FROM Restaurant WHERE Restaurant_ID = @id", conn);
                cmdRes.Parameters.AddWithValue("@id", id);
                using (var reader = cmdRes.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        viewModel.Restaurant = new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString(),
                            Address = reader["Address"].ToString(),
                            Restaurant_type = reader["Restaurant_type"].ToString(),
                            Rating = reader["Rating"] != DBNull.Value ? Convert.ToDecimal(reader["Rating"]) : 0,
                            Contain_number = reader["Contain_number"].ToString(),
                            Email = reader["Email"].ToString()
                        };
                    }
                    else return RedirectToAction("Index"); // Return home if not found
                }

                // 2. Get Menu Items
                var cmdMenu = new MySqlCommand("SELECT * FROM Food WHERE Restaurant_ID = @id", conn);
                cmdMenu.Parameters.AddWithValue("@id", id);
                using (var reader = cmdMenu.ExecuteReader())
                {
                    var menuList = new List<MenuItem>();
                    while (reader.Read())
                    {
                        string type = reader["Food_type"].ToString() ?? "Others";
                        if (string.IsNullOrWhiteSpace(type)) type = "Others";

                        // Logic to check if the item is a drink for the sugar/ice modal
                        string typeLower = type.ToLower();
                        bool isDrink = typeLower.Contains("drink") || typeLower.Contains("beverage") || typeLower.Contains("coffee") || typeLower.Contains("tea");

                        menuList.Add(new MenuItem
                        {
                            Id = Convert.ToInt32(reader["Food_ID"]),
                            Name = reader["Name"].ToString(),
                            Detail = reader["detail"].ToString(),
                            Type = type,
                            Amount = Convert.ToDecimal(reader["amount"]),
                            IsDrink = isDrink,
                            Image = $"/material/{viewModel.Restaurant.Name}/{reader["Name"]}.jpg"
                        });
                    }

                    // Group by type for the UI headers
                    viewModel.GroupedMenuItems = menuList
                        .GroupBy(m => m.Type!)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }
            }
            return View(viewModel);
        }

        // ==========================================
        // CONTACT US PAGE
        // ==========================================
        [HttpGet]
        public IActionResult Contact()
        {
            ViewBag.Title = "Contact Us - Cafe Dash";
            return View();
        }

        [HttpPost]
        public IActionResult Contact(string name, string phone, string email, string message)
        {
            ViewBag.Title = "Contact Us - Cafe Dash";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(message))
            {
                ViewBag.Error = "Please fill in all required fields.";
                return View();
            }

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("INSERT INTO contact_us (name, phone, email, message) VALUES (@name, @phone, @email, @message)", conn);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@phone", phone);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@message", message);

                if (cmd.ExecuteNonQuery() > 0) ViewBag.Success = "Message sent successfully. We will get back to you shortly.";
                else ViewBag.Error = "Unable to save your message. Please try again later.";
            }

            return View();
        }

        // ==========================================
        // BILLS PAGE
        // ==========================================
        [HttpGet]
        public IActionResult Bills()
        {
            ViewBag.Title = "Bills - Cafe Dash";

            // 🔒 REAL SECURE SESSION CHECK
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return RedirectToAction("Login", "Account");

            var bills = new List<Bill>();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand(@"
                    SELECT p.Payment_ID, p.Payment_amount, p.Subtotal_amount, p.SST_amount, 
                           p.Payment_status, p.Created_at, p.Paid_at, r.Name AS Restaurant_Name
                    FROM Payment p
                    LEFT JOIN Restaurant r ON p.Restaurant_ID = r.Restaurant_ID
                    WHERE p.User_ID = @userId ORDER BY p.Created_at DESC", conn);
                cmd.Parameters.AddWithValue("@userId", userId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bills.Add(new Bill
                        {
                            Payment_ID = Convert.ToInt32(reader["Payment_ID"]),
                            Payment_amount = Convert.ToDecimal(reader["Payment_amount"]),
                            Subtotal_amount = Convert.ToDecimal(reader["Subtotal_amount"]),
                            SST_amount = Convert.ToDecimal(reader["SST_amount"]),
                            Payment_status = reader["Payment_status"].ToString(),
                            Created_at = Convert.ToDateTime(reader["Created_at"]),
                            Paid_at = reader["Paid_at"] != DBNull.Value ? Convert.ToDateTime(reader["Paid_at"]) : Convert.ToDateTime(reader["Created_at"]),
                            Display_restaurant_name = reader["Restaurant_Name"].ToString() ?? "Multiple Restaurants"
                        });
                    }
                }

                foreach (var bill in bills)
                {
                    var itemCmd = new MySqlCommand(@"
                        SELECT pi.Item_name, pi.Quantity, pi.Unit_amount, pi.Line_total, pi.Sugar_level, pi.Ice_level, pi.Remark 
                        FROM Payment_Item pi WHERE pi.Payment_ID = @paymentId", conn);
                    itemCmd.Parameters.AddWithValue("@paymentId", bill.Payment_ID);
                    using (var itemReader = itemCmd.ExecuteReader())
                    {
                        while (itemReader.Read())
                        {
                            bill.Items.Add(new BillItem
                            {
                                Item_name = itemReader["Item_name"].ToString(),
                                Quantity = Convert.ToInt32(itemReader["Quantity"]),
                                Unit_amount = Convert.ToDecimal(itemReader["Unit_amount"]),
                                Line_total = Convert.ToDecimal(itemReader["Line_total"]),
                                Sugar_level = itemReader["Sugar_level"].ToString(),
                                Ice_level = itemReader["Ice_level"].ToString(),
                                Remark = itemReader["Remark"].ToString()
                            });
                        }
                    }
                }
            }
            return View(bills);
        }

        // ==========================================
        // SETTINGS & JSON API ENDPOINTS
        // ==========================================
        [HttpGet]
        public IActionResult Settings()
        {
            ViewBag.Title = "Settings - Cafe Dash";

            // 🔒 REAL SECURE SESSION CHECK
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return RedirectToAction("Login", "Account");

            // Fetch the avatar using our CSV helper
            string? avatarPath = Helpers.AvatarCsvHelper.GetAvatarFromCSV(userId);
            ViewBag.ProfileImage = avatarPath ?? "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 150 150%22%3E%3Crect width=%22150%22 height=%22150%22 fill=%22%23D3D3D3%22/%3E%3Ccircle cx=%2275%22 cy=%2250%22 r=%2230%22 fill=%22white%22/%3E%3Cpath d=%22M 30 90 Q 30 80 75 80 Q 120 80 120 90 L 120 150 Q 120 150 75 150 Q 30 150 30 150 Z%22 fill=%22white%22/%3E%3C/svg%3E";

            var user = new User();
            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT User_Name, Email, Contain_number, Address FROM User WHERE User_ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        user.User_name = reader["User_Name"].ToString();
                        user.Email = reader["Email"].ToString();
                        user.Contain_number = reader["Contain_number"].ToString();
                        user.Address = reader["Address"].ToString();
                    }
                }
            }
            return View(user);
        }

        [HttpPost]
        public IActionResult SaveProfile([FromForm] string full_name, [FromForm] string email, [FromForm] string phone, [FromForm] string address)
        {
            // 🔒 SECURE JSON CHECK - Return error instead of redirect so JS fetch doesn't break
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Unauthorized. Please log in." });

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("UPDATE User SET User_Name=@n, Email=@e, Contain_number=@p, Address=@a WHERE User_ID=@id", conn);
                cmd.Parameters.AddWithValue("@n", full_name);
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@p", phone);
                cmd.Parameters.AddWithValue("@a", address);
                cmd.Parameters.AddWithValue("@id", userId);

                if (cmd.ExecuteNonQuery() > 0) return Json(new { success = true });
            }
            return Json(new { success = false, message = "Database update failed." });
        }

        [HttpPost]
        public IActionResult UploadAvatar(IFormFile profileImage)
        {
            // 🔒 SECURE JSON CHECK
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Unauthorized. Please log in." });

            if (profileImage != null && profileImage.Length > 0)
            {
                // Ensure the avatars folder exists in wwwroot/material/avatars
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "material", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // We add a timestamp (like PHP's time()) so the browser doesn't cache the old picture!
                string ext = Path.GetExtension(profileImage.FileName);
                string fileName = $"avatar_{userId}_{DateTimeOffset.Now.ToUnixTimeSeconds()}{ext}";
                string filePath = Path.Combine(uploadsFolder, fileName);

                // Save the image file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    profileImage.CopyTo(stream);
                }

                // Save the path to the CSV
                string relativePath = $"/material/avatars/{fileName}";
                Helpers.AvatarCsvHelper.SaveAvatarToCSV(userId, relativePath);

                // WE MUST RETURN THE FILEPATH SO THE JAVASCRIPT CAN SHOW THE IMAGE!
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
        public IActionResult ChangePassword([FromForm] string current_password, [FromForm] string new_password, [FromForm] string confirm_password)
        {
            // 🔒 SECURE JSON CHECK
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Json(new { success = false, message = "Not logged in" });

            // Validation (Matching your exact PHP rules)
            if (string.IsNullOrEmpty(current_password)) return Json(new { success = false, message = "Please enter current password" });
            if (string.IsNullOrEmpty(new_password)) return Json(new { success = false, message = "Please enter new password" });
            if (string.IsNullOrEmpty(confirm_password)) return Json(new { success = false, message = "Please confirm new password" });
            if (new_password != confirm_password) return Json(new { success = false, message = "New password and confirm password do not match" });
            if (new_password.Length < 6) return Json(new { success = false, message = "New password must be at least 6 characters long" });
            if (current_password == new_password) return Json(new { success = false, message = "New password cannot be the same as current password" });

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT Password FROM User WHERE User_ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);

                string dbPassword = cmd.ExecuteScalar()?.ToString() ?? "";
                bool passwordVerified = false;

                // Support legacy plain text or BCrypt hashed passwords
                if (current_password == dbPassword)
                {
                    passwordVerified = true;
                }
                else
                {
                    try { passwordVerified = BCrypt.Net.BCrypt.Verify(current_password, dbPassword); }
                    catch { passwordVerified = false; }
                }

                if (!passwordVerified) return Json(new { success = false, message = "Current password is incorrect" });

                // Hash the new password and save it
                string hashedNewPassword = BCrypt.Net.BCrypt.HashPassword(new_password);
                var updateCmd = new MySqlCommand("UPDATE User SET Password = @pwd WHERE User_ID = @id", conn);
                updateCmd.Parameters.AddWithValue("@pwd", hashedNewPassword);
                updateCmd.Parameters.AddWithValue("@id", userId);

                if (updateCmd.ExecuteNonQuery() > 0)
                {
                    return Json(new { success = true, message = "Password updated successfully" });
                }
            }

            return Json(new { success = false, message = "Database update failed." });
        }

        // ==========================================
        // CAFE SEARCH JSON API
        // ==========================================
        [HttpGet]
        public IActionResult SearchCafe(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json(new { success = false, found = false, message = "Search keyword is required." });
            }

            string keyword = q.Trim();

            using (var conn = new MySqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Try an exact match first
                var exactCmd = new MySqlCommand("SELECT Restaurant_ID, Name FROM Restaurant WHERE LOWER(Name) = LOWER(@q) LIMIT 1", conn);
                exactCmd.Parameters.AddWithValue("@q", keyword);

                using (var reader = exactCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Json(new
                        {
                            success = true,
                            found = true,
                            cafeId = Convert.ToInt32(reader["Restaurant_ID"]),
                            cafeName = reader["Name"].ToString()
                        });
                    }
                }

                // 2. Fall back to a partial match
                var partialCmd = new MySqlCommand("SELECT Restaurant_ID, Name FROM Restaurant WHERE Name LIKE @likeQ ORDER BY Name ASC LIMIT 1", conn);
                partialCmd.Parameters.AddWithValue("@likeQ", "%" + keyword + "%");

                using (var reader = partialCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Json(new
                        {
                            success = true,
                            found = true,
                            cafeId = Convert.ToInt32(reader["Restaurant_ID"]),
                            cafeName = reader["Name"].ToString()
                        });
                    }
                }
            }

            // 3. No cafe found
            return Json(new { success = true, found = false, message = "No cafe found." });
        }
    }
}