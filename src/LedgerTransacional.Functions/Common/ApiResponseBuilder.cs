using System.Net;

using Amazon.Lambda.APIGatewayEvents;

namespace LedgerTransacional.Functions.Common;

/// <summary>
/// Helper class to build consistent API Gateway responses
/// </summary>
public static class ApiResponseBuilder
{
    private static readonly Dictionary<string, string> _jsonContentType = new()
    {
        { "Content-Type", "application/json" }
    };

    /// <summary>
    /// Creates a 200 OK response
    /// </summary>
    public static APIGatewayProxyResponse Ok(string body = null)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = body,
            Headers = _jsonContentType
        };
    }

    /// <summary>
    /// Creates a 201 Created response
    /// </summary>
    public static APIGatewayProxyResponse Created(string body = null)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Created,
            Body = body,
            Headers = _jsonContentType
        };
    }

    /// <summary>
    /// Creates a 400 Bad Request response
    /// </summary>
    public static APIGatewayProxyResponse BadRequest(string message)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.BadRequest,
            Body = System.Text.Json.JsonSerializer.Serialize(new { message }),
            Headers = _jsonContentType
        };
    }

    /// <summary>
    /// Creates a 404 Not Found response
    /// </summary>
    public static APIGatewayProxyResponse NotFound(string message = "Resource not found")
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.NotFound,
            Body = System.Text.Json.JsonSerializer.Serialize(new { message }),
            Headers = _jsonContentType
        };
    }

    /// <summary>
    /// Creates a 500 Internal Server Error response
    /// </summary>
    public static APIGatewayProxyResponse InternalServerError(string message = "An unexpected error occurred")
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Body = System.Text.Json.JsonSerializer.Serialize(new { message }),
            Headers = _jsonContentType
        };
    }
}