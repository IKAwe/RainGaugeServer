# Rain gauge server

ASP.NET Core 8 Web API backend for a connected rain gauge system. The server receives rainfall measurements from an external hardware device, stores records in MariaDB, queues maintenance commands, and serves a dashboard with interactive charts.

Note: The web dashboard currently includes a manual reading submission form. This is a temporary testing feature and will be removed in the final release once hardware integration is complete.

## Technologies

- .NET 8 (ASP.NET Core Web API)
- Entity Framework Core 8 with Pomelo MySQL provider
- MariaDB 10.11
- Docker and Docker Compose
- Swagger and OpenAPI
- Chart.js

## Project structure

- `docker-compose.yml`: Container orchestration for the MariaDB database and the API service.
- `RainGaugeServer/`
  - `Controllers/DeviceController.cs`: Endpoints for data ingestion, log retrieval, cleaning commands, and chart statistics.
  - `Data/AppDbContext.cs`: Database context managing the rain readings table.
  - `Models/RainReading.cs`: Data model and request payload definitions.
  - `Program.cs`: Service registration, database initialization, static files, and middleware pipeline.
  - `Dockerfile`: Container image build configuration for the .NET service.
  - `wwwroot/index.html`: Web interface showing recent readings, cleaning state, and the drill-down rainfall chart.

## Running the project

Run the services using Docker Compose:

```bash
docker compose up --build
```

- Dashboard: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger`
