using System;
using MarcoERP.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarcoERP.Persistence
{
    /// <summary>
    /// Design-time factory for MarcoDbContext.
    /// Used by EF Core CLI tools (dotnet ef migrations) when no DI container is available.
    /// Set environment variable DB_PROVIDER=PostgreSQL to target PostgreSQL.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MarcoDbContext>
    {
        public MarcoDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MarcoDbContext>();
            var provider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "SqlServer";

            if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var pgConn = Environment.GetEnvironmentVariable("DB_CONNECTION")
                    ?? throw new InvalidOperationException(
                        "Set DB_CONNECTION environment variable for PostgreSQL (e.g. Host=localhost;Database=MarcoERP;Username=postgres;Password=YOUR_PASSWORD)");
                optionsBuilder.UseNpgsql(pgConn);
            }
            else
            {
                var sqlConn = Environment.GetEnvironmentVariable("DB_CONNECTION")
                    ?? "Server=.\\SQL2022;Database=MarcoERP;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";
                optionsBuilder.UseSqlServer(sqlConn);
            }

            return new MarcoDbContext(optionsBuilder.Options, new DesignTimeCompanyContext());
        }

        /// <summary>Design-time stub — always returns CompanyId = 1.</summary>
        private sealed class DesignTimeCompanyContext : ICompanyContext
        {
            public int CurrentCompanyId => 1;
        }
    }
}
