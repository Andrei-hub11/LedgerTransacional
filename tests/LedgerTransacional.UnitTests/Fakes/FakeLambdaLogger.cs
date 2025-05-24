using Amazon.Lambda.Core;

namespace LedgerTransacional.UnitTests.Fakes;

public class FakeLambdaLogger : ILambdaLogger
{
    public List<string> LoggedMessages { get; } = new();

    public void Log(string message)
    {
        LoggedMessages.Add(message);
    }

    public void LogLine(string message)
    {
        LoggedMessages.Add(message);
    }
}