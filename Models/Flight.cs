namespace BookingService.Models
{
    public class Flight
    {
        public int FlightId { get; set; }
        public string? FlightNumber { get; set; }
        public string? Airline { get; set; }
        public string? DepartureCity { get; set; }
        public string? ArrivalCity { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public int AvailableSeats { get; set; }
        public decimal TicketPrice { get; set; }
    }
}
