using System.Collections.Generic;

namespace Launcher.Models;

public class LauncherData
{
    public int OnlineNow { get; set; }
    public List<ServerInfo> Recommended { get; set; } = [];
    public List<ServerInfo> LastVisited { get; set; } = [];
    public List<ServerInfo> AllServers { get; set; } = [];
}
