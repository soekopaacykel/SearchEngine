var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure HttpClient with default timeout
builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Add search service
builder.Services.AddScoped<SearchWeb.Services.SearchService>();

// Add configuration for search API
builder.Services.AddOptions();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Check API health on startup
using (var scope = app.Services.CreateScope())
{
    var searchService = scope.ServiceProvider.GetRequiredService<SearchWeb.Services.SearchService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var isApiAvailable = await searchService.CheckApiHealthAsync();
        if (!isApiAvailable)
        {
            logger.LogWarning("Search API is not available. The application will start in offline mode.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while checking API health.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
