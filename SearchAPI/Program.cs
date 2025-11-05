using NLog.Web;

namespace SearchAPI;

public class Program
{
    public static void Main(string[] args)
    {
    // Setup NLog first
    var logger = NLog.LogManager.GetCurrentClassLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            // Replace default logging providers with NLog
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.Host.UseNLog(); // NLog: setup NLog as the DI logging provider

            // Add services to the container.
            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Log database configuration at startup for easier troubleshooting
            try
            {
                logger.Info("[SearchAPI] Using SQLite DB: {db}", Core.Paths.SQLITE_DATABASE);
                logger.Info("[SearchAPI] Shard DBs: {shards}", string.Join(", ", Core.Paths.SQLITE_SHARD_DATABASES));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to log DB paths");
            }

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
        catch (Exception ex)
        {
            // NLog: catch setup errors
            NLog.LogManager.GetCurrentClassLogger().Error(ex, "Application stopped because of exception");
            throw;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}