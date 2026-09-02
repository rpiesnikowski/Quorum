var builder = DistributedApplication.CreateBuilder(args);

// 1. Rejestracja głównego backendu Quorum (Open.IdentityServer + AdminUI Blazor + Identity)
var backend = builder.AddProject<Projects.Quorum_Backend>("quorum-backend")
    .WithExternalHttpEndpoints();

// 2. Rejestracja bramki Quorum API Gateway (Proxy2ManyHostsMiddleware z OpenTelemetry Tracing)
// Bramka otrzymuje referencję sieciową do backendu oraz automatyczną konfigurację OTLP
var gateway = builder.AddProject<Projects.Quorum_Backend_Gateway>("quorum-gateway")
    .WithReference(backend)
    .WithExternalHttpEndpoints();

// 3. Uruchomienie orkiestracji .NET Aspire z wbudowanym Aspire Dashboard UI
// Aspire Dashboard automatycznie wizualizuje:
// - Rozproszone ślady żądań Proxy2ManyHosts (Traces Waterfall)
// - Metryki bramki i serwerów (Metrics: Requests/sec, Latency, Error Rate)
// - Strukturyzowane logi OpenTelemetry powiązane z TraceId (Structured Logs)
// - Zasoby i stan zdrowia kontenerów (Resources & Health Checks)
builder.Build().Run();
