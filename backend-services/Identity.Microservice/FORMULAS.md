# Identity Microservice - Query Formulas & Business Logic

## User Management

### Active Users (Tenant-scoped)
```sql
SELECT * FROM UserAccount
WHERE TenantId = @currentTenantId
  AND AccountStatus = 'Active'
```

### User by Email (for login)
```sql
SELECT * FROM UserAccount
WHERE Email = @email
  AND AccountStatus = 'Active'
```

### User Roles
```sql
SELECT u.*, r.Name as RoleName
FROM UserAccount u
JOIN UserRole ur ON ur.UserId = u.UserId
JOIN Role r ON r.RoleId = ur.RoleId
WHERE u.TenantId = @currentTenantId
```

### Brokers Only (for allocation dropdowns)
```sql
SELECT u.UserId, u.FullName, u.Email, u.Phone, u.LicenseNumber
FROM UserAccount u
JOIN UserRole ur ON ur.UserId = u.UserId
JOIN Role r ON r.RoleId = ur.RoleId
WHERE u.TenantId = @currentTenantId
  AND r.Name = 'Broker'
  AND u.AccountStatus = 'Active'
```

---

## Agencies/Tenants

### Agency Details
```sql
SELECT * FROM TenantAgency
WHERE TenantId = @currentTenantId
```

### Agency Status
```sql
SELECT OperationalStatus FROM TenantAgency
WHERE TenantId = @currentTenantId
-- Values: Active, TrialPending, TrialExpired, De-registered
```

---

## Authentication & Registration

### Password Setup Token Validation
```sql
SELECT * FROM PasswordSetupToken
WHERE Token = @token
  AND ExpiresAt > GETUTCDATE()
  AND UsedAt IS NULL
```

### Create Password Setup Token
```sql
INSERT INTO PasswordSetupToken (Token, UserId, ExpiresAt, CreatedAt)
VALUES (@token, @userId, DATEADD(HOUR, 48, GETUTCDATE()), GETUTCDATE())
```

---

## Permissions

### User Permissions (Effective)
```sql
SELECT DISTINCT p.Name
FROM UserAccount u
JOIN UserRole ur ON ur.UserId = u.UserId
JOIN Role r ON r.RoleId = ur.RoleId
JOIN RolePermission rp ON rp.RoleId = r.RoleId
JOIN Permission p ON p.PermissionId = rp.PermissionId
WHERE u.UserId = @userId
  AND u.TenantId = @currentTenantId
```

### Check Permission
```csharp
// In code: PermissionCheckService.HasPermissionAsync("permission:name")
// Calls Identity /api/system/permissions/check endpoint
```

---

## Registration Flow

1. AgencyAdmin/PlatformAdmin creates user via `POST /api/auth/register-user`
2. System generates password setup token (48hr expiry)
3. Email sent using `templates/registration/password-setup.html`
4. User clicks link → `/set-password?token={token}`
5. User sets password → token marked used, account activated

---

## Configuration

```json
{
  "Jwt": {
    "Issuer": "Identity.Microservice",
    "Audience": "RealEstateApp.Client",
    "Secret": "..."
  },
  "PasswordSetup": {
    "TokenExpiryHours": 48
  }
}
```

---

## Frontend Mapping

| Frontend Component | Backend Endpoint | Formula Reference |
|-------------------|------------------|-------------------|
| Login Page | `POST /api/auth/login` | User by Email |
| Register User (Admin) | `POST /api/auth/register-user` | Active Users |
| BrokerPicker | `GET /api/auth/brokers` | Brokers Only |
| User Management | `GET /api/system/users` | Active Users |
| Permission Gates | `PermissionCheckService` | Check Permission |