# Data Platform API

A production-ready travel data API built with ASP.NET Core 9. Ingests flight
and user data, then exposes clean endpoints for search, analytics, and
personalised recommendations. Built with caching, pagination, and filtering
throughout.

## Demo

![Data Platform Demo](docs/demo.gif)

## What it does

The platform has four areas:

**Ingestion** takes bulk flight and user data via POST endpoints and stores
it in PostgreSQL. Designed to accept data from external feeds, scrapers,
or partner systems.

**Search** lets you query flights by origin, destination, airline, price,
and departure date. All results are paginated and cached in Redis. A
repeated search returns in under 100ms.

**Analytics** exposes aggregated insights from the data. Popular routes,
price trends over time, and peak travel periods — the kind of data a
product or commercial team would actually use.

**Recommendations** takes a user ID and returns personalised flight
suggestions based on their preferred origin, airline, and budget. Falls
back gracefully when preferred airline results are sparse.

## Why I built this

Most travel APIs I have worked with treat search, analytics, and
recommendations as separate systems with separate databases. This project
explores what it looks like to build all three on a single clean data
model, with caching as a first-class concern rather than an afterthought.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 9 |
| Database | PostgreSQL + Entity Framework Core 9 |
| Caching | Redis (StackExchange.Redis) |
| Logging | Serilog |
| API docs | Scalar |

## Running locally

**Prerequisites:** .NET 9, Docker Desktop
```bash
# Start PostgreSQL
docker run -d --name dataplatform-postgres \
  -e POSTGRES_USER=dpuser \
  -e POSTGRES_PASSWORD=dppass \
  -e POSTGRES_DB=dataplatformdb \
  -p 5433:5432 postgres:16

# Start Redis
docker run -d --name dataplatform-redis \
  -p 6379:6379 redis:7-alpine

# Run the API
dotnet run --project src/DataPlatform.Api

# Open API docs
# http://localhost:5138/scalar/v1
```

The database seeds 240 flights and 4 users automatically on first run.

## API endpoints

### Ingest

| Method | Endpoint | What it does |
|---|---|---|
| POST | `/api/ingest/flights` | Bulk ingest flight records |
| POST | `/api/ingest/users` | Bulk ingest user records |

### Search

| Method | Endpoint | What it does |
|---|---|---|
| GET | `/api/search/flights` | Search flights with filters and pagination |
| GET | `/api/search/destinations` | Search destinations by name |

**Search query parameters:**

| Parameter | Type | Description |
|---|---|---|
| `origin` | string | Filter by origin airport code |
| `destination` | string | Filter by destination airport code |
| `airline` | string | Filter by airline name |
| `maxPrice` | decimal | Maximum price filter |
| `departureDate` | datetime | Filter by departure date |
| `page` | int | Page number, default 1 |
| `pageSize` | int | Results per page, default 10 |

### Analytics

| Method | Endpoint | What it does |
|---|---|---|
| GET | `/api/analytics/popular-routes` | Most searched routes by volume |
| GET | `/api/analytics/price-trends` | Monthly price trends for a route |
| GET | `/api/analytics/peak-times` | Busiest travel months |

### Recommendations

| Method | Endpoint | What it does |
|---|---|---|
| GET | `/api/recommendations/{userId}` | Personalised flight recommendations |

## Caching strategy

All read endpoints use Redis with a cache-aside pattern. Cache keys encode
every filter parameter so different queries never collide. TTLs are tuned
per endpoint:

| Endpoint | Cache TTL |
|---|---|
| Search results | 5 minutes |
| Recommendations | 10 minutes |
| Analytics | 15 minutes |

A cold search on LHR to DXB takes around 500ms. The same search cached
returns in under 100ms.

## Project structure

DataPlatform/
├── src/
│   ├── DataPlatform.Api/             # ASP.NET Core 9 — entry point, controllers
│   ├── DataPlatform.Core/            # Domain models
│   └── DataPlatform.Infrastructure/  # EF Core, DbContext, migrations
└── README.md

## Status

| Feature | Status |
|---|---|
| PostgreSQL + EF Core migrations | Done |
| Flight and user ingestion | Done |
| Search with filters and pagination | Done |
| Redis caching on all read endpoints | Done |
| Analytics endpoints | Done |
| Personalised recommendations | Done |
| Dashboard UI | In progress |