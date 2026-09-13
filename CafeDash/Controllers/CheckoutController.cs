using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Stripe;
using MySqlConnector;

namespace CafeDash.Models
{
    public class CartItem
    {
        public string CartId { get; set; } = Guid.NewGuid().ToString();
        public int FoodId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string ItemType { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitAmount { get; set; }
        public decimal LineTotal => Quantity * UnitAmount;
        public string Sugar { get; set; } = "";
        public string Ice { get; set; } = "";
        public string Remark { get; set; } = "";
    }

    public class CheckoutViewModel
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public decimal Subtotal { get; set; }
        public decimal Sst { get; set; }
        public decimal GrandTotal { get; set; }
        public string PublishableKey { get; set; } = "";
    }
}

namespace CafeDash.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public CheckoutController(IConfiguration config)
        {
            _config = config;
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        // ==========================================
        // 1. SESSION HELPERS
        // ==========================================
        private List<Models.CartItem> GetCart()
        {
            var json = HttpContext.Session.GetString("cart");
            return string.IsNullOrEmpty(json) ? new List<Models.CartItem>() : JsonSerializer.Deserialize<List<Models.CartItem>>(json)!;
        }

        private void SaveCart(List<Models.CartItem> cart)
        {
            HttpContext.Session.SetString("cart", JsonSerializer.Serialize(cart));
        }

        private object GetCartSummary()
        {
            var cart = GetCart();
            decimal subtotal = cart.Sum(i => i.LineTotal);
            decimal sst = Math.Round(subtotal * 0.06m, 2);
            decimal grandTotal = Math.Round(subtotal + sst, 2);

            // Returns the exact JSON structure your app.js file expects
            return new
            {
                success = true,
                items = cart.Select(i => new {
                    cartId = i.CartId,
                    foodId = i.FoodId,
                    restaurantId = i.RestaurantId,
                    restaurantName = i.RestaurantName,
                    itemName = i.ItemName,
                    itemType = i.ItemType,
                    quantity = i.Quantity,
                    unitAmount = i.UnitAmount,
                    lineTotal = i.LineTotal,
                    sugar = i.Sugar,
                    ice = i.Ice,
                    remark = i.Remark
                }),
                subtotal = subtotal,
                sst = sst,
                grandTotal = grandTotal
            };
        }

        // ==========================================
        // 2. CART API (Replaces cart_actions.php)
        // ==========================================
        [Route("Cart/Api")]
        public IActionResult CartApi([FromForm] string action, [FromQuery(Name = "action")] string actionGet, [FromForm] string cart_id, [FromForm] int quantity, [FromForm(Name = "food_id")] int food_id, [FromForm(Name = "restaurant_id")] int restaurant_id, [FromForm(Name = "restaurant_name")] string restaurant_name, [FromForm(Name = "item_name")] string item_name, [FromForm(Name = "item_type")] string item_type, [FromForm(Name = "unit_amount")] decimal unit_amount, [FromForm] string sugar, [FromForm] string ice, [FromForm] string remark)
        {
            string act = action ?? actionGet ?? "get";
            var cart = GetCart();

            if (act == "add")
            {
                cart.Add(new Models.CartItem
                {
                    CartId = "c_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    FoodId = food_id,
                    RestaurantId = restaurant_id,
                    RestaurantName = restaurant_name ?? "",
                    ItemName = item_name ?? "",
                    ItemType = item_type ?? "",
                    Quantity = Math.Max(1, quantity),
                    UnitAmount = unit_amount,
                    Sugar = sugar ?? "",
                    Ice = ice ?? "",
                    Remark = remark ?? ""
                });
                SaveCart(cart);
            }
            else if (act == "update")
            {
                var item = cart.FirstOrDefault(i => i.CartId == cart_id);
                if (item != null) { item.Quantity = Math.Max(1, quantity); SaveCart(cart); }
            }
            else if (act == "remove")
            {
                cart.RemoveAll(i => i.CartId == cart_id); SaveCart(cart);
            }
            else if (act == "clear")
            {
                cart.Clear(); SaveCart(cart);
            }

            return Json(GetCartSummary());
        }

        // ==========================================
        // 3. PAYMENT VIEW (Replaces payment.php)
        // ==========================================
        [HttpGet]
        public IActionResult Payment()
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return RedirectToAction("Login", "Account");

            var cart = GetCart();
            decimal subtotal = cart.Sum(i => i.LineTotal);
            decimal sst = Math.Round(subtotal * 0.06m, 2);

            var vm = new Models.CheckoutViewModel
            {
                Items = cart,
                Subtotal = subtotal,
                Sst = sst,
                GrandTotal = subtotal + sst,
                PublishableKey = _config["Stripe:PublishableKey"]! // Grab Stripe Key securely
            };

            return View(vm);
        }

        // ==========================================
        // 4. CREATE INTENT (Replaces create_payment_intent.php)
        // ==========================================
        [HttpPost]
        public IActionResult CreatePaymentIntent()
        {
            var cart = GetCart();
            if (!cart.Any()) return BadRequest(new { error = "Cart is empty" });

            decimal subtotal = cart.Sum(i => i.LineTotal);
            long amountInSen = (long)((subtotal + Math.Round(subtotal * 0.06m, 2)) * 100);

            try
            {
                var service = new PaymentIntentService();
                var intent = service.Create(new PaymentIntentCreateOptions
                {
                    Amount = amountInSen,
                    Currency = "myr",
                    Metadata = new Dictionary<string, string> { { "source", "cafe_dash" } }
                });
                return Json(new { clientSecret = intent.ClientSecret });
            }
            catch (StripeException e)
            {
                return BadRequest(new { error = "Stripe API: " + e.StripeError.Message });
            }
        }

        // ==========================================
        // 5. FINALIZE ORDER (Replaces finalize_order.php)
        // ==========================================
        public class FinalizeReq { public string payment_intent_id { get; set; } = ""; }

        [HttpPost]
        public IActionResult FinalizeOrder([FromBody] FinalizeReq req)
        {
            int userId = HttpContext.Session.GetInt32("user_id") ?? 0;
            if (userId == 0) return Unauthorized(new { success = false, message = "Please login first." });

            var cart = GetCart();
            if (!cart.Any()) return Json(new { success = false, message = "Cart is empty." });

            try
            {
                var service = new PaymentIntentService();
                var intent = service.Get(req.payment_intent_id);

                if (intent.Status != "succeeded") return Json(new { success = false, message = "Payment is not successful yet." });

                decimal subtotal = cart.Sum(i => i.LineTotal);
                decimal sst = Math.Round(subtotal * 0.06m, 2);
                decimal grandTotal = subtotal + sst;

                int? restaurantId = null;
                var uniqueRes = cart.Select(i => i.RestaurantId).Distinct().ToList();
                if (uniqueRes.Count == 1 && uniqueRes[0] > 0) restaurantId = uniqueRes[0];

                string paymentType = intent.PaymentMethodTypes?.FirstOrDefault() ?? "card";

                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();

                    // Idempotency Guard
                    var checkCmd = new MySqlCommand("SELECT Payment_ID FROM Payment WHERE Provider_payment_id = @pid LIMIT 1", conn);
                    checkCmd.Parameters.AddWithValue("@pid", intent.Id);
                    var existingId = checkCmd.ExecuteScalar();
                    if (existingId != null)
                    {
                        HttpContext.Session.Remove("cart");
                        return Json(new { success = true, payment_id = Convert.ToInt32(existingId) });
                    }

                    // Secure Database Transaction
                    using (var transaction = conn.BeginTransaction())
                    {
                        var headerCmd = new MySqlCommand("INSERT INTO Payment (User_ID, Restaurant_ID, Payment_type, Payment_amount, Subtotal_amount, SST_amount, Currency, Payment_status, Provider, Provider_payment_id, Paid_at) VALUES (@uid, @rid, @type, @amt, @sub, @sst, @cur, 'SUCCEEDED', 'STRIPE', @pid, @paid)", conn, transaction);
                        headerCmd.Parameters.AddWithValue("@uid", userId);
                        headerCmd.Parameters.AddWithValue("@rid", restaurantId);
                        headerCmd.Parameters.AddWithValue("@type", paymentType);
                        headerCmd.Parameters.AddWithValue("@amt", grandTotal);
                        headerCmd.Parameters.AddWithValue("@sub", subtotal);
                        headerCmd.Parameters.AddWithValue("@sst", sst);
                        headerCmd.Parameters.AddWithValue("@cur", intent.Currency.ToUpper());
                        headerCmd.Parameters.AddWithValue("@pid", intent.Id);
                        headerCmd.Parameters.AddWithValue("@paid", DateTime.Now);
                        headerCmd.ExecuteNonQuery();

                        int newPaymentId = (int)headerCmd.LastInsertedId;

                        foreach (var item in cart)
                        {
                            var itemCmd = new MySqlCommand("INSERT INTO Payment_Item (Payment_ID, Food_ID, Item_name, Item_type, Unit_amount, Quantity, Line_total, Sugar_level, Ice_level, Remark) VALUES (@pid, @fid, @name, @type, @unit, @qty, @total, @sugar, @ice, @remark)", conn, transaction);
                            itemCmd.Parameters.AddWithValue("@pid", newPaymentId);
                            itemCmd.Parameters.AddWithValue("@fid", item.FoodId > 0 ? item.FoodId : null);
                            itemCmd.Parameters.AddWithValue("@name", item.ItemName);
                            itemCmd.Parameters.AddWithValue("@type", item.ItemType);
                            itemCmd.Parameters.AddWithValue("@unit", item.UnitAmount);
                            itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                            itemCmd.Parameters.AddWithValue("@total", item.LineTotal);
                            itemCmd.Parameters.AddWithValue("@sugar", item.Sugar);
                            itemCmd.Parameters.AddWithValue("@ice", item.Ice);
                            itemCmd.Parameters.AddWithValue("@remark", item.Remark);
                            itemCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        HttpContext.Session.Remove("cart");
                        return Json(new { success = true, payment_id = newPaymentId });
                    }
                }
            }
            catch (Exception e)
            {
                return Json(new { success = false, message = e.Message });
            }
        }

        // ==========================================
        // 6. SUCCESS VIEW (Replaces Payment_success.php)
        // ==========================================
        [HttpGet]
        public IActionResult Success(int payment_id)
        {
            ViewBag.Title = "Payment Successful - Cafe Dash";
            ViewBag.PaymentId = payment_id;
            return View();
        }
    }
}