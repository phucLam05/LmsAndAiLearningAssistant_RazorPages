using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DAL.Data
{
    /// <summary>
    /// EF Core interceptor that automatically populates audit timestamps.
    /// The business layer is responsible for setting UpdatedBy from the actor ID
    /// supplied by the presentation layer.
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            UpdateAuditFields(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            UpdateAuditFields(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void UpdateAuditFields(DbContext? context)
        {
            if (context == null) return;

            var now = DateTime.UtcNow;

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var entityType = entry.Entity.GetType();

                if (entry.State == EntityState.Added)
                {
                    var createdAtProp = entityType.GetProperty("CreatedAt");
                    if (createdAtProp != null && createdAtProp.CanWrite)
                        createdAtProp.SetValue(entry.Entity, now);
                }

                if (entry.State == EntityState.Modified)
                {
                    var updatedAtProp = entityType.GetProperty("UpdatedAt");
                    if (updatedAtProp != null && updatedAtProp.CanWrite)
                        updatedAtProp.SetValue(entry.Entity, now);

                }
            }
        }
    }
}
