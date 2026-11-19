# AcxiomCRM

### Enterprise Customer Relationship Management System

**AcxiomCRM** is a role-based Customer Relationship Management (CRM) web application built using **ASP.NET Core MVC, .NET 10, Entity Framework Core and PostgreSQL**.

It provides an integrated platform for managing customers, leads, sales opportunities, follow-ups, activities and business operations, with secure authentication, authorization, REST APIs and audit logging.

---

## Overview

AcxiomCRM is designed to streamline customer relationship management and sales workflows through a centralized application.

The system supports different user roles, record ownership, sales pipeline management, activity tracking and administrative monitoring.

### Key Features

- **Authentication & Authorization** — ASP.NET Core Identity with secure login and registration.
- **Role-Based Access Control** — Admin, Manager and Sales Executive roles.
- **Customer Management** — Create, view, update, search and manage customer records.
- **Lead Management** — Track leads and manage lead conversion workflows.
- **Follow-Up Management** — Schedule and manage customer follow-ups.
- **Opportunity Management** — Manage sales opportunities and pipeline stages.
- **Activity Management** — Track calls, meetings, emails and tasks.
- **Dashboard & Analytics** — Business KPIs, charts and recent activities.
- **Audit Logging** — Track important user actions and business operations.
- **RESTful APIs** — API endpoints for CRM modules.
- **JWT Authentication** — Token-based API authentication.
- **Refresh Token Rotation** — Refresh and revoke authentication tokens.
- **Centralized Exception Handling** — Consistent handling of unexpected server errors.
- **API Validation** — Structured validation error responses.
- **Rate Limiting** — Request limits for authentication and API endpoints.
- **PostgreSQL Integration** — Relational data persistence using Entity Framework Core.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend Framework | ASP.NET Core MVC |
| Runtime | .NET 10 |
| Programming Language | C# |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Authentication | ASP.NET Core Identity |
| API Security | JWT Bearer Authentication |
| Token Management | Refresh Tokens |
| API Documentation | Swagger / OpenAPI |
| Database Migration | EF Core Migrations |
| Frontend | Razor Views, HTML, CSS, JavaScript |
| Version Control | Git & GitHub |
| Development Environment | Visual Studio Code |

---

## System Architecture

```text
                 Client / Browser
                        |
                        v
              ASP.NET Core MVC
                        |
          +-------------+-------------+
          |             |             |
          v             v             v
      Controllers    Middleware    Views
          |             |
          v             v
       Services    Authentication
          |        Authorization
          |        Exception Handling
          |        Rate Limiting
          |
          v
    Entity Framework Core
          |
          v
       PostgreSQL
          |
          v
      CRM Database
```

The application follows a layered backend architecture to separate request handling, business operations, authentication, persistence and presentation.

---

## Application Modules

### 1. Customer Management
- Customer registration and management.
- Customer information updates.
- Customer search and filtering.
- Record ownership and access control.

### 2. Lead Management
- Lead creation and tracking.
- Lead status management.
- Lead conversion workflow.
- Lead ownership and access control.

### 3. Opportunity Management
- Sales opportunity management.
- Opportunity stage tracking.
- Expected deal value management.
- Sales pipeline monitoring.

### 4. Follow-Up Management
- Schedule customer follow-ups.
- Track follow-up status.
- Manage assigned follow-up records.
- Filter follow-ups by date and status.

### 5. Activity Management
- Call tracking.
- Meeting management.
- Email activity tracking.
- Task management.

### 6. Dashboard & Analytics
- CRM performance indicators.
- Customer and lead statistics.
- Opportunity pipeline overview.
- Recent activity tracking.
- Audit event visibility for administrators.

### 7. Audit Logging
- Record important application actions.
- Track users and affected records.
- Maintain audit history.
- Protect audit records against modification and deletion.

---

## Authentication & Security

AcxiomCRM implements authentication and authorization using ASP.NET Core Identity and JWT.

Security features include:

- Password hashing through ASP.NET Core Identity.
- Role-based authorization.
- JWT access tokens.
- Refresh tokens with seven-day expiration.
- Refresh-token rotation.
- SHA-256 hashing of refresh tokens before database storage.
- Refresh-token revocation.
- Centralized exception handling.
- Centralized API model validation.
- API rate limiting.
- Audit logging.

### User Roles

| Role | Responsibility |
|---|---|
| Admin | Administrative access and system management |
| Manager | Management of assigned CRM operations |
| Sales Executive | Customer, lead and sales activity management |

