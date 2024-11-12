using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace Splitwise_Back.ExceptionHandler;

public class GlobalExceptionHandler:IExceptionHandler
{
    private ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
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
            _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error"),
        };
    }
}