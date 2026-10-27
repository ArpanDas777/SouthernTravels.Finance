using SouthernTravels.Areas.Finance.Data;
using SouthernTravels.Areas.Finance.Interfaces;
using SouthernTravels.Areas.Finance.Services;
using System.Threading.RateLimiting;
using SouthernTravels.Core.Common;
using SouthernTravels.Core.Helpers;
using SouthernTravels.Core.Interfaces;
using SouthernTravels.Core.Security;
using SouthernTravels.Core.Services;

var builder = WebApplication.CreateBuilder(args);

/* =======================
   CORE SERVICES
======================= */
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews()
    .AddRazorOptions(o =>
    {
        // Layout, pop-up frame, report grid and document parts shared by every area live in Core/Views/Shared.
        o.ViewLocationFormats.Add("/Core/Views/Shared/{0}.cshtml");
        o.AreaViewLocationFormats.Add("/Core/Views/Shared/{0}.cshtml");
    });

/* =======================
   SESSION (signed-in user and API token, read by ISessionSecurityService)
======================= */
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = "STFinanceSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // SameAsRequest so the session also works on http://localhost:5180; use Always once the site is HTTPS only.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

/* =======================
   API CLIENT (same flow as STPLCostSheet: Controller -> Service -> ApiClientHelper -> API)
======================= */
builder.Services.AddHttpClient<IApiClientHelper, ApiClientHelper>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("FinanceAPI:TimeoutSeconds", 100));
});
builder.Services.AddScoped<ISessionSecurityService, SessionSecurityService>();

/* =======================
   LOGIN (OTP, same Account API as STPLCostSheet)
======================= */
builder.Services.AddScoped<IAccountService, AccountService>();

// Anti brute force on Send OTP / Validate OTP: 5 tries per minute per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("loginLimit", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // The OTP form is a normal POST: send it back to the login page with a message instead of a blank 429.
    options.OnRejected = (ctx, _) =>
    {
        if (string.IsNullOrEmpty(ctx.HttpContext.Request.Headers.XRequestedWith))
            ctx.HttpContext.Response.Redirect("/Account/Login?limited=true");
        return ValueTask.CompletedTask;
    };
});

/* =======================
   FINANCE AREA
   FinanceSettings:UseApi = true  -> FinanceApiService (backend Finance API)
   FinanceSettings:UseApi = false -> FinanceDemoService (in-memory sample data, until the API exists)
======================= */
if (builder.Configuration.GetValue<bool>("FinanceSettings:UseApi"))
{
    builder.Services.AddScoped<IFinanceService, FinanceApiService>();
}
else
{
    // In-memory demo store: seeded once, mutated by the popups (raise bill, submit, pay).
    builder.Services.AddSingleton<FinanceStore>();
    builder.Services.AddScoped<IFinanceService, FinanceDemoService>();
}

var app = builder.Build();

/* =======================
   MIDDLEWARE PIPELINE
======================= */
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Finance/VendorPayable");
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseSession();
// Login is required: anyone without a session is sent to /Account/Login.
app.UseMiddleware<SessionMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

// Every controller routes itself by attribute: /Finance/<Report>, /Finance/<Report>/Popup, /Session/...
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/Finance/VendorPayable"));

app.Run();
 