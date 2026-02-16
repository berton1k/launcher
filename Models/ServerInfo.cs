namespace Launcher.Models;

public class ServerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Multiplier { get; set; } = "x1";
    public int Online { get; set; }
    public bool IsOnline => Online > 0;
}
