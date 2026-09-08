# Real Estate Enterprise Platform

Enterprise-grade real estate management platform with microservices architecture, event-driven background workers, and role-based access control.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        API Gateway (Ocelot)                         │
│                         Port: 5000                                  │
│  Routes: /api/identity/* → Identity.Microservice:5001              │
│          /api/properties/* → Property.Microservice:5002            │
│          /api/crm/* → CRM.Microservice:5003                        │
│          /api/developer/* → Developer.API:5006                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
   ┌────▼────┐          ┌─────▼─────┐        ┌─────▼─────┐
   │Identity │          │  Property │        │    CRM    │
   │ :5001   │          │   :5002   │        │    :5003  │
   └─────────┘          └───────────┘        └───────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                    ┌─────────▼──────────┐
                    │   Shared.Kafka     │
                    │  (Topics/Events)   │
                    └─────────┬──────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
   ┌────▼────┐          ┌─────▼─────┐        ┌─────▼─────┐
   │Brochure │          │   Image   │        │Platform  │
   │Generation│         │ Processor │        │Syndication│
   │ Worker  │          │  Worker   │        │  Worker   │
   └─────────┘          └───────────┘        └───────────┘
```

## Apps & Services

### Frontend Applications

| App | Path | Port | Tech Stack | Description |
|-----|------|------|------------|-------------|
| **Frontend Client** | `frontend-client/` | 5173 | Vite + React 19 + Bootstrap 5 | Internal admin/broker workspace with dark theme |
| **Client Facing App** | `client-facing-app/` | 3000 | Next.js | Public-facing marketing and lead capture site |

### Backend Microservices

| Service | Path | Port | Database | Description |
|---------|------|------|----------|-------------|
| **Identity.Microservice** | `backend-services/Identity.Microservice/` | 5001 | RealEstateIdentityDB | JWT authentication, users, roles, permissions, lead capture |
| **Property.Microservice** | `backend-services/Property.Microservice/` | 5002 | RealEstatePropertyDB | Properties, units, amenities, syndication, Excel import, document vault |
| **CRM.Microservice** | `backend-services/CRM.Microservice/` | 5003 | RealEstateCRMDB | Deals, leads, companies, legal entities, broker metrics, reports |
| **Developer.API** | `Developer.API/` | 5006 | - | Developer-specific API |

### Background Workers

| Worker | Path | Description |
|--------|------|-------------|
| **BrochureGeneration.Worker** | `backend-services/BrochureGeneration.Worker/` | PDF brochure generation using Puppeteer + Kafka |
| **ImageProcessor.Worker** | `backend-services/ImageProcessor.Worker/` | Image resizing/optimization using ImageSharp + Kafka |
| **PlatformSyndication.Worker** | `backend-services/PlatformSyndication.Worker/` | Property syndication to external portals (Property24, PrivateProperty, Gumtree) |
| **DataImport.Worker** | `backend-services/DataImport.Worker/` | Excel import processing (properties, units, contacts) via Kafka |

### Shared Libraries

| Library | Path | Description |
|---------|------|-------------|
| **Shared.Kafka** | `backend-services/Shared.Kafka/` | Central Kafka topic names, event schemas, base consumer |

### Infrastructure

| Component | Description |
|-----------|-------------|
| **API Gateway** | `api-gateway/` — Ocelot reverse proxy, routes to microservices |
| **Monitoring** | `monitoring/` — Prometheus + Grafana + Blackbox exporter |
| **Scripts** | `scripts/` — SQL seed scripts |
| **Docker Compose** | `docker-compose.yml` — Local dev stack (SQL Server, Kafka, Zookeeper, Prometheus, Grafana) |

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core, Entity Framework Core, SQL Server
- **Messaging**: Apache Kafka (Confluent.Kafka), event-driven architecture
- **Frontend**: React 19, Vite, Bootstrap 5, Recharts, Lucide icons
- **API Gateway**: Ocelot
- **Background Workers**: .NET Worker Services, PuppeteerSharp, ImageSharp
- **Excel**: ClosedXML
- **Monitoring**: Prometheus, Grafana, Blackbox Exporter
- **Containerization**: Docker, Docker Compose

## Kafka Event Flow

```
Property Upload → property-images topic → ImageProcessor.Worker
                                             ↓
                                    property-images-processed topic
                                             ↓
                                    Property.Microservice (updates gallery)
                                             ↓
                                    notifications topic → CRM.Microservice

Property Syndication Toggle → property-syndication topic → PlatformSyndication.Worker
                                                                  ↓
                                                     property-syndication-completed topic
                                                                  ↓
                                                     Property.Microservice

Excel Import Upload → data-import topic → DataImport.Worker
                                                ↓
                                    contacts topic → CRM.Microservice
                                                ↓
                                    import-completed topic → CRM.Microservice
                                                ↓
                                    notifications topic → CRM.Microservice

Brochure Generation Request → brochure-generation-requests topic → BrochureGeneration.Worker
                                                                              ↓
                                                                    brochure-generation-completed topic
                                                                              ↓
                                                                    CRM.Microservice + Property.Microservice
```

## Quick Start

### Prerequisites
- .NET 10 SDK
- Node.js 20+
- Docker & Docker Compose
- SQL Server (or use Docker)

### Local Development

1. **Start infrastructure**:
   ```bash
   docker-compose up -d sqlserver kafka zookeeper
   ```

2. **Run database migrations**:
   ```bash
   cd backend-services/Identity.Microservice && dotnet ef database update
   cd backend-services/Property.Microservice && dotnet ef database update
   cd backend-services/CRM.Microservice && dotnet ef database update
   ```

3. **Start backend services**:
   ```bash
   # Identity (5001)
   cd backend-services/Identity.Microservice && dotnet run
   
   # Property (5002)
   cd backend-services/Property.Microservice && dotnet run
   
   # CRM (5003)
   cd backend-services/CRM.Microservice && dotnet run
   
   # API Gateway (5000)
   cd api-gateway && dotnet run
   ```

4. **Start background workers**:
   ```bash
   cd backend-services/BrochureGeneration.Worker && dotnet run
   cd backend-services/ImageProcessor.Worker && dotnet run
   cd backend-services/PlatformSyndication.Worker && dotnet run
   cd backend-services/DataImport.Worker && dotnet run
   ```

5. **Start frontend**:
   ```bash
   cd frontend-client && npm install && npm run dev
   ```

### Docker Build

```bash
# Build all services
docker-compose -f docker-compose.prod.yml build
```

## Ports

| Service | Port |
|---------|------|
| API Gateway | 5000 |
| Identity.Microservice | 5001 |
| Property.Microservice | 5002 |
| CRM.Microservice | 5003 |
| Developer.API | 5006 |
| Kafka | 9092 |
| SQL Server | 1433 |
| Prometheus | 9090 |
| Grafana | 3001 |
| Blackbox Exporter | 9115 |

## Environment Variables

Key configuration is in `appsettings.json` files per service. Sensitive values should be set via environment variables or user secrets in production.

### Email Configuration (Gmail SMTP)
```json
{
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "your-email@gmail.com",
    "FromName": "Your Company"
  }
}
```

### Kafka Configuration
```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  }
}
```

## Key Features

- **Multi-tenant architecture** with tenant isolation
- **Role-based access control** (PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant)
- **Event-driven background processing** via Kafka
- **Property management** with units, amenities, and document vault
- **CRM** with deals, leads, companies, legal entities
- **Broker dashboard** with commission tracking and agent performance
- **System admin** with amenity catalogue management and data import
- **Platform syndication** to external property portals
- **PDF brochure generation** with Puppeteer
- **Image processing** with resizing and optimization
- **Excel import/export** for bulk data operations
- **JWT authentication** with permission-based authorization
- **Monitoring** with Prometheus + Grafana

## Scripts

- `combine-solution.sh` — Creates/updates the unified .NET solution file
- `launch-ecosystem.sh` — Launches the full local ecosystem
- `restart-ecosystem.sh` — Restarts all services
- `wait-for-it.sh` — Wait for a service to be ready
- `deploy.sh` — Deployment script

## Database

- **SQL Server** is used as the primary database
- **Entity Framework Core** with code-first migrations
- **Connection string** configured per service in `appsettings.json`

## License

Proprietary — Biscove Real Estate Tech
