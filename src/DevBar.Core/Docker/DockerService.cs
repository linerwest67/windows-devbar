using System.Text.Json;

namespace DevBar.Core.Docker;

public sealed record DockerContainer(string Id, string Name, string Image, string State, string Status);

public static class DockerService
{
    public static async Task<bool> IsAvailableAsync()
        => await ProcessRunner.RunAsync("docker", "version --format \"{{.Server.Version}}\"", 10_000) is { Success: true };

    public static async Task<List<DockerContainer>?> GetContainersAsync()
    {
        var result = await ProcessRunner.RunAsync("docker", "ps -a --format \"{{json .}}\"", 15_000);
        if (result is null) return null; // docker CLI missing
        if (!result.Success) return null; // daemon not running

        var containers = new List<DockerContainer>();
        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                containers.Add(new DockerContainer(
                    root.GetProperty("ID").GetString() ?? "",
                    root.GetProperty("Names").GetString() ?? "",
                    root.GetProperty("Image").GetString() ?? "",
                    root.GetProperty("State").GetString() ?? "",
                    root.GetProperty("Status").GetString() ?? ""));
            }
            catch (JsonException)
            {
                // Non-JSON noise line (warnings etc.)
            }
        }

        return containers;
    }

    public static Task<ProcessResult?> StartAsync(string id) => ProcessRunner.RunAsync("docker", $"start {id}", 30_000);
    public static Task<ProcessResult?> StopAsync(string id) => ProcessRunner.RunAsync("docker", $"stop {id}", 30_000);
    public static Task<ProcessResult?> RestartAsync(string id) => ProcessRunner.RunAsync("docker", $"restart {id}", 60_000);
}
