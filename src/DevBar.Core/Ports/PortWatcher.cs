using DevBar.Core.Automations;

namespace DevBar.Core.Ports;

/// <summary>Diffs successive port scans and reports opened/closed ports.</summary>
public sealed class PortWatcher
{
    /// <summary>
    /// Identity of a listener. LocalAddress is part of the key because one process
    /// legitimately binds the same protocol+port on several addresses (dual-stack
    /// IPv4/IPv6, or one bind per NIC).
    /// </summary>
    private readonly record struct Key(PortProtocol Protocol, string LocalAddress, int Port, int Pid);

    private Dictionary<Key, PortInfo> _previous = [];
    private bool _hasBaseline;

    public List<PortChange> Update(List<PortInfo> current)
    {
        var currentKeys = new Dictionary<Key, PortInfo>(current.Count);
        foreach (var p in current)
        {
            currentKeys[new Key(p.Protocol, p.LocalAddress, p.Port, p.Pid)] = p;
        }

        var changes = new List<PortChange>();
        if (_hasBaseline)
        {
            foreach (var (key, info) in currentKeys)
            {
                if (!_previous.ContainsKey(key)) changes.Add(new PortChange(true, info));
            }

            foreach (var (key, info) in _previous)
            {
                if (!currentKeys.ContainsKey(key)) changes.Add(new PortChange(false, info));
            }
        }

        _previous = currentKeys;
        _hasBaseline = true;
        return changes;
    }
}
