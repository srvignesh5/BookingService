
# BookingService - Airline Reservation System
**BookingService** manages flight bookings and passenger details in the Airline Reservation System. It provides CRUD operations for booking records, such as creating, retrieving, updating, and deleting bookings. This service listens on port 5002 but can also be accessed through the **API Gateway** running on port 5000.

### Key Features
- **Booking Management**: Create, update, retrieve, and delete bookings.
- **Passenger Management**: Handle passenger details associated with each booking.
- **Secure Communication**: The service uses JWT authentication to secure the endpoints.
- **API Gateway Access**: The service is also accessible via the API Gateway on port 5000.

### Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/srvignesh5/BookingService.git
   cd BookingService
   ```
2. **Restore the NuGet packages**
      ```bash
    dotnet restore
   ```
4. **Run the BookingService**
   ```bash
    dotnet run
   ```
   The service will start and listen on https://localhost:5002. However, it is also accessible via the API Gateway on port 5000.

Access the API documentation (Swagger UI) for BookingService directly
```bash
https://localhost:5002/swagger
```
Access the API through the API Gateway on port 5000:
```bash
https://localhost:5000
```
