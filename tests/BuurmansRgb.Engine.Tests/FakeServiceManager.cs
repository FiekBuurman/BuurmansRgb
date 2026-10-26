using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// Services in memory for "Herstarten", so no test touches a real service or driver. Records every
// stop and start in order.
internal sealed class FakeServiceManager : IServiceManager
{
    public HashSet<string> Services { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Drivers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Running { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Stopping these fails, as a driver another program holds.
    public Dictionary<string, ServiceControlFailure> StopFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> StartFailures { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Calls { get; } = [];

    public int LookUps { get; private set; }

    public static FakeServiceManager WithBoth()
    {
        var services = new FakeServiceManager();
        services.Add(ServiceRestarter.OpenRgbService);
        services.Add(ServiceRestarter.PawnIoService, driver: true);
        return services;
    }

    public void Add(string name, bool driver = false)
    {
        Services.Add(name);
        if (driver)
        {
            Drivers.Add(name);
        }

        Running.Add(name);
    }

    public bool Exists(string name)
    {
        LookUps++;
        return Services.Contains(name);
    }

    public string? FindDriver(string text)
    {
        LookUps++;
        return Drivers.FirstOrDefault(name => name.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    public void Stop(string name, TimeSpan timeout)
    {
        Calls.Add($"stop {name}");
        Assert.True(timeout > TimeSpan.Zero);
        Require(name);
        if (StopFailures.TryGetValue(name, out var failure))
        {
            throw new ServiceControlException(name, failure, "Fake failure.");
        }

        Running.Remove(name);
    }

    public void Start(string name, TimeSpan timeout)
    {
        Calls.Add($"start {name}");
        Assert.True(timeout > TimeSpan.Zero);
        Require(name);
        if (StartFailures.Contains(name))
        {
            throw new ServiceControlException(name, ServiceControlFailure.Other, "Fake failure.");
        }

        Running.Add(name);
    }

    private void Require(string name)
    {
        if (!Services.Contains(name))
        {
            throw new ServiceControlException(name, ServiceControlFailure.Other, "Not installed.");
        }
    }
}
