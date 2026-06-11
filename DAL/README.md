# Data Access Layer (DAL)

The `DAL` project handles all interactions with the database and external infrastructure. It only depends on the `Core` library.

## Folder Structure

- **`Data/`**: Contains the Entity Framework Core database context configuration and interceptors (e.g., automatically updating timestamps).
- **`Repositories/`**: Contains the concrete implementations of the data access interfaces to perform CRUD operations on the database.
- **`Interfaces/`**: Defines the data access contracts that the `BLL` will depend on.
- **`Providers/`**: Contains client classes to communicate with external services, such as Supabase Storage (for files) and the Gemini API (for vector embeddings).
- **`Migrations/`**: Stores the EF Core migration history files.

## Architectural Rule
The Business Logic Layer (BLL) communicates with the DAL **exclusively through Interfaces**. It never references the database context directly.

## Running Migrations
From the solution root directory:

```bash
# Add a new migration after modifying entities
dotnet ef migrations add <MigrationName> --project DAL/DAL.csproj --startup-project PL/PL.csproj

# Apply pending migrations to the database
dotnet ef database update --project DAL/DAL.csproj --startup-project PL/PL.csproj
```
