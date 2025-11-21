using System.Net;
using System.Text.Json;
using FluentValidation;
using Inventory.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Inventory.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException validationEx => (
                (int)HttpStatusCode.UnprocessableEntity,
                "Erro de validação",
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))
            ),
            DuplicateSkuException dupEx => (
                (int)HttpStatusCode.Conflict,
                "SKU duplicado",
                dupEx.Message
            ),
            DomainException domainEx => (
                (int)HttpStatusCode.UnprocessableEntity,
                "Erro de regra de negócio",
                domainEx.Message
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Erro interno do servidor",
                "Ocorreu um erro inesperado. Por favor, tente novamente mais tarde."
            )
        };

        _logger.LogError(exception, "Exceção capturada: {ExceptionType} - {Message}", exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            detail,
            instance = httpContext.Request.Path.ToString()
        };

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails),
            cancellationToken);

        return true;
    }
}
