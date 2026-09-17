# 🏦 FinBank API

> A robust, production-grade enterprise banking API built strictly on **.NET 9.0**, **Clean Architecture**, and **CQRS Pattern**.

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Mostafa-SAID7_Bank-Api&metric=alert_status&token=64cd308bb15671223d91856900d4d1e23843ad91)](https://sonarcloud.io/summary/new_code?id=Mostafa-SAID7_Bank-Api)

---

## 🎯 Focus Points

### 1. Architecture
- **Clean Architecture**: Hard separation of Domain, Application, Infrastructure, and API layers.
- **CQRS**: Commands (Writes) and Queries (Reads) are strictly isolated using **MediatR**.
- **Pure Domain**: All business rules (e.g., overdraft limits, dormancy checks) are encapsulated in Domain Policies.

### 2. Security
- **Authentication**: JWT Bearer tokens with secure Rotation and Refresh pipelines.
- **Identity**: Fully integrated ASP.NET Core Identity for standard Identity Management.
- **Authorization**: Resource-based policies and Role-Based Access Control (Admin, Manager, User).
- **Protection**: Strict Rate Limiting, IP Whitelisting, and robust Security Headers middleware.

### 3. Engineering Quality
- **Testing Layers**: Segmented testing strategy (Domain Unit Tests, Application Handler Tests, API Integration Tests).
- **Data Integrity**: Soft-delete mechanisms across the board and an immutable Audit Trail for critical financial data.
- **Database**: PostgreSQL (Neon Serverless) optimized with 50+ indexes.

---

## 🚀 Quick Start

Ensure you have **.NET 9.0 SDK** and a running **PostgreSQL** instance.

```bash
# 1. Clone & Restore
git clone https://github.com/Mostafa-SAID7/Bank-Api.git
cd Bank-Api
dotnet restore

# 2. Configure Database
# Set your PostgreSQL connection string
export DATABASE_URL="postgresql://user:password@localhost:5432/bank_db"

# 3. Build & Run
dotnet build
dotnet run --project src/Bank.Api/Bank.Api.csproj
```

API Documentation will be accessible at: `http://localhost:5000/swagger`

---

## 📚 Documentation
- [Features Overview](docs/FEATURES.md)
- [Project Setup](docs/PROJECT_SETUP.md)
- [Architecture Structure](docs/STRUCTURE.md)
- [Deployment Guide](docs/DEPLOYMENT.md)

*Licensed under MIT.*
