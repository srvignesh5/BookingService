namespace BookingService.Models
{
    public class Booking
    {
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public DateTime? BookingDate { get; set; }
        public string? Status { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? PNRNumber { get; set; }

        public ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
    }
}
