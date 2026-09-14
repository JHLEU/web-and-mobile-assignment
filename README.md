# CafeDash

An ASP.NET Core MVC web application for browsing cafes and restaurants, ordering food, and
managing the catalogue from an admin dashboard. Built as a web-and-mobile assignment.

## Prerequisites

| Requirement | Notes |
| --- | --- |
| Windows | The app uses SQL Server LocalDB, which is Windows-only. |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Verify with `dotnet --version` — must report `10.x`. |
| SQL Server Express LocalDB | Installed automatically with Visual Studio 2022 (any workload including **ASP.NET and web development**), or standalone via the [SQL Server Express installer](https://go.microsoft.com/fwlink/?linkid=866658). |

To confirm LocalDB is available, run:

```powershell
sqllocaldb info
```

You should see at least `MSSQLLocalDB`. If the command is not recognised, install LocalDB (see above).

## Running the app

```powershell
cd CafeDash
dotnet run
```

Then open the URL printed in the console (usually <https://localhost:7236>).

In Visual Studio, just open `CafeDash.slnx` and press **F5**.

The first run creates the database automatically. No manual SQL setup is required.

### What happens on first start

1. EF Core migrations create the schema.
2. `Data/DatabaseSeeder.cs` creates a table the migration does not cover, and — only when the
   database is empty — loads the demo data (9 restaurants, 100 menu items).

Seeding is skipped whenever data is already present, so restarting the app will not duplicate
anything.

## Signing in

| Role | Credentials |
| --- | --- |
| Admin | `admin` / `admin123` |

Admin login is at `/Admin/Login`.

The app runs in **Development** mode when started with `dotnet run` or F5. In that mode the
reCAPTCHA check is bypassed and new accounts are auto-verified, so you can register and log in
without configuring mail.

## Optional: payments and email

`appsettings.json` is committed with the database connection string but **no credentials**, so
Stripe checkout and outbound email are disabled out of the box. Everything else works.

To enable them, create `CafeDash/appsettings.Development.json` (this file is gitignored, so your
keys stay off the repository) and fill in your own values:

```json
{
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_..."
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "you@example.com",
    "Password": "your-app-password",
    "FromEmail": "you@example.com",
    "FromName": "CafeDash Support"
  }
}
```

`appsettings.Development.json` is layered on top of `appsettings.json`, so you only need to list
the values you are overriding.

## Project layout

```
CafeDash/
  Controllers/    MVC controllers (Account, Admin, Cart, Checkout, Home)
  Data/           DbContext, seeder, and the SQL seed scripts
  Migrations/     EF Core migrations
  Models/         Entity and view models
  Views/          Razor views
  wwwroot/        CSS, JS, and restaurant/menu images
```

## Troubleshooting

**"Cannot open database" / database connection errors**
Ensure LocalDB is installed (`sqllocaldb info`) and that `DefaultConnection` in
`CafeDash/appsettings.json` points at your instance.

**Restaurants list is empty**
The seed only runs against an empty database. Drop just this app's database and restart:

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -Q "ALTER DATABASE cafedash_db SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE cafedash_db;"
```

Do not delete the whole `MSSQLLocalDB` instance — that would remove every other LocalDB
database on the machine, including work for other projects.

**Chat page errors**
The `Chats` table is created by `Data/seed_schema.sql` on startup. If you see "Invalid object
name 'Chats'", drop the database as above and restart.
