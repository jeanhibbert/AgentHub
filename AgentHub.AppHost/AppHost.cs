var builder = DistributedApplication.CreateBuilder(args);

const string sqlPassword = "AgentHubSqlP@ssw0rd!";
const string serviceBusPassword = "AgentHubSbSqlP@ssw0rd!";
const string serviceBusKey = "SAS_KEY_VALUE";

var serviceBusConfigPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "infra", "servicebus", "config.json"));
var repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));

var commoditiesSql = builder.AddContainer("commodities-sql", "mcr.microsoft.com/mssql/server", "2022-latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlPassword)
    .WithEndpoint(port: 14331, targetPort: 1433, scheme: "tcp", name: "sql");

var ratesSql = builder.AddContainer("rates-sql", "mcr.microsoft.com/mssql/server", "2022-latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlPassword)
    .WithEndpoint(port: 14332, targetPort: 1433, scheme: "tcp", name: "sql");

var serviceBusSql = builder.AddContainer("servicebus-sql", "mcr.microsoft.com/mssql/server", "2022-latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", serviceBusPassword)
    .WithEndpoint(port: 14333, targetPort: 1433, scheme: "tcp", name: "sql");

var serviceBus = builder.AddContainer("servicebus-emulator", "mcr.microsoft.com/azure-messaging/servicebus-emulator", "latest")
    .WithEnvironment("SQL_SERVER", "servicebus-sql")
    .WithEnvironment("MSSQL_SA_PASSWORD", serviceBusPassword)
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("EMULATOR_HTTP_PORT", "5300")
    .WithBindMount(serviceBusConfigPath, "/ServiceBus_Emulator/ConfigFiles/Config.json", true)
    .WithEndpoint(port: 5672, targetPort: 5672, scheme: "tcp", name: "amqp")
    .WithHttpEndpoint(port: 5300, targetPort: 5300, name: "http")
    .WaitFor(serviceBusSql);

#pragma warning disable ASPIREPROXYENDPOINTS001
serviceBus = serviceBus.WithEndpointProxySupport(false);
#pragma warning restore ASPIREPROXYENDPOINTS001

var ollama = builder.AddContainer("ollama", "ollama/ollama", "latest")
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http");

#pragma warning disable ASPIREPROXYENDPOINTS001
ollama = ollama.WithEndpointProxySupport(false);
#pragma warning restore ASPIREPROXYENDPOINTS001

var commoditiesApi = builder.AddDockerfile("commodities-api", repoRoot, "AgentHub.Commodities.Api/Dockerfile", "final")
    .WithHttpEndpoint(port: 17011, targetPort: 8080, name: "http")
    .WithEnvironment("ConnectionStrings__commoditiesdb", "Server=commodities-sql,1433;Database=commoditiesdb;User Id=sa;Password=AgentHubSqlP@ssw0rd!;TrustServerCertificate=True;Encrypt=False")
    .WithEnvironment("ServiceBus__ConnectionString", $"Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={serviceBusKey};UseDevelopmentEmulator=true;");

#pragma warning disable ASPIREPROXYENDPOINTS001
commoditiesApi = commoditiesApi.WithEndpointProxySupport(false);
#pragma warning restore ASPIREPROXYENDPOINTS001

var ratesApi = builder.AddDockerfile("rates-api", repoRoot, "AgentHub.Rates.Api/Dockerfile", "final")
    .WithHttpEndpoint(port: 17012, targetPort: 8080, name: "http")
    .WithEnvironment("ConnectionStrings__ratesdb", "Server=rates-sql,1433;Database=ratesdb;User Id=sa;Password=AgentHubSqlP@ssw0rd!;TrustServerCertificate=True;Encrypt=False")
    .WithEnvironment("ServiceBus__ConnectionString", $"Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={serviceBusKey};UseDevelopmentEmulator=true;");

#pragma warning disable ASPIREPROXYENDPOINTS001
ratesApi = ratesApi.WithEndpointProxySupport(false);
#pragma warning restore ASPIREPROXYENDPOINTS001

builder.AddDockerfile("commodities-worker", repoRoot, "AgentHub.Commodities.Worker/Dockerfile", "final")
    .WithEnvironment("ServiceBus__ConnectionString", $"Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={serviceBusKey};UseDevelopmentEmulator=true;")
    .WaitFor(serviceBus);

builder.AddDockerfile("rates-worker", repoRoot, "AgentHub.Rates.Worker/Dockerfile", "final")
    .WithEnvironment("ServiceBus__ConnectionString", $"Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={serviceBusKey};UseDevelopmentEmulator=true;")
    .WaitFor(serviceBus);

builder.AddDockerfile("correlation-worker", repoRoot, "AgentHub.Correlation.Worker/Dockerfile", "final")
    .WithEnvironment("ServiceBus__ConnectionString", $"Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={serviceBusKey};UseDevelopmentEmulator=true;")
    .WithEnvironment("McpEndpoints__Commodities", "http://commodities-api:8080/mcp")
    .WithEnvironment("McpEndpoints__Rates", "http://rates-api:8080/mcp")
    .WithEnvironment("Ollama__BaseUrl", "http://ollama:11434")
    .WithEnvironment("Ollama__Model", "phi4-mini")
    .WaitFor(serviceBus);

var tradingDashboard = builder.AddDockerfile("trading-dashboard", repoRoot, "AgentHub.Web/Dockerfile", "final")
    .WithHttpEndpoint(port: 17020, targetPort: 8080, name: "http")
    .WithEnvironment("ServiceEndpoints__CommoditiesApi", "http://commodities-api:8080")
    .WithEnvironment("ServiceEndpoints__RatesApi", "http://rates-api:8080")
    .WithEnvironment("ServiceEndpoints__CommoditiesMcp", "http://commodities-api:8080/mcp")
    .WithEnvironment("ServiceEndpoints__RatesMcp", "http://rates-api:8080/mcp")
    .WithEnvironment("ServiceEndpoints__Ollama", "http://ollama:11434")
    .WithEnvironment("Ollama__Model", "phi4-mini");

#pragma warning disable ASPIREPROXYENDPOINTS001
tradingDashboard = tradingDashboard.WithEndpointProxySupport(false);
#pragma warning restore ASPIREPROXYENDPOINTS001

builder.Build().Run();
