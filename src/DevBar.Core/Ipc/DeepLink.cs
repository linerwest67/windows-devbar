namespace DevBar.Core.Ipc;

/// <summary>
/// Parses devbar:// URIs into pipe requests.
/// Supported: devbar://open/&lt;tab&gt;  and  devbar://kill/&lt;port&gt;
/// </summary>
public static class DeepLink
{
    public const string Scheme = "devbar";

    public static string? ToPipeRequest(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)) return null;

        // devbar://open/ports → Host="open", segments ["/","ports"]
        var action = uri.Host.ToLowerInvariant();
        var arg = uri.AbsolutePath.Trim('/');

        return action switch
        {
            "open" when arg.Length > 0 => $"{IpcProtocol.VerbOpenTab} {arg}",
            "open" => $"{IpcProtocol.VerbOpenTab} vitals",
            // Deep links come from browsers and other apps, so kill maps to the
            // confirmation-required verb — never the direct one.
            "kill" when ushort.TryParse(arg, out var port) => $"{IpcProtocol.VerbKillPortAsk} {port}",
            _ => null,
        };
    }
}
