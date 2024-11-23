using BookingService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace BookingService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly BookingDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public BookingController(BookingDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Booking>>> GetBookings()
        {
            if (!User.IsInRole("Admin"))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });

            return Ok(await _context.Bookings.Include(b => b.Passengers).ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Booking>> GetBooking(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Passengers).FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null) return NotFound(new { message = "Booking not found." });
            if (!IsUserAuthorized(booking.UserId))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });
            return Ok(booking);
        }
        [HttpGet("GetMyBookings")]
        public async Task<ActionResult<IEnumerable<Booking>>> GetMyBookings()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized(new { message = "User data could not be validated. Please log in again." });
            if (!IsUserAuthorized(int.Parse(userIdClaim)))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });
            var booking = await _context.Bookings.Include(b => b.Passengers).Where(c=>c.UserId ==int.Parse(userIdClaim)).ToListAsync();
            if (booking == null) return NotFound(new { message = "Booking not found." });

            return Ok(booking);
        }
        [HttpPost]
        public async Task<ActionResult<Booking>> CreateBooking([FromBody] Booking booking)
        {
            if (booking == null) return BadRequest(new { message = "Booking details are required." });
            var token = GetBearerToken();
            if (!await ValidateUser(booking.UserId, token)) return BadRequest(new { message = "Invalid User ID." });
            var flight = await FetchFlightDetails(booking.FlightId, token);
            if (flight == null) return BadRequest(new { message = "Invalid Flight ID." });

            booking.BookingDate = DateTime.Now;
            booking.Status = "Pending";
            booking.TotalAmount = 0; 
            booking.PNRNumber = "000000";
            
            foreach (var passenger in booking.Passengers)
            {
                passenger.Status = "Pending";
            }

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBooking), new { id = booking.BookingId }, booking);
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(int id, Booking booking)
        {
            if (id != booking?.BookingId) return BadRequest(new { message = "Invalid booking ID." });
            if (booking == null) return BadRequest(new { message = "Booking details are required." });
            var existingBooking = await _context.Bookings.Include(b => b.Passengers).FirstOrDefaultAsync(b => b.BookingId == id);
            
            if (existingBooking == null)
                return NotFound(new { message = "Booking not found." });

            if (!IsUserAuthorized(existingBooking.UserId))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });
            if (!User.IsInRole("Admin") && (booking.Status != "Confirmed" || booking.Status != "Cancelled")) 
                return BadRequest(new { message = "Only pending bookings can be update." });

            var incomingPassengerIds = booking.Passengers.Where(p => p.PassengerId != 0).Select(p => p.PassengerId).ToHashSet();
            var passengersToRemove = existingBooking.Passengers.Where(p => !incomingPassengerIds.Contains(p.PassengerId)).ToList();
            foreach (var passenger in passengersToRemove)
            {
                _context.Passengers.Remove(passenger);
            }
            foreach (var passenger in booking.Passengers)
            {
                if (passenger.PassengerId != 0)
                {
                    var existingPassenger = existingBooking.Passengers.FirstOrDefault(p => p.PassengerId == passenger.PassengerId);

                    if (existingPassenger != null)
                    {
                        existingPassenger.FullName = passenger.FullName;
                        existingPassenger.Age = passenger.Age;
                        existingPassenger.Gender = passenger.Gender;
                        existingPassenger.Status = existingPassenger.Status;
                    }
                }
                else
                {
                    // Add new passenger
                    existingBooking.Passengers.Add(new Passenger
                    {
                        BookingId = existingBooking.BookingId,
                        FullName = passenger.FullName,
                        Age = passenger.Age,
                        Gender = passenger.Gender,
                        Status = "Pending"
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Booking updated successfully.", UpdatedBooking = existingBooking });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            if (!User.IsInRole("Admin"))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });

            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Booking deleted successfully." });
        }

        [HttpPut("{id}/confirm")]
        public async Task<IActionResult> ConfirmBooking(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Passengers).FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound(new { message = "Booking not found." });

            if (!IsUserAuthorized(booking.UserId))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });
            if (booking.Status != "Pending") return BadRequest(new { message = "Cannot confirm a non-pending booking." });

            if (!booking.Passengers.Any())
                return BadRequest(new { message = "Cannot confirm booking without passengers." });

            var token = GetBearerToken();
            var flight = await FetchFlightDetails(booking.FlightId, token);
            if (flight == null || flight.AvailableSeats < booking.Passengers.Count)
                return BadRequest(new { message = "Insufficient seats available." });

            booking.TotalAmount = booking.Passengers.Count * flight.TicketPrice;
            flight.AvailableSeats -= booking.Passengers.Count;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var updateResponse = await client.PutAsJsonAsync($"https://localhost:5000/api/Flight/{flight.FlightId}", flight);

            if (!updateResponse.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to update flight availability." });

            booking.Status = "Confirmed";
            booking.PNRNumber = GeneratePNR();
            foreach (var passenger in booking.Passengers) passenger.Status = "Confirmed";

            await _context.SaveChangesAsync();
            return Ok(new { message = "Booking confirmed successfully.", booking });
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Passengers)
                .FirstOrDefaultAsync(b => b.BookingId == id);
            
            if (booking == null)
                return NotFound(new { message = "Booking not found." });
            if (!IsUserAuthorized(booking.UserId))
                return StatusCode(403, new { message = "You are not authorized to access this resource." });
            if (booking.Status != "Confirmed")
                return BadRequest(new { message = "Only confirmed bookings can be canceled." });
            var token = GetBearerToken();
            var flight = await FetchFlightDetails(booking.FlightId, token);
            if (flight == null)
                return BadRequest(new { message = "Failed to fetch flight details." });

            var user = await FetchUserDetails(booking.UserId, token);
            if (user == null) return BadRequest(new { message = "Failed to fetch user details." });
            flight.AvailableSeats += booking.Passengers.Count;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var updateResponse = await client.PutAsJsonAsync($"https://localhost:5000/api/Flight/{flight.FlightId}", flight);

            if (!updateResponse.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to update flight availability." });

            booking.Status = "Cancelled";
            foreach (var passenger in booking.Passengers)
            {
                passenger.Status = "Cancelled";
            }
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Booking Cancelled successfully.",
                bookingDetails = new
                {
                    booking.BookingId,
                    booking.PNRNumber,
                    booking.Status,
                    booking.TotalAmount,
                    booking.BookingDate,
                    flightDetails = new
                    {
                        flight.FlightId,
                        flight.FlightNumber,
                        flight.Airline,
                        flight.DepartureCity,
                        flight.ArrivalCity,
                        flight.DepartureTime,
                        flight.ArrivalTime,
                    },
                    passengers = booking.Passengers?.Select(p => new
                    {
                        p.PassengerId,
                        p.FullName,
                        p.Gender,
                        p.Status
                    }),
                    bookedby = new
                    {
                        user.FullName
                    }
                }
            });
        }

        [HttpGet("{id}/review")]
        public async Task<IActionResult> ReviewBooking(int id)
        {
            var booking = await _context.Bookings.Include(b => b.Passengers).FirstOrDefaultAsync(b => b.BookingId == id);
            if (booking == null) return NotFound(new { message = "Booking not found." });
            if (!IsUserAuthorized(booking.UserId))
                return StatusCode(403, new { message = "You are not authorized to access this resource." }); 
            var token = GetBearerToken();
            var flight = await FetchFlightDetails(booking.FlightId, token);
            if (flight == null) return BadRequest(new { message = "Failed to fetch flight details." });
            var user = await FetchUserDetails(booking.UserId, token);
            if (user == null) return BadRequest(new { message = "Failed to fetch user details." });

            if (booking.Status == "Confirmed" || booking.Status == "Cancelled")
            {
                return Ok(new
                {
                    message = "Flight Reservation details",
                    bookingDetails = new
                    {
                        booking.BookingId,
                        booking.PNRNumber,
                        booking.Status,
                        booking.TotalAmount,
                        booking.BookingDate,
                        flightDetails = new
                        {
                            flight.FlightId,
                            flight.FlightNumber,
                            flight.Airline,
                            flight.DepartureCity,
                            flight.ArrivalCity,
                            flight.DepartureTime,
                            flight.ArrivalTime,
                        },
                        passengers = booking.Passengers?.Select(p => new
                        {
                            p.PassengerId,
                            p.FullName,
                            p.Gender,
                            p.Status
                        }),
                        bookedby = new
                        {
                            user.FullName
                        }
                    }
                });
            }
            else
            {
                return Ok(new
                {
                    message = "Booking Preview",
                    bookingDetails = new
                    {
                        booking.BookingId,
                        booking.BookingDate,
                        booking.Status,
                        flightDetails = flight,
                        passengers = booking.Passengers?.Select(p => new
                        {
                            p.PassengerId,
                            p.FullName,
                            p.Gender,
                            p.Status
                        }),
                        bookingInitiatedby = new
                        {
                            user.FullName
                        }
                    }
                });
            }
        }
        private string GetBearerToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            return token;
        }
        private bool IsUserAuthorized(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return false;
            return userIdClaim == id.ToString() || User.IsInRole("Admin");
        }
        private async Task<bool> ValidateUser(int userId, string token)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync($"https://localhost:5001/api/User/{userId}");
            return response.IsSuccessStatusCode;
        }

        private async Task<Flight?> FetchFlightDetails(int flightId, string token)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync($"https://localhost:5000/api/Flight/{flightId}");
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<Flight>();
        }

        private async Task<User?> FetchUserDetails(int userId, string token)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync($"https://localhost:5000/api/User/{userId}");
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<User>();
        }
        private string GeneratePNR()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 6).Select(s => s[new Random().Next(s.Length)]).ToArray());
        }
    }
}
