using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

string defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=cafedash_db;Trusted_Connection=True;TrustServerCertificate=True;";

if (string.IsNullOrEmpty(builder.Configuration["Smtp:Host"]) ||
    string.IsNullOrEmpty(builder.Configuration["Smtp:Username"]) ||
    string.IsNullOrEmpty(builder.Configuration["Smtp:Password"]) ||
    string.IsNullOrEmpty(builder.Configuration["Smtp:FromEmail"]))
{
    Console.WriteLine("[WARN] SMTP configuration is missing. In Development mode, users will be auto-verified so testing is not blocked.");
}

// Add database services to the container.
builder.Services.AddDbContext<CafeDash.Data.ApplicationDbContext>(options =>
    options.UseSqlServer(defaultConnection));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Add session for Admin Login
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CafeDash.Data.ApplicationDbContext>();
    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Database migration warning: " + ex.Message);
    }
    //await CafeDash.Data.DatabaseSeeder.SeedRestaurantsAsync(context);
}

// Initialize Stripe using your Secret Key from appsettings.json
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // This allows the wwwroot folder to work!

app.UseRouting();

app.UseAuthorization();
app.UseSession(); // Enable session

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Welcome}/{id?}");

app.Run();
