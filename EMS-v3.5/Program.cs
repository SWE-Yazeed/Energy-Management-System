using EMS_v2._2.Data;
using EMS_v2._2.Models;
using EMS_v2._2.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register ApplicationDbContext with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Register PredictionService as a singleton
builder.Services.AddSingleton<PredictionService>();

// Configure Identity options
builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false; // Disable requirement for non-alphanumeric characters in passwords
    options.Password.RequiredLength = 4; // Minimum password length
    options.Password.RequireUppercase = false; // Disable requirement for uppercase characters
    options.Password.RequireLowercase = false; // Disable requirement for lowercase characters
    options.User.RequireUniqueEmail = true; // Ensure unique email for each user
    options.SignIn.RequireConfirmedAccount = false; // Disable account confirmation requirement
    options.SignIn.RequireConfirmedEmail = false; // Disable email confirmation requirement
    options.SignIn.RequireConfirmedPhoneNumber = false; // Disable phone number confirmation requirement
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings for sessions
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; // Path to the login page
    options.LogoutPath = "/Account/Logout"; // Path to the logout page
    options.AccessDeniedPath = "/Account/AccessDenied"; // Path to the access denied page
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // Expiration time for "Remember Me" sessions
    options.SlidingExpiration = true; // Automatically extend the session expiration on user activity
    options.Cookie.HttpOnly = true; // Make cookies HTTP-only for security
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Ensure cookies are sent over HTTPS only
    options.Cookie.IsEssential = true; // Mark cookies as essential to always be sent
});

// Add support for controllers with views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // Use error page for production
    app.UseHsts(); // Enable HTTP Strict Transport Security
}

app.UseHttpsRedirection(); // Redirect HTTP requests to HTTPS
app.UseStaticFiles(); // Serve static files

app.UseRouting(); // Enable routing middleware

// Enable authentication and authorization
app.UseAuthentication(); // Middleware to handle user authentication
app.UseAuthorization(); // Middleware to enforce access control

// Configure default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run(); // Run the application
