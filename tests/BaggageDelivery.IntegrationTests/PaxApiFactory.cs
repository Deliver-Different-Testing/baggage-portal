using System.Security.Cryptography;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BaggageDelivery.IntegrationTests;

public sealed class PaxApiFactory : WebApplicationFactory<Program>
{
    private const string SupportPhone = "09 3073555";

    private readonly SqliteConnection _connection;

    static PaxApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Server=(unused);Database=(unused);");
        Environment.SetEnvironmentVariable("JWTSecretKey", new string('k', 64));
        Environment.SetEnvironmentVariable("BaggageDeliveryEncryptionKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable("BaggageDeliveryEncryptionIV",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));
        Environment.SetEnvironmentVariable("TimeZone", "Pacific/Auckland");
        Environment.SetEnvironmentVariable("SupportPhone", SupportPhone);
    }

    public PaxApiFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _connection.CreateFunction("getdate", () => DateTime.UtcNow);
        _connection.CreateFunction("getutcdate", () => DateTime.UtcNow);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<BaggageDeliveryContext>(opts => opts
                .UseSqlite(_connection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());

            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

            services.RemoveAll<IDespatchCalendar>();
            services.AddSingleton<IDespatchCalendar, WeekdayCalendar>();
        });
    }

    private sealed class WeekdayCalendar : IDespatchCalendar
    {
        public Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct) =>
            Task.FromResult(IsWeekday(localDate.Date));

        public Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
            CancellationToken ct)
        {
            var date = localDate.Date;
            for (var i = 0; i < days; i++)
            {
                do
                {
                    date = date.AddDays(1);
                } while (!IsWeekday(date));
            }

            return Task.FromResult(date);
        }

        public async Task<IReadOnlyList<DateTime>> NextBusinessDaysAsync(int count,
            DateTime localDate, int clientId, CancellationToken ct)
        {
            var days = new List<DateTime>(count);
            var date = localDate.Date;
            for (var i = 0; i < count; i++)
            {
                date = await AddBusinessDaysAsync(1, date, clientId, ct);
                days.Add(date);
            }

            return days;
        }

        private static bool IsWeekday(DateTime date) =>
            date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    public async Task SeedAsync(Func<BaggageDeliveryContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaggageDeliveryContext>();
        await db.Database.EnsureCreatedAsync();
        await seed(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var doomed = services
            .Where(d => d.ServiceType == typeof(BaggageDeliveryContext)
                        || d.ServiceType == typeof(DbContextOptions)
                        || (d.ServiceType.IsGenericType
                            && d.ServiceType.GetGenericArguments().Contains(typeof(BaggageDeliveryContext))))
            .ToList();

        foreach (var descriptor in doomed)
        {
            services.Remove(descriptor);
        }
    }

    private sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies)
        : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var index in entityType.GetIndexes().Where(i => i.GetFilter() is not null).ToList())
                {
                    entityType.RemoveIndex(index.Properties);
                }
            }
        }
    }
}
