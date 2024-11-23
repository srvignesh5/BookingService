using Microsoft.EntityFrameworkCore;

namespace BookingService.Models
{
    public class BookingDbContext : DbContext
    {
        public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Passenger> Passengers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.ToTable("Bookings");
                entity.HasKey(e => e.BookingId);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.FlightId).IsRequired();
                entity.Property(e => e.BookingDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.TotalAmount).HasColumnType("DECIMAL(10, 2)").IsRequired();
                entity.Property(e => e.PNRNumber).HasMaxLength(6).IsRequired().IsFixedLength();
            });

            modelBuilder.Entity<Passenger>(entity =>
            {
                entity.ToTable("Passengers");
                entity.HasKey(e => e.PassengerId);
                entity.Property(e => e.BookingId).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Age).IsRequired();
                entity.Property(e => e.Gender).HasMaxLength(10).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            });
        }
    }
}
