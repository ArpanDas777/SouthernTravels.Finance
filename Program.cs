using SouthernTravels.Finance.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
// In-memory demo store: seeded once, mutated by the popups (raise bill, submit, pay).
builder.Services.AddSingleton<FinanceStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Finance/VendorPayable");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Finance}/{action=VendorPayable}/{id?}");

app.Run();
