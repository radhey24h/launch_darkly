namespace LaunchDarklyConsolePOC.Configuration;

/// <summary>
/// General application settings bound from the "App" section of appsettings.json.
/// </summary>
public sealed class AppOptions
{
    /// <summary>The configuration section key used in appsettings.json.</summary>
    public const string SectionName = "App";

    /// <summary>
    /// How many mock orders to generate when no command-line override is supplied.
    /// Override at runtime: dotnet run -- 10000
    /// </summary>
    public int DefaultOrderCount { get; init; } = 1000;
}
