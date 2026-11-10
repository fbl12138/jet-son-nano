using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ObjectDetection.Models;
using System.Data;

namespace ObjectDetection.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users => Set<User>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Detection> Detections => Set<Detection>();
        public DbSet<Violation> Violations => Set<Violation>();
        public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();
        public DbSet<DailyStat> DailyStats => Set<DailyStat>();
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasIndex(u => u.Username).IsUnique();
            b.Entity<Device>().HasIndex(d => d.DeviceCode).IsUnique();
            b.Entity<ComplianceRule>().HasIndex(r => r.DeviceId).IsUnique();
            // 字段长度（限制 NVARCHAR 长度，避免默认 nvarchar(max)）
            b.Entity<User>().Property(u => u.Username).HasMaxLength(50);
            b.Entity<User>().Property(u => u.PasswordHash).HasMaxLength(200);
            b.Entity<User>().Property(u => u.Role).HasMaxLength(20);
            b.Entity<Device>().Property(d => d.DeviceCode).HasMaxLength(50);
            b.Entity<Device>().Property(d => d.Name).HasMaxLength(100);
            b.Entity<Device>().Property(d => d.Location).HasMaxLength(200);
            b.Entity<Device>().Property(d => d.ApiKey).HasMaxLength(100);
            b.Entity<Device>().Property(d => d.Status).HasMaxLength(20);
            b.Entity<Detection>().Property(d => d.LabelCode).HasMaxLength(50);
            b.Entity<Detection>().Property(d => d.TrackId).HasMaxLength(50);
            b.Entity<Detection>().Property(d => d.ImagePath).HasMaxLength(300);
            b.Entity<Detection>().Property(d => d.Confidence).HasPrecision(5, 4);
            b.Entity<Violation>().Property(v => v.ViolationTypes).HasMaxLength(100);
            b.Entity<Violation>().Property(v => v.TrackId).HasMaxLength(50);
            b.Entity<Violation>().Property(v => v.ImagePath).HasMaxLength(300);
            b.Entity<Violation>().Property(v => v.Status).HasMaxLength(20);
            b.Entity<Violation>().Property(v => v.Remark).HasMaxLength(300);
            b.Entity<Violation>().Property(v => v.Confidence).HasPrecision(5, 4);
            b.Entity<ComplianceRule>().Property(r => r.MinConfidence).HasPrecision(5, 4);
            // 查询索引（按设备/类别查时间范围）
            b.Entity<Detection>().HasIndex(d => new { d.DeviceId, d.DetectedAt });
            b.Entity<Detection>().HasIndex(d => new { d.LabelCode, d.DetectedAt });
            b.Entity<Violation>().HasIndex(v => new { v.Status, v.DetectedAt });
            b.Entity<Violation>().HasIndex(v => new { v.DeviceId, v.DetectedAt });
            b.Entity<DailyStat>().HasIndex(s => new { s.DeviceId, s.StatDate }).IsUnique();
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            foreach (var entityType in b.Model.GetEntityTypes())
            {
                foreach (var prop in entityType.GetProperties())
                {
                    if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utcConverter);
                    else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullableConverter);
                }
            }
        }
    }
}
