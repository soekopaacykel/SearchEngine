namespace SearchAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Log database configuration at startup for easier troubleshooting
        try
        {
            Console.WriteLine($"[SearchAPI] Using SQLite DB: {Core.Paths.SQLITE_DATABASE}");
            Console.WriteLine($"[SearchAPI] Shard DBs: {string.Join(", ", Core.Paths.SQLITE_SHARD_DATABASES)}");
        }
        catch { }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // Do not force HTTPS in containerized env without certificates
        // app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}