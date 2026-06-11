# Core Layer

The `Core` project is the center of the solution. It contains the foundational structures and definitions shared across all other layers. 
**Architectural Rule:** `Core` has no dependencies on any other layer (BLL, DAL, or PL).

## Folder Structure

- **`Entities/`**: Contains the plain C# classes (POCOs) that represent the database tables (used with EF Core).
- **`DTOs/`**: Contains Data Transfer Objects used to pass data between layers without exposing the underlying database entities.
- **`Configurations/`**: Contains strongly-typed configuration classes that map to the settings in `appsettings.json`.
- **(root)**: Contains shared Enumerations used throughout the system (e.g., roles, statuses).

## Notable Design: Result Pattern
The system uses the **Result Pattern** (`Result` or `Result<T>`) in the Core layer to standardize the responses from BLL services. This approach avoids throwing exceptions for predictable errors and provides a clear success/failure status.
