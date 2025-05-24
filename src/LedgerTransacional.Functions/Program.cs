using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;

using LedgerTransacional.Services.Implementations;
using LedgerTransacional.Services.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace LedgerTransacional.Functions;

public class Program
{
    // Static ServiceProvider that will be used by Lambda functions
    public static ServiceProvider? ServiceProvider { get; private set; }

    public static void Main(string[] args)
    {
        InitializeServices();
    }

    private static void InitializeServices()
    {
        var services = new ServiceCollection();

        var regionName = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        var region = RegionEndpoint.GetBySystemName(regionName);

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(region));
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(region));

        services.AddSingleton<IDynamoDBContext>(sp =>
            new DynamoDBContextBuilder().WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>()).Build());

        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IAccountService, AccountService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public static T GetService<T>() where T : class
    {
        if (ServiceProvider == null)
        {
            InitializeServices();
        }
        return ServiceProvider!.GetRequiredService<T>();
    }
}