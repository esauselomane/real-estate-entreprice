# Technical Specification Document

## Real Estate Enterprise Platform

**Version:** 1.0  
**Date:** September 2026  
**Classification:** Internal / Proprietary  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Architecture Overview](#2-architecture-overview)
3. [Microservices](#3-microservices)
4. [API Gateway](#4-api-gateway)
5. [Database Design](#5-database-design)
6. [Kafka Event-Driven Architecture](#6-kafka-event-driven-architecture)
7. [Authentication & Authorization](#7-authentication--authorization)
8. [Background Workers](#8-background-workers)
9. [Frontend Applications](#9-frontend-applications)
10. [Infrastructure & Monitoring](#10-infrastructure--monitoring)
11. [Deployment](#11-deployment)
12. [Configuration](#12-configuration)

---

## 1. Executive Summary

The Real Estate Enterprise Platform is a microservices-based, event-driven system designed for enterprise real estate management. It provides property listing management, CRM capabilities, broker performance tracking, lead capture, subscription billing, and multi-tenant isolation.

The platform uses a .NET 10 backend with an Ocelot API Gateway as the single entry point, Kafka for asynchronous event processing, SQL Server for data persistence, and modern frontend frameworks (Vite/React and Next.js) for user interfaces.

### Key Features

- Multi-tenant architecture with tenant-scoped data isolation
- Role-based access control (PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant)
- JWT-based authentication with permission-based authorization
- Event-driven background processing via Apache Kafka
- Property management with units, amenities, and document vault
- CRM with deals, leads, companies, legal entities, and broker performance metrics
- Broker dashboard with commission tracking and agent scoring
- Platform syndication to external property portals
- PDF brochure generation with Puppeteer
- Image processing with resizing and optimization
- Excel import/export for bulk data operations
- Prometheus + Grafana monitoring with health checks
- Docker containerization for production deployment

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        API Gateway (Ocelot)                         │
│                         Port: 5000                                  │
│  Routes: /gateway/auth/*       → Identity.Microservice:5001         │
│          /gateway/properties/* → Property.Microservice:5002        │
│          /gateway/crm/*        → CRM.Microservice:5003             │
│          /gateway/developer/*  → Developer.API:5006                │
│          /gateway/system/*     → Identity.Microservice:5001         │
│          /gateway/payments/*   → Identity.Microservice:5001         │
└─────────────────────────────────────────────────────────────────────┘
                              │
    ┌─────────────────────────┼─────────────────────────┐
    │                         │                         │
┌───▼───┐                ┌────▼─────┐              ┌────▼─────┐
│Identity│                │ Property │              │   CRM    │
│ :5001  │                │  :5002   │              │   :5003  │
└────────┘                └──────────┘              └──────────┘
    │                          │                         │
    └──────────────────────────┼─────────────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │   Shared.Kafka      │
                    │  (Topics/Events)    │
                    └──────────┬──────────┘
                               │
    ┌──────────────────────────┼─────────────────────────┐
    │                          │                         │
┌───▼───┐                ┌─────▼─────┐              ┌────▼──────┐
│Brochure│              │  Image    │              │ Platform  │
│Generation│             │ Processor │              │ Syndication│
│ Worker  │              │  Worker   │              │  Worker   │
└────────┘              └───────────┘              └───────────┘
```

### Design Principles

- **Separation of Concerns:** Each microservice owns its data and domain logic
- **API Gateway Pattern:** Single entry point for routing, CORS, and request logging
- **Event-Driven Architecture:** Kafka enables loose coupling between services
- **Multi-Tenant:** Tenant ID is propagated via JWT claims and enforced at the database level
- **Stateless Services:** All microservices are stateless; state is stored in SQL Server
- **Health Checks:** Each service exposes a `/health` endpoint (except CRM which uses root)

### Technology Stack

| Layer | Technology |
|-------|-----------|
| Backend Framework | .NET 10, ASP.NET Core |
| ORM | Entity Framework Core 10 |
| Database | Microsoft SQL Server (Express in dev) |
| API Gateway | Ocelot (reverse proxy) |
| Messaging | Apache Kafka (Confluent.Kafka SDK) |
| Authentication | JWT Bearer Tokens |
| Frontend | React 19 + Vite + Bootstrap 5; Next.js (client-facing) |
| Image Processing | SixLabors.ImageSharp |
| Excel Processing | ClosedXML |
| PDF Generation | PuppeteerSharp |
| Monitoring | Prometheus, Grafana, Blackbox Exporter |
| Containerization | Docker, Docker Compose |
| CI/CD | Docker Compose (production) |

---

## 3. Microservices

### 3.1 Identity.Microservice (Port 5001)

**Database:** `RealEstateIdentityDB`  
**Namespace:** `Identity.Microservice`  
**Responsibilities:** Authentication, user management, role-based access control, subscriptions, payments, lead capture, and system configuration.

#### Key Features

- JWT token issuance and validation
- Multi-tenant user and agency management
- Role definitions: PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant
- Permission-based authorization with dynamic permission checking
- Payment gateway integration (Peach Payments, PayFast)
- Lead capture requests from the client-facing marketing site
- Pricing package management
- Subscription lifecycle management

#### Controllers and API Endpoints

| Controller | Route | Methods | Description |
|-----------|-------|---------|-------------|
| `AuthController` | `api/auth` | POST, GET, PUT | Login, registration, user management, trial onboarding, password management |
| `UserController` | `api/user` | GET, PUT, POST | User profile retrieval and updates |
| `SystemRolesController` | `api/system/roles` | GET, POST, PUT, DELETE | CRUD for system role definitions |
| `PermissionsController` | `api/system/permissions` | GET, POST, PUT, DELETE, POST(check) | Permission management and validation |
| `PricingPackagesController` | `api/system/pricing-packages` | GET, POST, PUT | Pricing/package management |
| `LeadCaptureController` | `api/system/lead-requests` | POST, GET, PUT | Lead capture from marketing site |
| `PaymentsController` | `api/payments` | POST, GET | Payment checkout, subscriptions, gateway config |
| `WebhookController` | `api/payments/webhook` | POST | Payment webhook receivers (PayFast, Peach) |

#### Auth Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Authenticate user, returns JWT token |
| POST | `/api/auth/register-user` | Register a new user |
| GET | `/api/auth/tenant-roster` | List tenants |
| GET | `/api/auth/visible-tenants` | List visible tenants |
| POST | `/api/auth/agency-tenants` | Create agency tenant |
| POST | `/api/auth/forgot-password` | Initiate password reset |
| POST | `/api/auth/reset-password` | Reset password |
| POST | `/api/auth/change-password` | Change password |
| PUT | `/api/auth/users/{userId}/disable` | Disable user |
| GET | `/api/auth/users` | List users |
| PUT | `/api/auth/users/{userId}/role` | Update user role |
| POST | `/api/auth/trial/onboarding` | Trial onboarding flow |
| POST | `/api/auth/trial/set-password` | Set trial password |

#### Database Schema

**UserAccounts**
| Column | Type | Description |
|--------|------|-------------|
| UserId | NVARCHAR(50) PK | Unique user identifier |
| TenantId | NVARCHAR(50) | Tenant association |
| Email | NVARCHAR(256) | User email |
| FullName | NVARCHAR(200) | Full name |
| Role | NVARCHAR(50) | Role name |
| LicenseNumber | NVARCHAR(100) | Broker license number |
| LicenseExpiry | DATETIME2 | License expiry date |
| OutOfOffice | BIT | Out-of-office flag |
| IsActive | BIT | Active status |
| CreatedAt | DATETIME2 | Creation timestamp |

**TenantAgencies**
| Column | Type | Description |
|--------|------|-------------|
| TenantId | NVARCHAR(450) PK | Tenant identifier |
| AgencyName | NVARCHAR(200) | Agency name |
| SubscriptionStatus | NVARCHAR(50) | Subscription status |
| IsTrial | BIT | Is trial tenant |
| TrialStartDate | DATETIME2 | Trial start |
| TrialEndDate | DATETIME2 | Trial end |
| SubscriptionEndsAt | DATETIME2 | Subscription expiry |

**SystemRoles**
| Column | Type | Description |
|--------|------|-------------|
| RoleDefId | INT PK | Role definition ID |
| Name | NVARCHAR(4000) | Role name |
| PermissionsJson | NVARCHAR(MAX) | Permissions as JSON |
| IsActive | BIT | Active status |

### 3.2 Property.Microservice (Port 5002)

**Database:** `RealEstatePropertyDB`  
**Namespace:** `Property.Microservice`  
**Responsibilities:** Property listing management, unit management, amenities, document vault, syndication, and Excel import.

#### Key Features

- Property asset management with images, descriptions, pricing
- Property unit management with lease tracking
- Amenity type catalogue management
- Property syndication to external portals
- Excel-based bulk data import
- Document vault for property and unit documents
- Maintenance call logging
- Sole mandate management

#### Controllers and API Endpoints

| Controller | Route | Methods | Description |
|-----------|-------|---------|-------------|
| `PropertyAssetsController` | `api/properties` | GET, POST, PUT | Property CRUD, search, stats, image upload |
| `PropertyUnitsController` | `api/properties` | GET, POST, PUT, DELETE | Property unit CRUD, leases, maintenance |
| `AmenityTypeController` | `api/properties` | GET, POST, PUT, DELETE | Amenity type management |
| `ImportController` | `api/properties/import` | GET, POST | Excel import template and upload |

#### Property Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/properties/search` | Search properties |
| POST | `/api/properties/create` | Create property |
| GET | `/api/properties/{propertyId}` | Get property |
| GET | `/api/properties/platforms` | External platforms list |
| PUT | `/api/properties/{propertyId}` | Update property |
| POST | `/api/properties/{propertyId}/upload-image` | Upload property image |
| POST | `/api/properties/syndicate` | Toggle syndication |
| POST | `/api/properties/brochure/generate` | Generate brochure |
| GET | `/api/properties/active-listings-count` | Count active listings |
| GET | `/api/properties/stats/growth` | Growth statistics |
| GET | `/api/properties/stats/sole-mandates` | Sole mandate statistics |

#### Unit Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/properties/{propertyId}/units` | List units |
| POST | `/api/properties/{propertyId}/units` | Create unit |
| PUT | `/api/properties/{propertyId}/units/{unitId}` | Update unit |
| DELETE | `/api/properties/{propertyId}/units/{unitId}` | Delete unit |

#### Import Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/properties/import/template` | Download Excel import template |
| POST | `/api/properties/import/upload` | Upload Excel for import |
| GET | `/api/properties/import/jobs/{jobId}` | Get import job status |

#### Database Schema

**PropertyAssets**
| Column | Type | Description |
|--------|------|-------------|
| PropertyId | INT PK | Property ID |
| CompleteAddress | NVARCHAR(MAX) | Full address |
| ErfSpatialSize | DECIMAL | Erf size |
| MarketClass | NVARCHAR(50) | Market class |
| IsForSale | BIT | Marketing for sale |
| IsForRent | BIT | Marketing for rent |
| SalePrice | DECIMAL(18,2) | Sale price |
| RentalRate | DECIMAL(18,2) | Rental rate |
| Latitude | DECIMAL(9,6) | GPS latitude |
| Longitude | DECIMAL(9,6) | GPS longitude |
| GalleryImageJson | NVARCHAR(MAX) | Gallery images JSON |
| VerificationDocumentsJson | NVARCHAR(MAX) | Verification docs |
| PropertyTypeId | INT | FK to PropertyTypes |
| TenantId | NVARCHAR(50) | Tenant scope |

**PropertyUnits**
| Column | Type | Description |
|--------|------|-------------|
| PropertyUnitId | INT PK | Unit ID |
| PropertyId | INT FK | Parent property |
| UnitNumber | NVARCHAR(50) | Unit identifier |
| NetRentableAreaSqM | DECIMAL | Area in sq meters |
| LeaseTermMonths | INT | Lease term |
| IsCurrentlyVacant | BIT | Vacancy status |
| UnitImagesJson | NVARCHAR(MAX) | Unit images JSON |
| UnitDocumentsJson | NVARCHAR(MAX) | Unit documents JSON |
| SpecificUnitRentalPrice | DECIMAL(18,2) | Custom rental price |
| SpecificUnitSalePrice | DECIMAL(18,2) | Custom sale price |

**UnitLeases**
| Column | Type | Description |
|--------|------|-------------|
| UnitLeaseId | INT PK | Lease ID |
| PropertyUnitId | INT FK | Unit reference |
| ContactId | NVARCHAR(50) | Tenant contact |
| LeaseStart | DATETIME2 | Lease start date |
| LeaseEnd | DATETIME2 | Lease end date |
| MonthlyRent | DECIMAL(18,2) | Monthly rent |
| DepositAmount | DECIMAL(18,2) | Security deposit |
| AccruedInterestPercent | DECIMAL(5,2) | Interest rate |
| IsActive | BIT | Active status |

**AmenityTypes**
| Column | Type | Description |
|--------|------|-------------|
| AmenityTypeId | INT PK | Amenity type ID |
| Name | NVARCHAR(50) | Amenity name |
| Label | NVARCHAR(100) | Display label |
| RequiresBoolean | BIT | Boolean value flag |
| RequiresNumber | BIT | Numeric value flag |
| IsResidentialOnly | BIT | Residential only |
| IsActive | BIT | Active status |

**PropertyUnitAmenities**
| Column | Type | Description |
|--------|------|-------------|
| AmenityId | INT PK | Amenity record ID |
| PropertyId | INT FK | Property reference |
| PropertyUnitId | INT NULL | Unit scope (NULL = property-level) |
| AmenityTypeId | INT FK | Amenity type |
| BooleanValue | BIT | Boolean value |
| NumberValue | INT | Numeric value |
| Notes | NVARCHAR(250) | Notes |
| IsActive | BIT | Active status |

**SoleMandates**
| Column | Type | Description |
|--------|------|-------------|
| SoleMandateId | INT PK | Mandate ID |
| PropertyId | INT FK | Property reference |
| TenantId | NVARCHAR(50) | Tenant scope |
| StartDate | DATETIME2 | Mandate start |
| EndDate | DATETIME2 | Mandate end |
| IsActive | BIT | Active status |
| ClosedAt | DATETIME2 | Closure date |

**MaintenanceCalls**
| Column | Type | Description |
|--------|------|-------------|
| MaintenanceCallId | INT PK | Call ID |
| PropertyUnitId | INT FK | Unit reference |
| RequestedByUserId | NVARCHAR(50) | Requester |
| Title | NVARCHAR(250) | Call title |
| Description | NVARCHAR(MAX) | Description |
| Status | NVARCHAR(50) | Status |
| DueDate | DATETIME2 | Due date |

**ImportJobs**
| Column | Type | Description |
|--------|------|-------------|
| Id | INT PK | Job ID |
| JobId | NVARCHAR(64) | Unique job identifier |
| ImportType | NVARCHAR(50) | Type of import |
| Status | NVARCHAR(20) | Job status |
| TotalRows | INT | Total rows |
| ProcessedRows | INT | Processed count |
| SuccessCount | INT | Success count |
| ErrorCount | INT | Error count |
| ErrorsJson | NVARCHAR(MAX) | Error details |

### 3.3 CRM.Microservice (Port 5003)

**Database:** `RealEstateCRMDB`  
**Namespace:** `CRM.Microservice`  
**Responsibilities:** Customer relationship management, deal tracking, lead management, broker performance, and reporting.

#### Key Features

- Deal pipeline management with workflow stages
- Lead tracking with promotion to contacts/brokers
- Company and legal entity management
- Operational record management (geography, contacts)
- Broker performance metrics and scoring
- Brochure card management
- Agency dashboard with summary metrics
- Reporting engine (commission, leaderboard, broker metrics)
- Notification system

#### Controllers and API Endpoints

| Controller | Route | Methods | Description |
|-----------|-------|---------|-------------|
| `CRMController` | `api/crm` | GET, POST, PUT, DELETE | Core CRM entities: contacts, agencies, brokers, operations |
| `DealsController` | `api/crm` | GET, POST, PUT, DELETE | Deal management, brochure cards, broker splits |
| `LeadsController` | `api/crm` | GET, POST, PUT | Lead management and conversion |
| `CompaniesController` | `api/crm` | GET, POST, PUT | Company management |
| `LegalEntitiesController` | `api/crm` | GET, POST, PUT | Legal entity management |
| `AgencyDashboardController` | `api/crm/agency` | GET | Agency dashboard metrics |

#### CRM Endpoints (CRMController)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/crm/operations` | List operational records |
| POST | `/api/crm/operations` | Create operational record |
| PUT | `/api/crm/operations/{recordId}` | Update operational record |
| GET | `/api/crm/contacts` | List contacts |
| POST | `/api/crm/contacts/search` | Search contacts |
| GET | `/api/crm/geography` | List geography records |
| GET | `/api/crm/geography/{geographyId}` | Get geography |
| POST | `/api/crm/contacts` | Create contact |
| PUT | `/api/crm/contacts/{contactId}` | Update contact |
| GET | `/api/crm/agencies` | List agencies |
| GET | `/api/crm/agencies/roster` | Agency roster |
| POST | `/api/crm/agencies` | Create agency |
| PUT | `/api/crm/agencies/{agencyId}` | Update agency |
| GET | `/api/crm/agencies/{agencyId}/contacts` | Agency contacts |
| POST | `/api/crm/agencies/{agencyId}/contacts` | Add contact to agency |
| DELETE | `/api/crm/contacts/{contactId}` | Delete contact |
| POST | `/api/crm/contacts/{contactId}/promote-to-broker` | Promote contact to broker |
| GET | `/api/crm/brokers` | List brokers |
| POST | `/api/crm/brokers` | Create broker |
| PUT | `/api/crm/brokers/{brokerId}` | Update broker |
| GET | `/api/crm/contacts/{contactId}` | Get contact |
| GET | `/api/crm/boards` | Get boards |
| GET | `/api/crm/report/summary` | Summary report |
| GET | `/api/crm/report/overview` | Overview report |
| GET | `/api/crm/report/commission-by-stage` | Commission by stage |
| GET | `/api/crm/report/broker-leaderboard` | Broker leaderboard |
| GET | `/api/crm/report/broker-metrics` | Broker metrics |
| GET | `/api/crm/broker-targets` | Broker targets |
| POST | `/api/crm/broker-targets` | Create broker target |
| GET | `/api/crm/broker-activity` | Broker activity |
| GET | `/api/crm/report/broker-leaderboard-v2` | V2 leaderboard |
| GET | `/api/crm/compliance/inbox` | Compliance inbox |
| GET | `/api/crm/notifications` | Notifications |
| POST | `/api/crm/notifications/{notificationId:int}/read` | Mark notification read |
| POST | `/api/crm/notifications` | Create notification |
| POST | `/api/crm/agency/message` | Send agency message |
| POST | `/api/crm/brochure/generate` | Generate brochure |

#### Deal Endpoints (DealsController)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/crm/deals/{dealId}/presentation` | Deal presentation |
| DELETE | `/api/crm/deals/{dealId}` | Delete deal |
| PUT | `/api/crm/deal/{dealId}` | Update deal |
| POST | `/api/crm/deal/{dealId}/notes` | Add deal note |
| POST | `/api/crm/reallocate` | Reallocate deals |
| POST | `/api/crm/update-deal-stage` | Update deal stage |
| GET | `/api/crm/deals/{dealId}/broker-splits` | Get broker splits |
| PUT | `/api/crm/deals/{dealId}/broker-splits` | Update broker splits |
| POST | `/api/crm/contacts/{contactId}/link-deal/{dealId}` | Link contact to deal |
| GET | `/api/crm/brochure-cards` | List brochure cards |
| GET | `/api/crm/brochure-cards/active` | Active brochure cards |
| PUT | `/api/crm/brochure-cards` | Update brochure card |
| DELETE | `/api/crm/brochure-cards/{cardId}/lines/{lineId}` | Delete brochure card line |
| POST | `/api/crm/brochure-cards/{cardId}/generated` | Record generated brochure |

#### Lead Endpoints (LeadsController)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/crm/brokers/suggested` | Suggested brokers |
| POST | `/api/crm/lead/create` | Create lead |
| PUT | `/api/crm/lead/{leadId}` | Update lead |
| POST | `/api/crm/leads/{leadId}/promote` | Promote lead |
| POST | `/api/crm/update-lead-stage` | Update lead stage |

#### CRM Database Schema

**Contacts**
| Column | Type | Description |
|--------|------|-------------|
| ContactId | INT PK | Contact ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| FirstName | NVARCHAR(100) | First name |
| LastName | NVARCHAR(100) | Last name |
| Email | NVARCHAR(256) | Email |
| Phone | NVARCHAR(50) | Phone |
| CompanyId | INT NULL | Associated company |
| IsBroker | BIT | Is broker flag |
| IsOwner | BIT | Is owner flag |

**DealRecords**
| Column | Type | Description |
|--------|------|-------------|
| DealId | INT PK | Deal ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Title | NVARCHAR(250) | Deal title |
| PropertyId | INT | Property reference |
| AskingPrice | DECIMAL(18,2) | Asking price |
| DealValue | DECIMAL(18,2) | Final deal value |
| Status | NVARCHAR(50) | Deal status |
| CurrentStage | NVARCHAR(100) | Current workflow stage |
| CreatedAt | DATETIME2 | Creation timestamp |
| ClosedAt | DATETIME2 | Closure date |

**LeadRecords**
| Column | Type | Description |
|--------|------|-------------|
| LeadId | INT PK | Lead ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Source | NVARCHAR(100) | Lead source |
| Status | NVARCHAR(50) | Lead status |
| CurrentStage | NVARCHAR(100) | Current stage |
| AssignedBrokerId | INT NULL | Assigned broker |

**BrokerRecords**
| Column | Type | Description |
|--------|------|-------------|
| BrokerId | INT PK | Broker ID |
| ContactId | INT FK | Contact reference |
| TenantId | NVARCHAR(50) | Tenant scope |
| LicenseNumber | NVARCHAR(100) | License number |
| TotalCommissions | DECIMAL(18,2) | Total commissions |
| DealsClosed | INT | Closed deals count |

**BrochureCards**
| Column | Type | Description |
|--------|------|-------------|
| CardId | INT PK | Card ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Title | NVARCHAR(250) | Card title |
| Status | NVARCHAR(50) | Card status |
| PropertyId | INT | Property reference |

**AgencyRecords**
| Column | Type | Description |
|--------|------|-------------|
| AgencyId | INT PK | Agency ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Name | NVARCHAR(200) | Agency name |
| RegistrationNumber | NVARCHAR(100) | Registration |
| Status | NVARCHAR(50) | Status |

**CompanyRecords**
| Column | Type | Description |
|--------|------|-------------|
| CompanyId | INT PK | Company ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Name | NVARCHAR(200) | Company name |
| RegistrationNumber | NVARCHAR(100) | Registration |

**LegalEntityRecords**
| Column | Type | Description |
|--------|------|-------------|
| LegalEntityId | INT PK | Entity ID |
| TenantId | NVARCHAR(50) | Tenant scope |
| Name | NVARCHAR(200) | Entity name |
| EntityType | NVARCHAR(50) | Entity type |

**BrokerMonthlyTarget**
| Column | Type | Description |
|--------|------|-------------|
| Id | INT PK | Target ID |
| BrokerId | INT FK | Broker reference |
| Month | DATETIME2 | Target month |
| TargetCommission | DECIMAL | Target commission |

**Geographic / Operational Records**
- Geography records for regional/area segmentation
- Operational records for general CRM operations

---

## 4. API Gateway

**Port:** 5000  
**Framework:** Ocelot (.NET reverse proxy)  
**Purpose:** Single entry point for all frontend clients, routing to downstream microservices

### Route Configuration

The gateway dynamically builds its route configuration based on environment variables (with Docker hostname defaults):

| Upstream Path | Downstream Service | Default Host | Default Port |
|---------------|-------------------|-------------|-------------|
| `/gateway/auth/{everything}` | Identity.Microservice | identity | 5001 |
| `/gateway/properties/{everything}` | Property.Microservice | property | 5002 |
| `/gateway/crm/{everything}` | CRM.Microservice | crm | 5003 |
| `/gateway/system/{everything}` | Identity.Microservice | identity | 5001 |
| `/gateway/payments/{everything}` | Identity.Microservice | identity | 5001 |
| `/gateway/developer/{everything}` | Developer.API | developer-api | 5006 |

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `IDENTITY_HOST` | identity | Identity service hostname |
| `IDENTITY_PORT` | 5001 | Identity service port |
| `PROPERTY_HOST` | property | Property service hostname |
| `PROPERTY_PORT` | 5002 | Property service port |
| `CRM_HOST` | crm | CRM service hostname |
| `CRM_PORT` | 5003 | CRM service port |
| `GATEWAY_BASE_URL` | http://gateway:5000 | Gateway base URL |

### Features

- Request logging with trace IDs
- CORS with configurable allowed origins
- Response header forwarding (X-Aggregates, X-Deal-Count, X-Lead-Count, X-Total-Count, Content-Disposition)
- Health endpoint: `GET /health`

### CORS Configuration

Allowed origins (configurable via `Cors:AllowedOrigins`):
- `http://localhost:5173` (frontend-client)
- `http://localhost:3000` (client-facing-app)

---

## 5. Database Design

### 5.1 Multi-Tenant Strategy

The platform uses a **database-per-service** approach with **tenant isolation at the row level**:

- Each microservice has its own database
- Tenant ID is stored as a column on all tenant-scoped tables
- JWT tokens carry `TenantId` claims
- All queries are filtered by tenant ID
- Database contexts apply query filters where applicable

### 5.2 Database Mapping

| Service | Database | Connection String (Development) |
|---------|----------|---------------------------------|
| Identity | RealEstateIdentityDB | `Server=127.0.0.1;Database=RealEstateIdentityDB;...` |
| Property | RealEstatePropertyDB | `Server=127.0.0.1;Database=RealEstatePropertyDB;...` |
| CRM | RealEstateCRMDB | `Server=127.0.0.1;Database=RealEstateCRMDB;...` |

### 5.3 Schema Evolution

- **Identity:** Uses EF Core migrations with manual `EnsureCreated()` + `Migrate()` in Program.cs
- **Property:** Uses `EnsureCreated()` + raw SQL `ALTER TABLE` for idempotent schema updates
- **CRM:** Uses EF Core migrations with `Migrate()` in Program.cs

All services run schema initialization at startup, applying any pending migrations or schema additions idempotently.

---

## 6. Kafka Event-Driven Architecture

### 6.1 Overview

The platform uses Apache Kafka for asynchronous event processing between microservices and background workers. The `Shared.Kafka` library provides centralized topic names and event schemas.

### 6.2 Kafka Configuration

| Setting | Value |
|---------|-------|
| Bootstrap Servers | `localhost:9092` (dev) / `kafka:29092` (prod) |
| Broker Image | `confluentinc/cp-kafka:7.5.0` |
| Zookeeper Image | `confluentinc/cp-zookeeper:7.5.0` |

### 6.3 Event Flow

```
Property Upload
  → property-images topic
    → ImageProcessor.Worker
      → property-images-processed topic
        → Property.Microservice (updates gallery)
          → notifications topic
            → CRM.Microservice
```

```
Property Syndication Toggle
  → property-syndication topic
    → PlatformSyndication.Worker
      → property-syndication-completed topic
        → Property.Microservice
```

```
Excel Import Upload
  → data-import topic
    → DataImport.Worker
      → contacts topic → CRM.Microservice
      → import-completed topic → CRM.Microservice
        → notifications topic → CRM.Microservice
```

```
Brochure Generation Request
  → brochure-generation-requests topic
    → BrochureGeneration.Worker
      → brochure-generation-completed topic
        → CRM.Microservice + Property.Microservice
```

### 6.4 Internal Event Processing

Both Property.Microservice and CRM.Microservice have internal background services that consume Kafka events:

- **Property.Microservice:** `ImageProcessingBackgroundService`, `SyndicationBackgroundService`, `DataImportBackgroundService`
- **CRM.Microservice:** `BrochureGenerationBackgroundService`, `DataImportBackgroundService`

---

## 7. Authentication & Authorization

### 7.1 JWT Token Flow

1. Client sends credentials to `POST /gateway/auth/login`
2. Identity.Microservice validates credentials against `UserAccounts` table
3. JWT token is issued with claims:
   - `TenantId` — tenant scope
   - `UserId` — user identifier
   - `Role` — user role (PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant)
   - `PermissionKey` — specific permissions

### 7.2 JWT Configuration

| Setting | Value |
|---------|-------|
| Issuer | `Identity.Microservice` |
| Audience | `RealEstateApp.Client` |
| Secret | `SuperSecretRealEstateEnterpriseCryptographicSigningKey123!` |
| Validation | Issuer, Audience, Lifetime, SigningKey |

### 7.3 Roles and Permissions

| Role | Description |
|------|-------------|
| PlatformAdmin | Full system access, tenant management |
| AgencyAdmin | Agency-level access, user management |
| Broker | Broker-specific access, deal management |
| Owner | Property owner access |
| Tenant | Tenant-specific access |

### 7.4 Authorization Handler

The platform uses a custom `PermissionRequirement` and `PermissionHandler` that checks permissions at runtime based on the user's role and assigned permission keys. The `PermissionPolicyProvider` dynamically resolves authorization policies.

---

## 8. Background Workers

### 8.1 Worker Architecture

The platform uses .NET `IHostedService` implementations running Kafka consumers alongside in-process background services within the microservices themselves.

### 8.2 Internal Background Services

| Service | Class | Topics |
|---------|-------|--------|
| Property.Microservice | `ImageProcessingBackgroundService` | property-images |
| Property.Microservice | `SyndicationBackgroundService` | property-syndication |
| Property.Microservice | `DataImportBackgroundService` | data-import |
| CRM.Microservice | `BrochureGenerationBackgroundService` | brochure-generation-requests |
| CRM.Microservice | `DataImportBackgroundService` | import-completed |

### 8.3 External Background Workers

| Worker | Description |
|--------|-------------|
| `BrochureGeneration.Worker` | PDF brochure generation using PuppeteerSharp |
| `ImageProcessor.Worker` | Image resizing/optimization using ImageSharp |
| `PlatformSyndication.Worker` | Property syndication to external portals |
| `DataImport.Worker` | Excel import processing via Kafka |

### 8.4 Billing Notification Worker

The Identity.Microservice includes a `BillingNotificationWorker` hosted service that runs on a schedule, sending billing notifications to tenants via email.

---

## 9. Frontend Applications

### 9.1 Frontend Client (Port 5173)

**Tech Stack:** Vite + React 19 + Bootstrap 5 + Recharts + Lucide Icons  
**Purpose:** Internal admin/broker workspace with dark theme  
**Path:** `frontend-client/`  
**API Base:** Gateway at `http://localhost:5000`

#### Features
- Dark-themed admin dashboard
- Property management UI
- CRM dashboard with charts and reports
- Broker performance tracking
- Deal pipeline visualization
- Lead management interface
- User and role administration

### 9.2 Client-Facing App (Port 3000)

**Tech Stack:** Next.js (React SSR)  
**Purpose:** Public-facing marketing and lead capture site  
**Path:** `client-facing-app/`  
**API Base:** Gateway at configured `NEXT_PUBLIC_GATEWAY_URL`

#### Features
- Marketing landing pages
- Property listings viewer
- Lead capture forms
- Contact forms
- Pricing package display

---

## 10. Infrastructure & Monitoring

### 10.1 Docker Services (docker-compose.yml)

| Service | Image | Port |
|---------|-------|------|
| SQL Server | `mcr.microsoft.com/mssql/server` | 1433 |
| Zookeeper | `confluentinc/cp-zookeeper:7.5.0` | 2181 |
| Kafka | `confluentinc/cp-kafka:7.5.0` | 9092 |
| Kafka UI | `provectuslabs/kafka-ui:latest` | 8080 |
| Prometheus | `prom/prometheus:v2.45.0` | 9090 |
| Grafana | `grafana/grafana:10.1.0` | 3001 |
| Blackbox Exporter | `prom/blackbox-exporter:v2.24.0` | 9115 |

### 10.2 Monitoring

**Prometheus** scrapes:
- Health endpoints via Blackbox Exporter
- Service: `http_2xx_or_404` module

**Grafana** provisioned dashboards:
- `rbac-health.json` — RBAC health dashboard
- `real-estate-dashboard.json` — Real estate platform metrics

### 10.3 Health Check Endpoints

| Service | Endpoint | Port |
|---------|----------|------|
| Identity | `/health` | 5001 |
| Property | `/health` | 5002 |
| Developer.API | `/health` | 5006 |
| Gateway | `/health` | 5000 |
| CRM | (root path) | 5003 |

### 10.4 Docker Network

All services communicate over the `realestate-mesh-network` (bridge driver) in local development, and `realestate-network` in production.

---

## 11. Deployment

### 11.1 Development (Local)

1. Start infrastructure: `docker-compose up -d`
2. Wait for SQL Server on port 1433
3. Start backend services (Identity, Property, CRM, Gateway)
4. Start frontend (Vite dev server on 5173, Next.js on 3000)

**Scripts:**
- `launch-ecosystem.sh` — Full local ecosystem boot
- `restart-ecosystem.sh` — Restart all services

### 11.2 Production

1. Configure `.env` from `.env.example`
2. Build: `docker-compose -f docker-compose.prod.yml build --no-cache`
3. Deploy: `docker-compose -f docker-compose.prod.yml up -d`
4. Use `deploy.sh` for automated deployment

### 11.3 Production Environment Variables

| Variable | Description |
|----------|-------------|
| `MSSQL_SA_PASSWORD` | SQL Server admin password |
| `JWT_SECRET` | JWT signing key |
| `CORS_ORIGINS` | CORS allowed origins |
| `KAFKA_BOOTSTRAP_SERVERS` | Kafka bootstrap servers |
| `GATEWAY_BASE_URL` | Gateway base URL for cross-service calls |
| `NEXT_PUBLIC_GATEWAY_URL` | Gateway URL for frontend |

---

## 12. Configuration

### 12.1 appsettings.json Structure

Each microservice has:
- `appsettings.json` — Base configuration
- `appsettings.Development.json` — Development overrides (localhost connections)

### 12.2 Key Configuration Sections

| Section | Services | Description |
|---------|----------|-------------|
| `ConnectionStrings:DefaultConnection` | All | SQL Server connection string |
| `Jwt` | All | JWT issuer, audience, secret |
| `Cors:AllowedOrigins` | Gateway, Identity | CORS origins |
| `Identity:BaseUrl` | CRM, Property | Identity service URL for cross-service calls |
| `Gateway:BaseUrl` | CRM | Gateway URL for callbacks |
| `Email` | Identity, CRM | Email SMTP configuration |
| `Payments` | Identity | Payment gateway settings |

### 12.3 Port Summary

| Service | Port |
|---------|------|
| API Gateway | 5000 |
| Identity.Microservice | 5001 |
| Property.Microservice | 5002 |
| CRM.Microservice | 5003 |
| Developer.API | 5006 |
| Frontend Client (Vite) | 5173 |
| Client-Facing App (Next.js) | 3000 |
| SQL Server | 1433 |
| Kafka | 9092 |
| Zookeeper | 2181 |
| Kafka UI | 8080 |
| Prometheus | 9090 |
| Grafana | 3001 |
| Blackbox Exporter | 9115 |

---

## Appendix A: Solution Structure

```
real-estate-enterprise/
├── api-gateway/                    # Ocelot API Gateway (port 5000)
├── backend-services/
│   ├── Identity.Microservice/      # Auth & user management (port 5001)
│   ├── Property.Microservice/      # Property management (port 5002)
│   ├── CRM.Microservice/           # CRM & deals (port 5003)
│   ├── Shared.Access/              # Shared middleware & access control
│   ├── Shared.Presentation/        # Shared presentation components
│   └── scripts/                    # SQL scripts
├── Developer.API/                  # Developer API (port 5006)
├── frontend-client/                # React admin portal (port 5173)
├── client-facing-app/              # Next.js marketing site (port 3000)
├── monitoring/                     # Prometheus, Grafana, Blackbox configs
├── docs/                           # Documentation
├── docker-compose.yml              # Local dev infrastructure
├── docker-compose.prod.yml         # Production deployment
├── restart-ecosystem.sh            # Restart all dev services
└── launch-ecosystem.sh             # Launch full dev ecosystem
```

## Appendix B: Shared Libraries

### Shared.Access

Contains:
- `SubscriptionAccessMiddleware` — Subscription validation middleware
- `SubscriptionAccessResponse` — Standard response DTO for subscription checks

### Shared.Presentation

Contains:
- `EmailContent` — Email template helpers
- `EmailLayout` — Email HTML layout render

### Shared.Kafka (referenced in solution, not present in local filesystem)

Provides centralized Kafka topic names and event schemas for cross-service communication.

---

*Document generated from codebase analysis. All endpoints and schemas reflect the current state of the repository.*
