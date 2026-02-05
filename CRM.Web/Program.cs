using CRM.Core.Storage;
using CRM.Email.Abstractions;
using CRM.Email.Persistence;
using CRM.Email.Providers.Microsoft;
using CRM.Email.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("CrmDatabase")
    ?? "Server=localhost;Database=CrmEmailDemo;Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
builder.Services.AddSingleton<SqlSchemaInitializer>();

builder.Services.AddSingleton<IAccountStore, SqlAccountStore>();
builder.Services.AddSingleton<IContactStore, SqlContactStore>();
builder.Services.AddSingleton<IEmailThreadStore, SqlEmailThreadStore>();
builder.Services.AddSingleton<IEmailThreadQuery, SqlEmailThreadStore>();

builder.Services.AddSingleton(sp =>
{
    var options = new EmailStorageOptions();
    builder.Configuration.GetSection("Email:Storage").Bind(options);
    return options;
});

builder.Services.AddSingleton(sp =>
{
    var options = new EmailProviderOptions();
    builder.Configuration.GetSection("Email:Provider").Bind(options);
    return options;
});

builder.Services.AddHttpClient<MicrosoftOAuthTokenProvider>();

builder.Services.AddSingleton<IAccessTokenProvider>(sp =>
{
    var providerKey = builder.Configuration["Email:ProviderType"] ?? "Local";
    return providerKey.Equals("MicrosoftMailKit", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<MicrosoftOAuthTokenProvider>()
        : new DemoAccessTokenProvider();
});

builder.Services.AddSingleton<LocalEmailProvider>();

builder.Services.AddSingleton<IEmailProvider>(sp =>
{
    var providerKey = builder.Configuration["Email:ProviderType"] ?? "Local";
    return providerKey.Equals("MicrosoftMailKit", StringComparison.OrdinalIgnoreCase)
        ? new MicrosoftMailKitClient(
            sp.GetRequiredService<EmailProviderOptions>(),
            sp.GetRequiredService<IAccessTokenProvider>())
        : sp.GetRequiredService<LocalEmailProvider>();
});

builder.Services.AddSingleton<IEmailService>(sp =>
{
    var providerKey = builder.Configuration["Email:ProviderType"] ?? "Local";
    return providerKey.Equals("MicrosoftMailKit", StringComparison.OrdinalIgnoreCase)
        ? (IEmailService)new MicrosoftMailKitClient(
            sp.GetRequiredService<EmailProviderOptions>(),
            sp.GetRequiredService<IAccessTokenProvider>())
        : sp.GetRequiredService<LocalEmailProvider>();
});

builder.Services.AddSingleton<EmailSyncService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<SqlSchemaInitializer>();
    await initializer.EnsureCreatedAsync(CancellationToken.None);
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Accounts}/{action=Index}/{id?}");

app.Run();

internal sealed class DemoAccessTokenProvider : IAccessTokenProvider
{
    public Task<string> GetAccessTokenAsync(string scope, CancellationToken cancellationToken)
    {
        return Task.FromResult("demo-token");
    }
}
