using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace DAL.Data
{
    /// <summary>
    /// EF Core interceptor that automatically populates audit fields (updated_at, updated_by)
    /// on every SaveChanges call, using the currently logged-in user from HttpContext.
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

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

            // Resolve the current user's ID from the cookie claim
            var userIdStr = _httpContextAccessor.HttpContext?
                .User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? currentUserId = Guid.TryParse(userIdStr, out var uid) ? uid : null;

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

                    // Automatically track who made the last update
                    var updatedByProp = entityType.GetProperty("UpdatedBy");
                    if (updatedByProp != null && updatedByProp.CanWrite && currentUserId.HasValue)
                        updatedByProp.SetValue(entry.Entity, currentUserId);
                }
            }
        }
    }
}
