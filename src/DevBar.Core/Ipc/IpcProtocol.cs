namespace DevBar.Core.Ipc;

public static class IpcProtocol
{
    public const string PipeName = "devbar";

    // Request verbs. Requests are single lines: "VERB arg1 arg2".
    // Responses are UTF-8 text terminated by the pipe closing.
    public const string VerbPing = "PING";
    public const string VerbVitals = "VITALS";
    public const string VerbPorts = "PORTS";
    public const string VerbKillPort = "KILL-PORT";

    /// <summary>
    /// Kill that requires user confirmation in the tray app. Deep links map to this
    /// verb so a webpage can never kill processes without an explicit click in DevBar.
    /// </summary>
    public const string VerbKillPortAsk = "KILL-PORT-ASK";
    public const string VerbOpenTab = "OPEN-TAB";
    public const string VerbWslList = "WSL-LIST";
    public const string VerbExport = "EXPORT";
}
