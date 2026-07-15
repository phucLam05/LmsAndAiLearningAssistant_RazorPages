# LMS & AI Learning Assistant

## 1. General System Description
LmsAndAiLearningAssistant is a Learning Management System (LMS) integrated with an AI Learning Assistant, built on **ASP.NET Core 10.0 Razor Pages**. 
The system allows lecturers to upload course documents, and students can interact with an AI chatbot (based on the RAG - Retrieval-Augmented Generation model) to ask questions. The AI's responses are strictly based on the documents provided by the lecturers, ensuring accuracy and relevance to the curriculum.

**Key Features:**
- **Role-based Access:** Admin, Lecturer, and Student roles with clear access boundaries.
- **Document Management:** Secure document upload and storage using Supabase Storage.
- **AI Assistant (RAG):** Students can select specific documents within a subject and ask questions. The AI uses `pgvector` for similarity search and the Gemini API to generate answers based on those documents, with active citation de-duplication and page-level references.
- **Real-time Progress & Updates:** Employs SignalR to broadcast document upload, chunking, and embedding progress, as well as subject allocations in real-time.
- **Background Processing:** Utilizes Hangfire to asynchronously process document text extraction (chunking) and vector embedding generation.

## 2. System Architecture
The solution is built using a strict **3-Tier Architecture** to separate concerns, making the codebase maintainable and scalable.

![System Architecture Diagram](architecture.svg)

### Solution Layers:
- **PL (Presentation Layer):** The ASP.NET Core Razor Pages application hosting the UI, ViewModels, SignalR Hubs, and page endpoints. It communicates with the business logic via interfaces.
- **BLL (Business Logic Layer):** Contains all business services (Authentication, UserService, Chat/RAG logic, Document processing, and encryption utilities) and coordinates Hangfire background jobs.
- **DAL (Data Access Layer):** Responsible for communicating with the database (Entity Framework Core), repository pattern implementation, Supabase Storage API, and the Gemini API.
- **Core:** Contains shared elements like Entities, DTOs, and Enums. It has zero dependencies on any other layer and is referenced by all of them.

## 3. Setup and Run Instructions

### 3.1. Prerequisites
- **.NET 10.0 SDK**
- **PostgreSQL** with the `pgvector` extension installed.
- A **Supabase** project (for private file storage).
- **Gemini API Key** (for the AI Chatbot and Embeddings).

### 3.2. Configuration
1. Open the `PL/appsettings.json` (or `appsettings.Development.json`) file.
2. Fill in the required connection strings and API keys:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=your_db;Username=postgres;Password=your_password"
  },
  "Security": {
    "EncryptionKey": "your_32_byte_aes_encryption_key_here"
  },
  "Supabase": {
    "Url": "https://YOUR_PROJECT_REF.supabase.co/rest/v1/",
    "ServiceRoleKey": "YOUR_SUPABASE_SERVICE_ROLE_KEY",
    "Bucket": "Document"
  },
  "Upload": {
    "MaxFileSize": 52428800,
    "AllowedMimeTypes": {
      ".pdf": [ "application/pdf" ],
      ".doc": [ "application/msword" ],
      ".docx": [ "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ],
      ".ppt": [ "application/vnd.ms-powerpoint" ],
      ".pptx": [ "application/vnd.openxmlformats-officedocument.presentationml.presentation" ],
      ".txt": [ "text/plain" ],
      ".csv": [ "text/csv", "application/csv", "application/vnd.ms-excel" ],
      ".md": [ "text/markdown", "text/plain" ],
      ".rtf": [ "application/rtf", "text/rtf" ],
      ".json": [ "application/json", "text/json" ],
      ".xml": [ "application/xml", "text/xml" ]
    }
  },
  "GeminiSettings": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "models/gemini-embedding-001",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/"
  },
  "SmtpSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your_email@gmail.com",
    "Password": "your_app_password",
    "EnableSsl": true,
    "FromAddress": "no-reply@yourdomain.com"
  },
  "AllowedHosts": "*"
}
```

### 3.3. Database Migrations
Use Entity Framework Core to create the database and tables (Ensure your PostgreSQL user has the privilege to install the `vector` extension):

```bash
# Run this from the solution root directory:
dotnet ef database update --project DAL/DAL.csproj --startup-project PL/PL.csproj
```

### 3.4. Supabase Storage Setup
1. Create a bucket named `Document` and set its privacy to Private.
2. Set an upload size limit (e.g., 50MB) and configure the policy to allow file types like `.pdf`, `.doc`, `.docx`, `.ppt`, `.pptx`, `.txt`, `.csv`, `.md`, `.rtf`, `.json`, `.xml`.

### 3.5. Run the Application
```bash
# From the solution root directory:
dotnet run --project PL/PL.csproj
```
- Access the application at: `https://localhost:7120` or `http://localhost:5011` (or the port specified in launchSettings).
- Hangfire Dashboard (Requires Admin account): `https://localhost:7120/hangfire`
