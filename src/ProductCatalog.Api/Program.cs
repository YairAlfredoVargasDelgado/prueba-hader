using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using ProductCatalog.Api.Middleware;
using ProductCatalog.Application;
using ProductCatalog.Infrastructure;
using ProductCatalog.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuración
// ---------------------------------------------------------------------------
// La cadena de conexión se toma de appsettings.json y puede sobrescribirse con la
// variable de entorno ConnectionStrings__ProductCatalog, que es lo que se usa en
// Docker y en el despliegue para no versionar credenciales.
var connectionString = builder.Configuration.GetConnectionString("ProductCatalog")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'ProductCatalog'. Defínala en appsettings.json " +
        "o en la variable de entorno ConnectionStrings__ProductCatalog.");

// Varios proveedores de despliegue (Railway, Render, Fly.io...) asignan el puerto
// en tiempo de ejecución mediante la variable PORT. Si está presente se respeta;
// si no, se usan los valores por defecto de ASP.NET Core.
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ---------------------------------------------------------------------------
// Servicios
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// Los errores de validación de modelo salen con la misma forma (ProblemDetails)
// y en el mismo idioma que los errores de negocio.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida",
            Detail = "Uno o más campos no superaron la validación.",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API de Catálogo de Productos",
        Version = "v1",
        Description = """
            API REST para administrar un catálogo de productos y su inventario.

            **Puntos a tener en cuenta**

            - El stock nunca puede quedar en negativo: un ajuste que lo provoque devuelve `409 Conflict`.
            - Los movimientos de inventario se hacen con `PATCH /api/products/{id}/stock`
              enviando una cantidad con signo (positiva suma, negativa resta).
            - Todos los errores se devuelven en formato ProblemDetails (RFC 7807).
            - Las fechas se expresan en UTC con formato ISO 8601.
            """
    });

    // Los comentarios XML del proyecto de API (endpoints) y de la capa de
    // aplicación (contratos) se publican en Swagger para que la documentación
    // viva junto al código y no se desactualice.
    IncludeXmlCommentsOf(options, Assembly.GetExecutingAssembly());
    IncludeXmlCommentsOf(options, typeof(ProductCatalog.Application.Contracts.ProductResponse).Assembly);

    options.SupportNonNullableReferenceTypes();
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Preparación de la base de datos
// ---------------------------------------------------------------------------
// Crea esquema y tabla si no existen. Es idempotente y puede desactivarse con
// Database:AutoMigrate=false cuando el esquema se gestione por otra vía.
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
// El manejo de errores va primero para que cubra todo lo que venga después.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger queda habilitado también fuera de desarrollo: la documentación es parte
// del entregable y debe estar disponible en la URL pública.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API de Catálogo de Productos v1");
    options.DocumentTitle = "API de Catálogo de Productos";

    // La interfaz se sirve en la raíz para que la URL pública abra directo en la documentación.
    options.RoutePrefix = string.Empty;
});

app.MapControllers();

// Comprobación de vida para el proveedor de despliegue. Se excluye de Swagger
// porque no forma parte del contrato funcional de la API.
app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .ExcludeFromDescription();

app.Run();

static void IncludeXmlCommentsOf(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options, Assembly assembly)
{
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
}

/// <summary>
/// Expuesta para que el proyecto de pruebas pueda levantar la API en memoria.
/// </summary>
public partial class Program;
