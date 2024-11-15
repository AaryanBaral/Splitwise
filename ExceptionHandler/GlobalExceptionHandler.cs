using System.Diagnostics;
using System.Net;
using Azure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Splitwise_Back.ExceptionHandler;

public class GlobalExceptionHandler : IExceptionHandler
{
    private ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        _logger.LogError(exception,
            "Could not process a request on machine {MachineName}, TraceId:{TraceId}",
            Environment.MachineName,
            traceId);

        var (statusCode, title) = MapException(exception);

        await Results.Problem(
            title: title,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                { "traceId", traceId },
            }
        ).ExecuteAsync(context);
        return true;
    }

    private static (int statusCode, string title) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentOutOfRangeException => ((int)HttpStatusCode.BadRequest, exception.Message),
            ArgumentNullException => ((int)HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => ((int)HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, exception.Message),
            InvalidOperationException => ((int)HttpStatusCode.BadRequest, exception.Message),
            TimeoutException => ((int)HttpStatusCode.InternalServerError, exception.Message),
            DbUpdateException => ((int)HttpStatusCode.BadRequest, exception.Message),
            JsonSerializationException => ((int)HttpStatusCode.BadRequest, exception.Message),
            InvalidCastException => ((int)HttpStatusCode.BadRequest, exception.Message),
            FormatException => ((int)HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException => ((int)HttpStatusCode.NotFound, exception.Message),
            AuthenticationFailureException => ((int)HttpStatusCode.BadRequest, exception.Message),
            RequestFailedException => ((int)HttpStatusCode.BadRequest, exception.Message),
            SqlException sqlEx => HandleSqlException(sqlEx), // Direct handling of SqlException
            _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error"),
        };
    }

    private static (int statusCode, string title) HandleSqlException(SqlException sqlEx)
    {
        // Handle specific SQL exception error codes
        return sqlEx.Number switch
        {
            2627 => ((int)HttpStatusCode.BadRequest, "Duplicate entry, unique constraint violation."), // Primary key violation
            8115 => ((int)HttpStatusCode.BadRequest, "Arithmetic overflow error.."), // Primary key violation
            547 => ((int)HttpStatusCode.BadRequest, "Foreign key constraint violation."), // Foreign key violation
            1205 => ((int)HttpStatusCode.InternalServerError, "Deadlock detected."), // Deadlock
            4060 => ((int)HttpStatusCode.InternalServerError, "Cannot open database requested by the login."), // Database connection issue
            53 => ((int)HttpStatusCode.InternalServerError, "Cannot connect to the server."),
            18456 => ((int)HttpStatusCode.Unauthorized, "Login failed for user."), // Authentication failure
            -2 => ((int)HttpStatusCode.RequestTimeout, "SQL query timeout."), // Query timeout
            515 => ((int)HttpStatusCode.BadRequest, "Cannot insert NULL value."),
            23000 => ((int)HttpStatusCode.BadRequest, "Integrity constraint violation."),
            9002 => ((int)HttpStatusCode.InternalServerError, "Transaction log is full."),
            _ => ((int)HttpStatusCode.InternalServerError, "Sql Error: "+sqlEx.Message), // Default SQL error
        };
    }
}