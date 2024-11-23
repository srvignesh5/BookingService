namespace BookingService.Models
{
    public class Passenger
    {
        public int PassengerId { get; set; }
        public int BookingId { get; set; }
        public string? FullName { get; set; }
        public int Age { get; set; }
        public string? Gender { get; set; } 
        public string? Status { get; set; }
    }
}
