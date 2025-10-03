using System.Net.Http.Headers;

// Program.cs for Loadbalencer
// ----------------------------
// This sets up a minimal ASP.NET Core app that uses controller-based routing.
// The LoadBalancerController does all the proxying; this file just wires up services and endpoints.

var builder = WebApplication.CreateBuilder(args);

// Register MVC controllers and HttpClient for outgoing calls to backends
builder.Services.AddControllers();
builder.Services.AddHttpClient();

var app = builder.Build();

// Enable endpoint routing and map controller routes (our catch-all controller route handles everything)
app.UseRouting();
app.MapControllers();

// Friendly startup logs so you know what's running
Console.WriteLine("Load Balancer starting...");
Console.WriteLine("Backend servers:");
Console.WriteLine("  - http://localhost:5154");
Console.WriteLine("  - http://localhost:5155");
Console.WriteLine("Load Balancer running on http://localhost:5000");
Console.WriteLine("Press Ctrl+C to stop the load balancer");

// Bind the app to a specific URL/port so you can call it directly
app.Run("http://localhost:5000");