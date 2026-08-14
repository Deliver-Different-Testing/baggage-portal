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

// Boots the real API pipeline (auth, antiforgery, MVC filters) against a SQLite
// in-memory Despatch schema. Mirrors UnitTests/Helpers/InMemoryDb.cs: the same
// getdate/getutcdate stubs and filtered-index stripping, and the same NoTracking
// behaviour production uses — see the EF Core section in CLAUDE.md.
public sealed class PaxApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    // Program.cs reads configuration during service registration (AddInfrastructure,
    // AddAppAuthentication), which runs before WebApplicationFactory's
    // ConfigureAppConfiguration delegates are applied under minimal hosting. Environment
    // variables are the only source available that early.
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

            // Keeps the antiforgery key ring off the filesystem and out of AWS SSM.
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

            // UTL_IsBusinessDay / UTL_AddBusinessDays are SQL Server scalar UDFs
            // with no SQLite equivalent — the interface exists for exactly this.
            services.RemoveAll<IDespatchCalendar>();
            services.AddSingleton<IDespatchCalendar, WeekdayCalendar>();
        });
    }

    // Weekdays are business days; holidays are the real calendar's job, not this
    // fixture's.
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

        private static bool IsWeekday(DateTime date) =>
            date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    // Owns the scope so the context can't outlive it. EnsureCreated is idempotent —
    // the SQLite connection is held open for the factory's lifetime, so the schema
    // survives between calls.
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

    // AddDbContextPool registers several closed generics over the context type
    // (options, pool, scoped lease). Strip every one before swapping in SQLite.
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

    // JobDeliveryJourney carries SQL Server filtered indexes whose predicates use
    // bracket quoting SQLite can't parse, so EnsureCreated would fail. The covering
    // behaviour isn't what these tests exercise.
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
