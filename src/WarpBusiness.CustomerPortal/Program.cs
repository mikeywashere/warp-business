using WarpBusiness.CustomerPortal.Components;
using WarpBusiness.CustomerPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Typed HttpClient for WarpBusiness.Api — same Aspire service discovery as the main web app
builder.Services.AddHttpClient<CustomerApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

builder.Services.AddSingleton<CustomerAuthState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

await app.RunAsync();
