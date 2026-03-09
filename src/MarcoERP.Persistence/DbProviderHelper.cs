using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarcoERP.Persistence
{
    /// <summary>
    /// Helper for dual-provider (SQL Server / PostgreSQL) EF Core configuration.
    /// Encapsulates provider-specific SQL syntax differences.
    /// </summary>
    public static class DbProviderHelper
    {
        // Thread-local provider flag (set once during OnModelCreating)
        private static bool _isPostgreSql;

        /// <summary>Whether the current provider is PostgreSQL.</summary>
        public static bool IsPostgreSql => _isPostgreSql;

        /// <summary>Call from MarcoDbContext.OnModelCreating to detect provider.</summary>
        public static void DetectProvider(DbContext context)
        {
            _isPostgreSql = context.Database.ProviderName?.Contains("Npgsql") == true;
        }

        // ── Filter Expressions ──────────────────────────────────────

        /// <summary>
        /// Returns a provider-appropriate SQL filter for soft-delete indexes.
        /// SQL Server: [IsDeleted] = 0
        /// PostgreSQL: "IsDeleted" = false
        /// </summary>
        public static string SoftDeleteFilter()
            => _isPostgreSql ? "\"IsDeleted\" = false" : "[IsDeleted] = 0";

        /// <summary>
        /// Returns a provider-appropriate SQL NOT NULL filter for nullable FK indexes.
        /// SQL Server: [ColumnName] IS NOT NULL
        /// PostgreSQL: "ColumnName" IS NOT NULL
        /// </summary>
        public static string IsNotNullFilter(string columnName)
            => _isPostgreSql ? $"\"{columnName}\" IS NOT NULL" : $"[{columnName}] IS NOT NULL";

        /// <summary>
        /// Returns a provider-appropriate compound filter: NOT NULL + SoftDelete.
        /// SQL Server: [Column] IS NOT NULL AND [IsDeleted] = 0
        /// PostgreSQL: "Column" IS NOT NULL AND "IsDeleted" = false
        /// </summary>
        public static string NotNullAndSoftDeleteFilter(string columnName)
            => _isPostgreSql
                ? $"\"{columnName}\" IS NOT NULL AND \"IsDeleted\" = false"
                : $"[{columnName}] IS NOT NULL AND [IsDeleted] = 0";

        // ── Check Constraints ───────────────────────────────────────

        /// <summary>
        /// Quotes a column name for use in check constraints or filters.
        /// SQL Server: [ColumnName]
        /// PostgreSQL: "ColumnName"
        /// </summary>
        public static string QuoteColumn(string columnName)
            => _isPostgreSql ? $"\"{columnName}\"" : $"[{columnName}]";

        /// <summary>
        /// Builds a check constraint expression, quoting all column references.
        /// Pass the expression with {0}, {1} etc. placeholders for column names.
        /// Example: CheckExpr("{0} >= 0 AND {1} >= 0", "DebitAmount", "CreditAmount")
        /// </summary>
        public static string CheckExpr(string template, params string[] columns)
        {
            var quoted = new object[columns.Length];
            for (int i = 0; i < columns.Length; i++)
                quoted[i] = QuoteColumn(columns[i]);
            return string.Format(template, quoted);
        }

        // ── Row Version / Concurrency ───────────────────────────────

        /// <summary>
        /// Configures concurrency token for the RowVersion property.
        /// SQL Server: IsRowVersion() + IsConcurrencyToken() (uses timestamp/rowversion type).
        /// PostgreSQL: UseXminAsConcurrencyToken() on the entity (uses xmin system column).
        /// </summary>
        public static void ConfigureRowVersion<TEntity>(EntityTypeBuilder<TEntity> builder)
            where TEntity : class
        {
            if (_isPostgreSql)
            {
#pragma warning disable CS0618 // UseXminAsConcurrencyToken is obsolete but still the safest approach for byte[] RowVersion properties
                builder.UseXminAsConcurrencyToken();
#pragma warning restore CS0618
                // Ignore the RowVersion property since PostgreSQL uses xmin
                builder.Ignore("RowVersion");
            }
            else
            {
                builder.Property("RowVersion")
                    .IsRowVersion()
                    .IsConcurrencyToken();
            }
        }

        // ── Column Type Mapping ──────────────────────────────────────

        /// <summary>
        /// Returns the provider-appropriate unlimited-length string type.
        /// SQL Server: nvarchar(max)
        /// PostgreSQL: text
        /// </summary>
        public static string MaxStringType()
            => _isPostgreSql ? "text" : "nvarchar(max)";

        // ── Delete Table SQL ────────────────────────────────────────

        /// <summary>
        /// Returns a DELETE FROM statement for the given table name.
        /// SQL Server: DELETE FROM [dbo].[TableName]
        /// PostgreSQL: DELETE FROM "TableName"
        /// </summary>
        public static string DeleteFromTable(string tableName)
        {
            // Whitelist: only alphanumeric and underscore allowed in table names
            if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException($"Invalid table name: {tableName}");
            return _isPostgreSql ? $"DELETE FROM \"{tableName}\"" : $"DELETE FROM [dbo].[{tableName}]";
        }
    }
}
