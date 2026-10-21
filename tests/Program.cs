using CodexMonitor;

const string actualShape = """
{
  "schema":"usage-v1","provider":"codex","plan":"prolite","status":"cached",
  "retrieved_at":"2026-10-21T07:05:56+00:00",
  "windows":[{"name":"weekly","seconds":604800,"used_pct":33,"resets_at":"2026-10-27T07:48:58+00:00"}]
}
""";
var weekly = UsageSnapshot.Parse(actualShape);
Assert(weekly.MainWindow?.Name == "weekly", "Weekly-only account");
Assert(weekly.MainWindow?.UsedPercent == 33, "Used percentage");
Assert(weekly.MainWindow?.ResetsAt is not null, "Reset date");
Assert(weekly.Status == "cached", "Cache status");

const string multipleWindows = """
{"schema":"usage-v1","provider":"codex","windows":[
  {"name":"weekly","used_pct":80}, {"name":"5-hour","used_pct":25}]}
""";
var multiple = UsageSnapshot.Parse(multipleWindows);
Assert(multiple.MainWindow?.Name == "5-hour", "Prefer 5-hour window");

try
{
    UsageSnapshot.Parse("{\"schema\":\"unexpected\",\"provider\":\"codex\",\"windows\":[]}");
    throw new Exception("Unknown schema accepted");
}
catch (FormatException) { }

Assert(CliService.IsBrokenLauncherError("did not find executable at 'C:\\Users\\name\\AppData\\Roaming\\uv\\python\\cpython...'"), "Missing Python launcher");
Assert(CliService.IsBrokenLauncherError("Fatal error in launcher"), "Broken Python launcher");
Assert(!CliService.IsBrokenLauncherError("authentication failed"), "Authentication error is not a launcher failure");

Console.WriteLine("Parser checks passed.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
