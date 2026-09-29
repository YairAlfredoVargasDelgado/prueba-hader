using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Api.Middleware;

/// <summary>
/// Traduce las excepciones a respuestas HTTP con el formato ProblemDetails (RFC 7807).
/// </summary>
/// <remarks>
/// Centralizar el manejo de errores aquí permite que los controladores queden
/// libres de bloques try/catch y garantiza que todos los fallos —esperados o no—
/// salgan con la misma forma. Los errores de dominio se traducen a su código
/// semántico; cualquier otro se registra y se responde con un 500 genérico para
/// no filtrar detalles internos al cliente.
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // Mismo codificador que usa MVC para el resto de respuestas: así los acentos
        // viajan legibles (\"solicitó\") y no como secuencias de escape.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                // La respuesta ya viaja hacia el cliente: no se puede reescribir.
                _logger.LogError(exception, "Error después de haber comenzado a enviar la respuesta.");
                throw;
            }

            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var problem = BuildProblemDetails(context, exception);

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }

    private ProblemDetails BuildProblemDetails(HttpContext context, Exception exception)
    {
        var problem = new ProblemDetails
        {
            Instance = context.Request.Path
        };

        switch (exception)
        {
            case ProductNotFoundException notFound:
                problem.Status = StatusCodes.Status404NotFound;
                problem.Title = "Producto no encontrado";
                problem.Detail = notFound.Message;
                problem.Extensions["productId"] = notFound.ProductId;
                break;

            case InsufficientStockException insufficientStock:
                // 409: la petición es válida pero choca con el estado actual del recurso.
                problem.Status = StatusCodes.Status409Conflict;
                problem.Title = "Stock insuficiente";
                problem.Detail = insufficientStock.Message;
                problem.Extensions["currentStock"] = insufficientStock.CurrentStock;
                problem.Extensions["requestedChange"] = insufficientStock.RequestedChange;
                break;

            case BusinessRuleException businessRule:
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Solicitud inválida";
                problem.Detail = businessRule.Message;
                break;

            default:
                _logger.LogError(exception, "Error no controlado procesando {Method} {Path}.",
                    context.Request.Method, context.Request.Path);

                problem.Status = StatusCodes.Status500InternalServerError;
                problem.Title = "Error interno del servidor";
                problem.Detail = "Ocurrió un error inesperado. Consulte los registros del servidor con el traceId.";
                break;
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        return problem;
    }
}
