using Microsoft.EntityFrameworkCore;
using MyAIMachine.Models;
namespace MyAIMachine.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<TrainingExample> TrainingExamples => Set<TrainingExample>();
    public DbSet<ModelSettings> ModelSettings => Set<ModelSettings>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<AppLog> AppLogs => Set<AppLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Conversation>().Property(x => x.OwnerId).HasMaxLength(64);
        b.Entity<Conversation>().Property(x => x.Title).HasMaxLength(100);
        b.Entity<Conversation>().HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        b.Entity<Conversation>().HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ChatMessage>().Property(x => x.Role).HasMaxLength(20);
        b.Entity<ChatMessage>().Property(x => x.Content).HasMaxLength(8000);
        b.Entity<TrainingExample>().Property(x => x.Input).HasMaxLength(4000);
        b.Entity<TrainingExample>().Property(x => x.Response).HasMaxLength(8000);
        b.Entity<ModelSettings>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ModelSettings>().Property(x => x.FallbackResponse).HasMaxLength(2000);
        b.Entity<ModelSettings>().HasData(new ModelSettings());
        b.Entity<AppLog>().Property(x => x.Event).HasMaxLength(100);
        b.Entity<AppLog>().Property(x => x.Level).HasMaxLength(20);
        b.Entity<AppLog>().Property(x => x.Detail).HasMaxLength(2000);
    }
}