Access to CRM records is controlled according to user roles and record ownership.

---

## REST API

The application provides REST API controllers for its core CRM modules.

| Module | Endpoint |
|---|---|
| Authentication | `/api/auth` |
| Customers | `/api/customers` |
| Leads | `/api/leads` |
| Opportunities | `/api/opportunities` |
| Follow-Ups | `/api/followups` |
| Activities | `/api/activities` |
| Audit Logs | `/api/auditlogs` |

### Authentication Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a new user |
| POST | `/api/auth/login` | Authenticate a user |
| POST | `/api/auth/refresh` | Refresh access and refresh tokens |
| POST | `/api/auth/revoke` | Revoke a refresh token |

### API Authentication

Protected API endpoints use Bearer token authentication.

Include the access token in the request header:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Swagger UI is available in the Development environment for exploring the API.

---

## Database Design

AcxiomCRM uses PostgreSQL as its relational database.

Entity Framework Core handles database operations and schema migrations.

### Core Entities

- Customer
- Lead
- FollowUp
- Opportunity
- Activity
- AuditLog
- RefreshToken

ASP.NET Core Identity manages user accounts, roles and authentication-related data.

### Database Migrations

Create a migration:

```bash
dotnet ef migrations add MigrationName
```

Apply migrations:

```bash
dotnet ef database update
```

---

## Project Structure

```text
AcxiomCRM.Web/
│
├── Controllers/
│   ├── API/
│   └── MVC Controllers
│
├── Data/
│   └── ApplicationDbContext.cs
│
├── DTOs/
│   ├── Auth/
│   ├── Customers/
│   ├── Leads/
│   ├── Opportunities/
│   ├── FollowUps/
│   ├── Activities/
│   └── AuditLogs/
│
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
│
├── Models/
│
├── Views/
│
├── wwwroot/
│
├── Migrations/
│
├── Program.cs
│
└── appsettings.json
```

---

## Getting Started

### Prerequisites

Install the following:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/)
- [Visual Studio Code](https://code.visualstudio.com/)

### 1. Clone the Repository

```bash
git clone https://github.com/SurajRauniyar1/AcxiomCRM.git
```

Navigate to the project directory:

```bash
cd AcxiomCRM/AcxiomCRM.Web
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Configure PostgreSQL

Create a PostgreSQL database named:

```text
AcxiomCRM_DB
```

Configure the connection string using .NET User Secrets:

```bash
dotnet user-secrets init
```

Set your database connection string:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=AcxiomCRM_DB;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"
```

### 4. Configure JWT Settings

Generate a secure random JWT signing key and store it in User Secrets.

Configure the following settings:

```bash
dotnet user-secrets set "Jwt:Key" "YOUR_SECURE_RANDOM_KEY"
dotnet user-secrets set "Jwt:Issuer" "AcxiomCRM"
dotnet user-secrets set "Jwt:Audience" "AcxiomCRM.Client"
```

Use a strong secret key and never commit credentials or signing keys to GitHub.

### 5. Apply Database Migrations

```bash
dotnet ef database update
```

### 6. Build the Application

```bash
dotnet build
```

### 7. Run the Application

```bash
dotnet run
```

Open the application in your browser using the localhost URL displayed in the terminal.

Swagger is available at:

```text
/swagger
```

---

## Configuration & Environment Variables

Sensitive configuration should be stored outside source control.

| Configuration | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection |
| `Jwt:Key` | JWT signing key |
| `Jwt:Issuer` | JWT issuer |
| `Jwt:Audience` | JWT audience |

For local development, use .NET User Secrets. For production, use an appropriate secure secrets-management solution.

---

## Future Enhancements

Planned improvements include:

- Comprehensive automated unit and integration testing.
- Distributed rate limiting for multi-instance deployments.
- Advanced refresh-token reuse detection and concurrency protection.
- Redis caching and background processing.
- Advanced reporting and analytics.
- Production monitoring and observability.
- CI/CD automation.
- Cloud deployment and production infrastructure.

---

## Author

**Suraj Rauniyar**

M.Tech Computer Science & Engineering

GitHub: [SurajRauniyar1](https://github.com/SurajRauniyar1)

Project Repository: [AcxiomCRM](https://github.com/SurajRauniyar1/AcxiomCRM)

---

## License

Refer to the repository's license file for licensing terms.

---

*AcxiomCRM — Building a secure and scalable customer relationship management platform.*