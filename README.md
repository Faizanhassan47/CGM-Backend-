# CGM Backend API & Database Service

Production-ready ASP.NET Core 10 Web API service for the Continuous Glucose Monitoring (CGM) Platform, providing secure authentication, real-time telemetry streaming, historical analytics, alerts, and patient profile management.

- **Frontend Repository:** [https://github.com/Faizanhassan47/CGM](https://github.com/Faizanhassan47/CGM)

---

## Quick Start Guide

### 1. Prerequisites
- **.NET 10 SDK** installed (`dotnet --version`).
- **Microsoft SQL Server** (LocalDB, SQL Server Express, or Standard).

### 2. Configure Environment Variables
Copy `.env.example` to `.env` in the repository root:
```powershell
Copy-Item .env.example .env
```
Ensure your `DB_CONNECTION_STRING` points to your SQL Server database:
```ini
DB_CONNECTION_STRING="Server=localhost;Database=CGM_DB;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
JWT_SECRET=your_super_secret_key_at_least_32_bytes_long!
```

### 3. Run the Backend Service
```powershell
dotnet run --project CGM.Api/CGM.Api.csproj --urls "http://0.0.0.0:5232"
```

### 4. Interactive Swagger UI
Open your browser and navigate to:
**[http://localhost:5232](http://localhost:5232)**

---

## Default Seeded Developer Credentials

On startup, `DbInitializer.cs` automatically seeds a default active patient account:
- **Email:** `faizanhassan47@gmail.com`
- **Password:** `Mahar4722@` *(or `Test1234`)*

---

## Database Migrations & Scripts

All SQL Server schemas and initial seed data scripts are located in `Database/`:
- `Database/CGM_Schema.sql`: Complete relational tables, foreign keys, and indexes.
- `Database/SeedData.sql`: Seed data for testing and development.

To update database schema with EF Core:
```powershell
dotnet ef database update --project CGM.Api/CGM.Api.csproj
```

---

## REST API Overview

- **`/api/Auth`**: Register, Login, Token Refresh, Forgot Password, Reset Password.
- **`/api/Devices`**: Register and track CGM transmitters and sensors.
- **`/api/Sensors`**: 14-day sensor warmup session initialization and wear duration.
- **`/api/Glucose`**: Live telemetry point streaming, bulk sync, 24-hour Time In Range (TIR) summary.
- **`/api/Alerts`**: Clinical threshold alarms (Urgent Low, High, Sensor Expiring).
- **`/api/Profile`**: Target glucose range customization (70–180 mg/dL), preferred units (`mg/dL` vs `mmol/L`).
- **`/api/Health`**: Health check and SQL latency monitoring.