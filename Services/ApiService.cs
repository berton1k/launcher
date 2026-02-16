using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Models;

namespace Launcher.Services;

public class ApiService
{
    private static readonly HttpClient HttpClient = new();

    public async Task<LauncherData> GetLauncherDataAsync(CancellationToken cancellationToken)
    {
        var apiUrl = Environment.GetEnvironmentVariable("LAUNCHER_API_URL");
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return GetFallbackData();
        }

        try
        {
            var response = await HttpClient.GetFromJsonAsync<LauncherData>(apiUrl, cancellationToken);
            return response ?? GetFallbackData();
        }
        catch
        {
            return GetFallbackData();
        }
    }

    private static LauncherData GetFallbackData()
    {
        return new LauncherData
        {
            OnlineNow = 0,
            Recommended = new List<ServerInfo>(),
            LastVisited = new List<ServerInfo>(),
            AllServers = new List<ServerInfo>
            {
                new() { Name = "Granted", Multiplier = "x1", Online = 0 },
            }
        };
    }
}
