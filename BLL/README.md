# Business Logic Layer (BLL)

The `BLL` project contains all the business rules, main processing workflows, and AI integration logic. It acts as the bridge between the Presentation Layer (PL) and the Data Access Layer (DAL).

## Folder Structure

- **`Services/`**: Contains the implementations of the business logic, such as user management, document processing, and email sending.
- **`Interfaces/`**: Defines the service contracts that the PL controllers will call, ensuring loose coupling.

## Background Jobs & AI (RAG) Integration

1. **Background Processing (Hangfire):**
   - When a document is uploaded, a background service automatically extracts its text into smaller chunks.
   - Another service then sends these chunks to the Gemini API to generate vector embeddings, which are stored in PostgreSQL via the `pgvector` extension.
   
2. **Chatbot Workflow (RAG):**
   - The chat service converts the student's natural language question into a vector.
   - It performs a similarity search using `pgvector` against the selected course documents.
   - The top relevant results are used as context in a prompt sent to Gemini to generate an accurate, document-grounded answer.

## Architectural Rule
- The BLL depends only on **Core** and the **Interfaces from the DAL**.
- It uses the **Result Pattern** to return clear success or failure states instead of throwing exceptions.
- It must never directly reference the database context or any web-specific libraries (like MVC).
