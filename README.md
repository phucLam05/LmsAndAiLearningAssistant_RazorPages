# RazorPages Architecture

## Architecture
This solution is organized as a three-layer architecture for ASP.NET Core Razor Pages:
- **PL** (Presentation Layer): ASP.NET Core Razor Pages project (`net10.0`) hosting the UI and endpoints.
- **BLL** (Business Logic Layer): Class library (`net10.0`) for services (Authentication, Business Logic, Encryption).
- **DAL** (Data Access Layer): Class library (`net10.0`) for repositories and database context (`Entity Framework Core`).
- **Core**: Shared models/contracts and Data Transfer Objects (`net10.0`).

## Project References
- **PL** references **BLL** and **Core**.
- **BLL** references **DAL** and **Core**.
- **DAL** references **Core**.

## Folder Structure

### PL
- `Pages/` - Razor Pages that handle both UI and backend page model logic.
  - `Auth/` - Authentication pages (Login, Register).
- `appsettings.json` - Configuration file storing database connection strings and security keys.

### BLL
- `Interfaces/` – BLL service interfaces (e.g., `IAuthService`).
- `Services/` – BLL service implementations (e.g., `AuthService`).

### DAL
- `Data/` - Database context (e.g., `ApplicationDbContext`).
- `Interfaces/` – DAL repository interfaces (e.g., `IUserRepository`).
- `Repositories/` – DAL repository implementations (e.g., `UserRepository`).

### Core
- `Entities/` - Database entities (e.g., `User`).
- `DTOs/` - Data Transfer Objects (e.g., `RegisterDTO`, `LoginDTO`).

## Security & Keys
- **Encryption Key**: The system uses AES-256 for encrypting sensitive data like emails. The encryption key must be exactly 32 bytes and is stored in `PL/appsettings.json` under `Security:EncryptionKey`.
  - *Note for Production*: In a production environment, you should move this key to a secure vault like **Azure KeyVault** or environment variables.
- **Password Hashing**: Passwords are mathematically hashed using `BCrypt.Net-Next`.
- **Authentication**: The system uses Cookie Authentication for the Razor Pages frontend.

## Database Setup & Migration
The application uses PostgreSQL. To run this project locally, ensure you have PostgreSQL installed and running.

1. Configure your local database credentials in `PL/appsettings.json` inside `ConnectionStrings:DefaultConnection`.
2. Open a terminal in the root directory and apply migrations to create the database schema:

   ```bash
   dotnet ef migrations add InitialCreate --project DAL\DAL.csproj --startup-project PL\PL.csproj
   dotnet ef database update --project DAL\DAL.csproj --startup-project PL\PL.csproj
   ```
