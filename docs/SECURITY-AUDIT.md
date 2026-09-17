# Security Audit Report

## Current Architecture
- **Domain**: Contains entities like `Account`, `Transaction`, `User` (Identity), `Card`, `Loan`, `Deposit`, `BillPayment`.
- **Application**: Uses CQRS/MediatR with a mix of authentication and authorization features.
- **Infrastructure**: Uses Entity Framework Core with PostgreSQL and Identity services. Contains various configurations.
- **API**: ASP.NET Core Web API, controllers expose endpoints. Uses JWT authentication.
- **Tests**: Contains unit and integration tests.

## Current Authentication
- ASP.NET Core Identity is used.
- Custom JWT generation and validation exists.
- Sessions and TwoFactorTokens are modeled.

## Current Authorization
- Roles: Admin, Manager, User, Auditor.
- Resource-based authorization exists but needs consolidation.

## Current JWT
- Token generation is present in AuthService.
- Token validation is handled via middleware/JwtBearer.
- Claims include user ID, roles, email.
- Refresh tokens are implemented but need review for rotation and revocation.

## Current User Model
- Uses ASP.NET Core Identity `IdentityUser`.
- Extended with custom properties.
- Needs cleanup for duplicated flags (e.g. email confirmation, 2FA).

## Current Password Security
- Identity password hasher is used.
- Password policies and histories are configured in infrastructure.

## Duplicate Implementations

| Responsibility | Existing Implementations | Canonical Implementation | Action |
| --- | --- | --- | --- |
| Password hashing | ASP.NET Core Identity, Custom Hashers? | ASP.NET Core Identity | remove duplicates |
| JWT | Custom JwtService, Identity Tokens | Single ITokenService | consolidate |
| Current user | HttpContext access scattered | ICurrentUser abstraction | consolidate |
| Authorization | Role checks, Policy checks | MediatR behaviors, Policies | consolidate |
| 2FA / MFA | TwoFactorTokens, Identity MFA | Single IMfaService | consolidate |
| Session management | Sessions table | ISessionService | consolidate |
| Audit / Security events | AuditLog, manual logging | IAuditService | consolidate |
| Validation | FluentValidation, DataAnnotations | FluentValidation | consolidate |
| Security logging | Standard ILogger | Structured security logging | consolidate |
