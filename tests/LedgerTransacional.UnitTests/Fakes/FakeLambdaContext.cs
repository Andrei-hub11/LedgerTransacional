using Amazon.Lambda.Core;

namespace LedgerTransacional.UnitTests.Fakes;

public class FakeLambdaContext : ILambdaContext
{
    private readonly FakeLambdaLogger _logger = new FakeLambdaLogger();

    public string AwsRequestId { get; set; } = Guid.NewGuid().ToString();
    public IClientContext ClientContext { get; set; } = null!;
    public string FunctionName { get; set; } = "TestFunction";
    public string FunctionVersion { get; set; } = "1.0";
    public ICognitoIdentity Identity { get; set; } = null!;
    public string InvokedFunctionArn { get; set; } = "arn:aws:lambda:us-east-1:123456789012:function:TestFunction";
    public ILambdaLogger Logger => _logger;
    public string LogGroupName { get; set; } = "/aws/lambda/TestFunction";
    public string LogStreamName { get; set; } = Guid.NewGuid().ToString();
    public int MemoryLimitInMB { get; set; } = 256;
    public TimeSpan RemainingTime { get; set; } = TimeSpan.FromSeconds(30);

    public FakeLambdaContext()
    {
    }

    public FakeLambdaContext(string functionName)
    {
        FunctionName = functionName;
        LogGroupName = $"/aws/lambda/{functionName}";
    }

    public class FakeLambdaLogger : ILambdaLogger
    {
        public List<string> LoggedMessages { get; } = new List<string>();

        public void Log(string message)
        {
            LoggedMessages.Add(message);
            Console.WriteLine($"[Lambda] {message}");
        }

        public void LogLine(string message)
        {
            LoggedMessages.Add(message);
            Console.WriteLine($"[Lambda] {message}");
        }
    }

    public List<string> GetLogMessages()
    {
        return _logger.LoggedMessages;
    }
}