using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeDash.Models
{
    public class Restaurant
    {
        [Key]
        public int Restaurant_ID { get; set; }
        public string? Name { get; set; }
        public string? Restaurant_type { get; set; }
        public string? Email { get; set; }
        public string? Contain_number { get; set; }
        public string? Address { get; set; }

        // Added for Customer Homepage
        [Precision(18, 2)] 
        public decimal Rating { get; set; }
    }

    public class Food
    {
        [Key]
        public int Food_ID { get; set; }
        public int Restaurant_ID { get; set; }
        public string? Name { get; set; }
        public string? Food_type { get; set; }
        public string? Detail { get; set; }

        [Precision(18, 2)] 
        public decimal? Amount { get; set; }
    }

    public class Payment
    {
        [Key]
        public int Payment_ID { get; set; }
        public string? Payment_type { get; set; }

        [Precision(18, 2)]
        public decimal Payment_amount { get; set; }

        [Precision(18, 2)]
        public decimal Subtotal_amount { get; set; }

        [Precision(18, 2)]
        public decimal SST_amount { get; set; }

        public int User_ID { get; set; }
        public int Restaurant_ID { get; set; }
        public string? Payment_status { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime Paid_at { get; set; }
    }

    public class Feedback
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class User
    {
        [Key]
        public int User_ID { get; set; }
        public string? User_name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Contain_number { get; set; }
        public string? Address { get; set; }
        public int Suspend { get; set; }
    }

    public class RestaurantViewModel
    {
        public List<Restaurant> AllRestaurants { get; set; } = new List<Restaurant>();
        public Restaurant? SelectedRestaurant { get; set; }
        public List<Food> SelectedRestaurantFoods { get; set; } = new List<Food>();
    }

    public class CustomerHomeViewModel
    {
        public List<Restaurant> TopRestaurants { get; set; } = new List<Restaurant>();
        public List<Restaurant> AllRestaurants { get; set; } = new List<Restaurant>();
    }

    public class MenuItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Detail { get; set; }
        public string? Type { get; set; }
        public decimal Amount { get; set; }
        public bool IsDrink { get; set; }
        public string? Image { get; set; }
    }

    public class CafeViewModel
    {
        public Restaurant? Restaurant { get; set; }
        public Dictionary<string, List<MenuItem>> GroupedMenuItems { get; set; } = new();
    }

    public class ContactUs
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Message { get; set; }
        public DateTime Created_at { get; set; } = DateTime.Now;
    }

    
    public class PaymentItem
    {
        [Key]
        public int Payment_Item_ID { get; set; }
        public int Payment_ID { get; set; }
        public int? Food_ID { get; set; }
        public string? Item_name { get; set; }
        public string? Item_type { get; set; }
        [Precision(18, 2)]
        public decimal Unit_amount { get; set; }
        public int Quantity { get; set; }
        [Precision(18, 2)]
        public decimal Line_total { get; set; }
        public string? Sugar_level { get; set; }
        public string? Ice_level { get; set; }
        public string? Remark { get; set; }
        public DateTime Created_at { get; set; }
    }

    public class Driver
    {
        [Key]
        public int Driver_ID { get; set; }
        public string? Driver_Name { get; set; }
        public string? Plate_number { get; set; }
    }

    public class Admin
    {
        [Key]
        public int Admin_ID { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string? Contain_number { get; set; }
        public string? Email { get; set; }
    }

    public class PasswordReset
    {
        [Key]
        public int Id { get; set; }
        public int user_id { get; set; }
        public string? token_hash { get; set; }
        public DateTime expires_at { get; set; }
        public DateTime created_at { get; set; }
    }

    public class Bill
    {
        public int Payment_ID { get; set; }
        [Precision(18, 2)]
        public decimal Payment_amount { get; set; }
        [Precision(18, 2)]
        public decimal Subtotal_amount { get; set; }
        [Precision(18, 2)]
        public decimal SST_amount { get; set; }
        public string? Payment_status { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime? Paid_at { get; set; }
        public string? Display_restaurant_name { get; set; }
        public List<BillItem> Items { get; set; } = new();
    }

    public class BillItem
    {
        public string? Item_name { get; set; }
        public int Quantity { get; set; }
        [Precision(18, 2)]
        public decimal Unit_amount { get; set; }
        [Precision(18, 2)]
        public decimal Line_total { get; set; }
        public string? Sugar_level { get; set; }
        public string? Ice_level { get; set; }
        public string? Remark { get; set; }
    }
}