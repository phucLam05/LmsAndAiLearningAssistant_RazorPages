# LMS & AI Learning Assistant (Razor Pages)

## Architecture
This solution is organized as a three-layer architecture using pure ASP.NET Core Razor Pages:
- **PL** (Presentation Layer): ASP.NET Core Razor Pages project (`net10.0`) hosting the UI, ViewModels, SignalR Hubs, and page endpoints.
- **BLL** (Business Logic Layer): Class library (`net10.0`) containing business services (Authentication, Chat/RAG logic, Document processing, and encryption utilities).
- **DAL** (Data Access Layer): Class library (`net10.0`) handling repositories, DbContext, and migrations using PostgreSQL + Entity Framework Core + Pgvector.
- **Core**: Shared entities, contracts, DTOs, and common models (`net10.0`).

## Project References
- **PL** references **BLL** and **Core**.
- **BLL** references **DAL** and **Core**.
- **DAL** references **Core**.

## Folder Structure & Key Modules

### PL (Presentation Layer)
- `Pages/`: Razor Pages (`.cshtml` and `.cshtml.cs`) for handling routing, page rendering, and AJAX handlers.
  - `Admin/`: Admin dashboard, document monitoring, and user management.
  - `Auth/`: Authentication pages (Login, Register, Profile/Change Password).
  - `Subject/`: Subject browsing, details, and the RAG chatbot interface.
  - `Lecturer/`: Lecturer portal for managing documents.
- `ViewModels/`: Presentation-only ViewModels (e.g., `LoginViewModel`, `RegisterViewModel`, `DocumentUploadViewModel`).
- `Hubs/`: SignalR hubs (e.g., `LmsHub`) mapping real-time broadcasts.
- `DbSeeder.cs`: Seeds default Admin, Lecturer, and Student accounts on start.
- `wwwroot/`: Static client assets, custom CSS, and AJAX dropzone upload logic.

### BLL (Business Logic Layer)
- `Interfaces/` & `Services/`: Services like `AuthService`, `UserService`, `ChatService`, and `DocumentService`.
- Coordinates Gemini RAG embedding generation and text completion logic.

### DAL (Data Access Layer)
- `Data/`: DB context (`ApplicationDbContext`) and interceptors (e.g., AuditInterceptor).
- `Interfaces/` & `Repositories/`: Repositories representing the database access layer (e.g., `ChatMessageRepository`).

### Core
- `Entities/`: Database models including `User`, `Subject`, `Document`, `ChatSession`, `ChatMessage`.
- `DTOs/`: Data Transfer Objects for moving data cleanly between layers.

---

## Key Features & Custom Implementations

1. **User Password Management**:
   - For safety and security, manual password entry during resets is disabled for administrators. 
   - When a password is reset, BLL automatically generates a strong random password, encrypts/hashes it, emails it to the user, and sets their status to `Inactive`, forcing a password change upon their first login.
   - Admin account creation is restricted to protect security boundaries.
   
2. **NotebookLM-Style Chatbot & RAG Integration**:
   - Chat routing transitioned to clean route parameters: `/Subject/Chat/{subjectId}/{sessionId?}`.
   - Implemented citation deduplication: AI answers are parsed for references, unused sources are stripped, and active sources are re-mapped sequentially `[1]`, `[2]`, etc.
   - Interactive quote box showing the exact reference text.
   - Dynamic document viewer: PDFs are rendered on their specific referenced page number (instead of always page 1), and other file formats fall back gracefully to original sources.
   
3. **SignalR Real-time Updates**:
   - Uses `/lmsHub` to broadcast subjects, allocations, and document upload/deletion changes to all active users.
   - Client browsers listen and show immediate toast notifications, reloading content seamlessly to maintain realtime consistency.

4. **Client-side Instant Filtering**:
   - Added instant keyword filtering to all main listing pages (User list, Admin/Lecturer document tables, Subject catalogs) listening to text input to show/hide records instantly on the client side.

5. **Covariance Paging Controls**:
   - Leverages `IPagedResult` in the pagination partial (`_Pager.cshtml`) to enable generic covariant binding and render page buttons dynamically.

---

## Security & Database Settings

- **Encryption Key**: AES-256 is used for encrypting sensitive user fields. Configure the 32-byte key in `PL/appsettings.json` under `Security:EncryptionKey`.
- **Password Hashing**: BCrypt.Net-Next is used for hashing passwords securely.
- **PostgreSQL Setup**: Make sure PostgreSQL is running and update `ConnectionStrings:DefaultConnection` in `PL/appsettings.json`.
- Apply migrations:
  ```bash
  dotnet ef migrations add InitialCreate --project DAL\DAL.csproj --startup-project PL\PL.csproj
  dotnet ef database update --project DAL\DAL.csproj --startup-project PL\PL.csproj
  ```
