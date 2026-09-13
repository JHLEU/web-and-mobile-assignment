using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Microsoft.AspNetCore.Http;
using CafeDash.Models;
using System;
using System.Collections.Generic;

namespace CafeDash.Controllers;

public class AdminController : Controller
{
    private readonly string _connectionString;

    public AdminController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    // =========================================================
    // 1. PAGE VIEWS (GET METHODS)
    // =========================================================
    [HttpGet]
    [HttpGet]
    public IActionResult Ranking()
    {
        // 🔒 Secure the actual page view so it kicks you out if you aren't logged in!
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null || adminId == 0)
        {
            return RedirectToAction("Login", "Admin");
        }

        ViewBag.Title = "Restaurant Performance Ranking";
        return View();
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.Title = "Admin Dashboard";
        var feedbackList = new List<Feedback>();

        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("SELECT * FROM contact_us ORDER BY created_at DESC", conn);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    feedbackList.Add(new Feedback
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        Name = reader["name"].ToString(),
                        Email = reader["email"].ToString(),
                        Phone = reader["phone"].ToString(),
                        Message = reader["message"].ToString(),
                        CreatedAt = Convert.ToDateTime(reader["created_at"])
                    });
                }
            }
        }
        return View(feedbackList);
    }

    [HttpGet]
    public IActionResult Members(string search)
    {
        ViewBag.Title = "Member Management";
        ViewBag.SearchTerm = search; // Remembers what you typed in the search box
        var userList = new List<User>();

        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            string query = "SELECT * FROM User";
            if (!string.IsNullOrEmpty(search))
                query += " WHERE User_name LIKE @search OR Email LIKE @search";

            var cmd = new MySqlCommand(query, conn);
            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("@search", "%" + search + "%");

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    userList.Add(new User
                    {
                        User_ID = Convert.ToInt32(reader["User_ID"]),
                        User_name = reader["User_name"].ToString(),
                        Email = reader["Email"].ToString(),
                        Suspend = Convert.ToInt32(reader["Suspend"])
                    });
                }
            }
        }
        return View(userList);
    }

    [HttpGet] public IActionResult Settings() { return View(); }
    [HttpGet] public IActionResult Login() { return View(); }

    [HttpGet]
    public IActionResult Restaurants(int? edit_id)
    {
        ViewBag.Title = "Manage Restaurants - Admin";
        var viewModel = new RestaurantViewModel();

        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();

            if (edit_id.HasValue && edit_id.Value > 0)
            {
                var cmdRes = new MySqlCommand("SELECT * FROM Restaurant WHERE Restaurant_ID = @id", conn);
                cmdRes.Parameters.AddWithValue("@id", edit_id.Value);
                using (var reader = cmdRes.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        viewModel.SelectedRestaurant = new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString()!,
                            Restaurant_type = reader["Restaurant_type"].ToString()!,
                            Email = reader["Email"].ToString()!,
                            Contain_number = reader["Contain_number"].ToString()!,
                            Address = reader["Address"].ToString()!
                        };
                    }
                }

                var cmdFood = new MySqlCommand("SELECT * FROM Food WHERE Restaurant_ID = @id", conn);
                cmdFood.Parameters.AddWithValue("@id", edit_id.Value);
                using (var reader = cmdFood.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.SelectedRestaurantFoods.Add(new Food
                        {
                            Food_ID = Convert.ToInt32(reader["Food_ID"]),
                            Name = reader["Name"].ToString()!,
                            Food_type = reader["Food_type"].ToString()!,
                            Detail = reader["detail"].ToString()!,
                            Amount = reader["amount"] != DBNull.Value ? Convert.ToDecimal(reader["amount"]) : null
                        });
                    }
                }
            }
            else
            {
                var cmdAll = new MySqlCommand("SELECT * FROM Restaurant ORDER BY Restaurant_ID DESC", conn);
                using (var reader = cmdAll.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.AllRestaurants.Add(new Restaurant
                        {
                            Restaurant_ID = Convert.ToInt32(reader["Restaurant_ID"]),
                            Name = reader["Name"].ToString()!,
                            Restaurant_type = reader["Restaurant_type"].ToString()!,
                            Address = reader["Address"].ToString()!
                        });
                    }
                }
            }
        }
        return View(viewModel);
    }

    // =========================================================
    // 2. ADMIN LOGIN & LOGOUT 
    // =========================================================
    [HttpPost]
    public IActionResult Admin_login_btn(string admin_name, string admin_password)
    {
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("SELECT Admin_ID, Name, Password FROM Admin WHERE Name = @name", conn);
            cmd.Parameters.AddWithValue("@name", admin_name);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (admin_password == reader["Password"].ToString())
                    {
                        HttpContext.Session.SetInt32("admin_id", Convert.ToInt32(reader["Admin_ID"]));
                        HttpContext.Session.SetString("admin_name", reader["Name"].ToString()!);
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        TempData["error_message"] = "Incorrect Admin Password!";
                    }
                }
                else
                {
                    TempData["error_message"] = "Admin account not found!";
                }
            }
        }
        return RedirectToAction("Login");
    }

    [HttpPost]
    public IActionResult Logout_btn()
    {
        HttpContext.Session.Clear(); // This destroys the login session
        return RedirectToAction("Login", "Admin"); // Sends you back to the login page
    }

    // =========================================================
    // 3. RESTAURANT MANAGEMENT (ADD / UPDATE / DELETE)
    // =========================================================
    [HttpPost]
    public IActionResult AddRestaurant(string res_name, string res_address, string res_type, string res_email, string res_phone)
    {
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("INSERT INTO Restaurant (Name, Address, Restaurant_type, Email, Contain_number) VALUES (@name, @address, @type, @email, @phone)", conn);
            cmd.Parameters.AddWithValue("@name", res_name);
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@type", res_type ?? "");
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");

            if (cmd.ExecuteNonQuery() > 0) TempData["msg"] = "Restaurant added successfully!";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public IActionResult UpdateRestaurant(int restaurant_id, string res_name, string res_type, string res_email, string res_phone, string res_address)
    {
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("UPDATE Restaurant SET Name = @name, Restaurant_type = @type, Email = @email, Contain_number = @phone, Address = @address WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@name", res_name);
            cmd.Parameters.AddWithValue("@type", res_type);
            cmd.Parameters.AddWithValue("@email", res_email ?? "");
            cmd.Parameters.AddWithValue("@phone", res_phone ?? "");
            cmd.Parameters.AddWithValue("@address", res_address ?? "");
            cmd.Parameters.AddWithValue("@id", restaurant_id);

            if (cmd.ExecuteNonQuery() > 0) TempData["msg"] = "Restaurant updated successfully!";
        }
        return RedirectToAction("Restaurants");
    }

    [HttpPost]
    public IActionResult DeleteRestaurant(int restaurant_id)
    {
        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new MySqlCommand("DELETE FROM Restaurant WHERE Restaurant_ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", restaurant_id);
            cmd.ExecuteNonQuery();
            TempData["msg"] = "Restaurant removed successfully.";
        }
        return RedirectToAction("Restaurants");
    }

    // =========================================================
    // 4. SETTINGS & PASSWORD
    // =========================================================
    [HttpPost]
    public IActionResult UpdateAdminPassword(string current_password, string new_password, string confirm_password)
    {
        int? adminId = HttpContext.Session.GetInt32("admin_id");
        if (adminId == null) return RedirectToAction("Login", "Admin");

        if (new_password != confirm_password)
        {
            TempData["msg"] = "New passwords do not match!";
            return RedirectToAction("Settings");
        }

        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();
            var selectCmd = new MySqlCommand("SELECT Password FROM Admin WHERE Admin_ID = @id", conn);
            selectCmd.Parameters.AddWithValue("@id", adminId);
            string currentDbPassword = selectCmd.ExecuteScalar()?.ToString() ?? "";

            if (current_password == currentDbPassword)
            {
                var updateCmd = new MySqlCommand("UPDATE Admin SET Password = @newPass WHERE Admin_ID = @id", conn);
                updateCmd.Parameters.AddWithValue("@newPass", new_password);
                updateCmd.Parameters.AddWithValue("@id", adminId);
                if (updateCmd.ExecuteNonQuery() > 0) TempData["msg"] = "Password updated successfully!";
            }
            else
            {
                TempData["msg"] = "Current password is incorrect.";
            }
        }
        return RedirectToAction("Settings");
    }

    // =========================================================
    // 5. RESTAURANT RANKING JSON API
    // =========================================================
    [HttpGet]
    public IActionResult GetRanking(int limit = 10)
    {
        // 🔒 SECURE ADMIN SESSION CHECK
        int adminId = HttpContext.Session.GetInt32("admin_id") ?? 0;
        if (adminId == 0)
        {
            return Unauthorized(new { success = false, message = "Unauthorized." });
        }

        // Keep the limit within boundaries
        if (limit <= 0) limit = 10;
        if (limit > 100) limit = 100;

        var rows = new List<object>();

        using (var conn = new MySqlConnection(_connectionString))
        {
            conn.Open();

            var sql = @"
                SELECT 
                    r.Restaurant_ID, 
                    r.Name, 
                    COUNT(p.Payment_ID) AS transaction_count, 
                    COALESCE(SUM(p.Payment_amount), 0) AS total_sales
                FROM Restaurant r
                LEFT JOIN Payment p 
                    ON p.Restaurant_ID = r.Restaurant_ID 
                    AND p.Payment_status = 'SUCCEEDED'
                GROUP BY r.Restaurant_ID, r.Name
                ORDER BY transaction_count DESC, total_sales DESC, r.Name ASC
                LIMIT @limit";

            var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@limit", limit);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add(new
                    {
                        restaurantId = Convert.ToInt32(reader["Restaurant_ID"]),
                        restaurantName = reader["Name"].ToString(),
                        transactionCount = Convert.ToInt32(reader["transaction_count"]),
                        totalSales = Math.Round(Convert.ToDecimal(reader["total_sales"]), 2)
                    });
                }
            }
        }

        return Json(new
        {
            success = true,
            limit = limit,
            ranking = rows
        });
    }
}