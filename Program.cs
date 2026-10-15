using SouthernTravels.Areas.Finance.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddRazorOptions(o =>
    {
        // Layout, pop-up frame, report grid and document parts shared by every area live in Core/Views/Shared.
        o.ViewLocationFormats.Add("/Core/Views/Shared/{0}.cshtml");
        o.AreaViewLocationFormats.Add("/Core/Views/Shared/{0}.cshtml");
    });


// In-memory demo store for the Finance area: seeded once, mutated by the popups (raise bill, submit, pay).
builder.Services.AddSingleton<FinanceStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Finance/VendorPayable");
}

app.UseStaticFiles();
app.UseRouting();

// Every controller routes itself by attribute: /Finance/<Report>, /Finance/<Report>/Popup, /Session/...
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/Finance/VendorPayable"));

app.Run();
