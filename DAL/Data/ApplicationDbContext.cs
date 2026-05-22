using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data
{
    /// <summary>
    /// The main application database context.
    /// Manages the entity objects during runtime, which includes fetching from and saving to the database.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the Users DbSet.
        /// Represents the Users table in the database.
        /// </summary>
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.EmailEncrypt)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.EmailHash)
                    .IsRequired()
                    .HasMaxLength(255);

                // Add an index to the hashed email for faster lookups during login
                entity.HasIndex(e => e.EmailHash).IsUnique();

                entity.Property(e => e.PasswordHash)
                    .IsRequired();

                entity.Property(e => e.FullName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("NOW()"); // PostgreSQL specific current timestamp
            });
        }
    }
}
