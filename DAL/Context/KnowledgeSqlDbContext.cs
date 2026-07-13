using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Entities.Billing;

namespace PRN222_FINAL.DAL.Context;

public sealed class KnowledgeSqlDbContext : DbContext
{
    public KnowledgeSqlDbContext(DbContextOptions<KnowledgeSqlDbContext> options) : base(options)
    {
    }

    public DbSet<KnowledgeSqlDocument> Documents => Set<KnowledgeSqlDocument>();
    public DbSet<KnowledgeSqlChunk> Chunks => Set<KnowledgeSqlChunk>();
    public DbSet<KnowledgeSqlCourseSubject> CourseSubjects => Set<KnowledgeSqlCourseSubject>();
    public DbSet<KnowledgeSqlCourseChapter> CourseChapters => Set<KnowledgeSqlCourseChapter>();
    public DbSet<KnowledgeSqlChatSession> Sessions => Set<KnowledgeSqlChatSession>();
    public DbSet<KnowledgeSqlChatMessage> Messages => Set<KnowledgeSqlChatMessage>();
    public DbSet<KnowledgeSqlCitation> Citations => Set<KnowledgeSqlCitation>();
    public DbSet<KnowledgeSqlSubjectLecturer> SubjectLecturers => Set<KnowledgeSqlSubjectLecturer>();
    public DbSet<KnowledgeSqlSubjectStudent> SubjectStudents => Set<KnowledgeSqlSubjectStudent>();
    public DbSet<KnowledgeSqlCourseAccessLog> CourseAccessLogs => Set<KnowledgeSqlCourseAccessLog>();
    public DbSet<KnowledgeSqlPackage> Packages => Set<KnowledgeSqlPackage>();
    public DbSet<KnowledgeSqlPayment> Payments => Set<KnowledgeSqlPayment>();
    public DbSet<KnowledgeSqlSubscription> Subscriptions => Set<KnowledgeSqlSubscription>();
    public DbSet<KnowledgeSqlPackagePriceChange> PackagePriceChanges => Set<KnowledgeSqlPackagePriceChange>();
    public DbSet<KnowledgeSqlSystemNotification> SystemNotifications => Set<KnowledgeSqlSystemNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KnowledgeSqlDocument>(entity =>
        {
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.Subject);
            entity.HasIndex(d => d.UploadedAt);
        });

        modelBuilder.Entity<KnowledgeSqlChunk>(entity =>
        {
            entity.HasIndex(c => c.DocumentId);
            entity.HasIndex(c => c.Subject);

            entity.HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlCourseSubject>(entity =>
        {
            entity.HasIndex(s => s.Code).IsUnique();

            entity.HasMany(s => s.Chapters)
                .WithOne(c => c.Subject)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.TeachingLecturers)
                .WithOne(tl => tl.Subject)
                .HasForeignKey(tl => tl.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlSubjectLecturer>(entity =>
        {
            entity.HasIndex(tl => new { tl.SubjectId, tl.UserId }).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSqlSubjectStudent>(entity =>
        {
            entity.HasIndex(ss => new { ss.SubjectId, ss.UserId }).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSqlCourseChapter>(entity =>
        {
            entity.HasIndex(c => new { c.SubjectId, c.Title }).IsUnique();
        });

        modelBuilder.Entity<KnowledgeSqlChatSession>(entity =>
        {
            entity.HasIndex(s => s.OwnerUserId);
            entity.HasIndex(s => s.UpdatedAt);

            entity.HasMany(s => s.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlChatMessage>(entity =>
        {
            entity.HasMany(m => m.Citations)
                .WithOne(c => c.Message)
                .HasForeignKey(c => c.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeSqlCitation>(entity =>
        {
            entity.HasIndex(c => c.DocumentId);
            entity.HasIndex(c => c.Subject);
        });

        modelBuilder.Entity<KnowledgeSqlCourseAccessLog>(entity =>
        {
            entity.HasIndex(log => log.UserId);
            entity.HasIndex(log => log.SubjectId);
            entity.HasIndex(log => log.AccessedAt);
        });

        modelBuilder.Entity<KnowledgeSqlPackage>(entity =>
        {
            entity.Property(package => package.PriceVnd).HasColumnType("numeric(18,2)");
            entity.HasIndex(package => package.Code).IsUnique();
            entity.HasIndex(package => package.IsActive);
            entity.HasIndex(package => package.SortOrder);
        });

        modelBuilder.Entity<KnowledgeSqlPackagePriceChange>(entity =>
        {
            entity.Property(change => change.OldPriceVnd).HasColumnType("numeric(18,2)");
            entity.Property(change => change.NewPriceVnd).HasColumnType("numeric(18,2)");
            entity.HasIndex(change => change.ChangedAt);
            entity.HasIndex(change => change.PackageId);
        });

        modelBuilder.Entity<KnowledgeSqlSystemNotification>(entity =>
        {
            entity.HasIndex(notification => notification.OccurredAt);
            entity.HasIndex(notification => notification.Type);
        });

        modelBuilder.Entity<KnowledgeSqlPayment>(entity =>
        {
            entity.Property(payment => payment.Provider).HasConversion<string>().HasMaxLength(32);
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(payment => payment.AmountVnd).HasColumnType("numeric(18,2)");
            entity.HasIndex(payment => new { payment.Provider, payment.OrderCode }).IsUnique();
            entity.HasIndex(payment => payment.UserId);
            entity.HasIndex(payment => payment.Status);

            entity.HasOne(payment => payment.Package)
                .WithMany()
                .HasForeignKey(payment => payment.PackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeSqlSubscription>(entity =>
        {
            entity.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(subscription => new { subscription.UserId, subscription.Status, subscription.EndsAt });
            entity.HasIndex(subscription => subscription.PaymentId).IsUnique();

            entity.HasOne(subscription => subscription.Package)
                .WithMany()
                .HasForeignKey(subscription => subscription.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(subscription => subscription.Payment)
                .WithMany()
                .HasForeignKey(subscription => subscription.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
