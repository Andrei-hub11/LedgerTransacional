using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using LedgerTransacional.Functions.Common;
using LedgerTransacional.Models.DTOs.Requests;
using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LedgerTransacional.Functions.Accounts;

public class CreateAccount
{
    private readonly IAccountService _accountService;

    /// <summary>
    /// Construtor padrão - usado pela AWS Lambda
    /// </summary>
    public CreateAccount()
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var dynamoDbClient = new AmazonDynamoDBClient(Amazon.RegionEndpoint.GetBySystemName(region));
        _accountService = new AccountService(dynamoDbClient);
    }

    /// <summary>
    /// Construtor para injeção de dependências nos testes
    /// </summary>
    public CreateAccount(IAccountService accountService)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
    }

    /// <summary>
    /// Função Lambda para criação de conta
    /// </summary>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogLine("CreateAccount - Processing request");

        try
        {
            // Validar que o corpo da requisição existe
            if (string.IsNullOrEmpty(request.Body))
            {
                return ApiResponseBuilder.BadRequest("Request body is required");
            }

            // Deserializar o corpo da requisição
            var createAccountRequest = JsonSerializer.Deserialize<CreateAccountRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            // Validar que a deserialização foi bem sucedida
            if (createAccountRequest == null)
            {
                return ApiResponseBuilder.BadRequest("Invalid request format");
            }

            // Criar a conta usando o serviço
            var result = await _accountService.CreateAccountAsync(createAccountRequest);

            // Retornar a resposta
            return ApiResponseBuilder.Created(JsonSerializer.Serialize(result));
        }
        catch (ArgumentException ex)
        {
            context.Logger.LogLine($"Validation error: {ex.Message}");
            return ApiResponseBuilder.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error creating account: {ex.Message}");
            context.Logger.LogLine(ex.StackTrace);
            return ApiResponseBuilder.InternalServerError("An error occurred while creating the account");
        }
    }
}