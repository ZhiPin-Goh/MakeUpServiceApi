using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace MakeUpServiceApi.Models
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<TokenActivity> TokenActivities { get; set; }
        public DbSet<ServiceArea> ServiceAreas { get; set; }
        public DbSet<Idempotency> Idempotencies { get; set; }
        public DbSet<ScheduleBlocker> ScheduleBlockers { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Banner> Banners { get; set; }
        // AuditLog: Override SaveChanges to log changes to the database
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            int? currentAdminID = null;
            var user = _httpContextAccessor?.HttpContext?.User;

            if(user != null && user.Identity != null && user.Identity.IsAuthenticated)
            {
                var adminIDClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("AdminID");
                if (adminIDClaim != null && int.TryParse(adminIDClaim.Value, out int parsedID))
                {
                    currentAdminID = parsedID;
                }
            }
            var auditEntries = new List<AuditLog>();
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is not AuditLog &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
                .ToList();

            foreach (var entry in entries)
            {
                var auditLog = new AuditLog
                {
                    TableName = entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    Timestamp = DateTime.Now,
                    AdminID = currentAdminID 
                };

                var keyProps = entry.Properties.Where(p => p.Metadata.IsPrimaryKey());
                auditLog.KeyValues = JsonSerializer.Serialize(keyProps.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));

                if (entry.State == EntityState.Modified)
                {
                    var modifiedProps = entry.Properties.Where(p => p.IsModified);
                    auditLog.OldValues = JsonSerializer.Serialize(modifiedProps.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    auditLog.NewValues = JsonSerializer.Serialize(modifiedProps.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                }
                else if (entry.State == EntityState.Added)
                {
                    auditLog.NewValues = JsonSerializer.Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                }
                else if (entry.State == EntityState.Deleted)
                {
                    auditLog.OldValues = JsonSerializer.Serialize(entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                }

                auditEntries.Add(auditLog);
            }
            if (auditEntries.Any())
            {
                AuditLogs.AddRange(auditEntries);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
