using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using HunterPie.Core.Client;

namespace RiseOverlay.UI.Update;

/// <summary>
/// Checks GitHub Releases and applies updates via an external PowerShell helper
/// (so locked HunterPie.exe can be replaced after exit).
/// </summary>
public sealed class RiseGitHubUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public RiseGitHubUpdateService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? CreateDefaultClient();
    }

    public static string GetLocalVersionString()
    {
        Assembly? entry = Assembly.GetEntryAssembly();
        string? info = entry?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            int plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }

        Version v = ClientInfo.Version;
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public async Task<RiseUpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        string local = GetLocalVersionString();
        try
        {
            using HttpResponseMessage resp = await _http.GetAsync(RiseUpdateEndpoints.LatestReleaseApi, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new RiseUpdateCheckResult(
                    false, local, null, null, null, RiseUpdateEndpoints.ReleasesPage,
                    "还没有发布任何版本（仓库里没有 Release）。");
            }

            resp.EnsureSuccessStatusCode();
            await using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
            GitHubReleaseDto? release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, ct);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new RiseUpdateCheckResult(
                    false, local, null, null, null, RiseUpdateEndpoints.ReleasesPage,
                    "无法解析 GitHub Release 信息。");
            }

            string remote = NormalizeVersion(release.TagName);
            GitHubAssetDto? asset = release.Assets.FirstOrDefault(a =>
                a.Name.StartsWith(RiseUpdateEndpoints.ZipAssetPrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                return new RiseUpdateCheckResult(
                    false, local, remote, null, release.Body, release.HtmlUrl,
                    $"最新 Release ({release.TagName}) 没有 {RiseUpdateEndpoints.ZipAssetPrefix}*.zip 资源。");
            }

            bool hasUpdate = IsRemoteNewer(local, remote);
            return new RiseUpdateCheckResult(
                hasUpdate, local, remote, asset.BrowserDownloadUrl, release.Body, release.HtmlUrl, null);
        }
        catch (Exception ex)
        {
            return new RiseUpdateCheckResult(
                false, local, null, null, null, RiseUpdateEndpoints.ReleasesPage,
                $"检查更新失败：{ex.Message}\n若在国内，可开 VPN 后重试，或打开 Releases 页手动下载。");
        }
    }

    public async Task ApplyUpdateAsync(
        string downloadUrl,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"RiseOverlay-update-{Guid.NewGuid():N}.zip");
        string extractDir = Path.Combine(Path.GetTempPath(), $"RiseOverlay-update-{Guid.NewGuid():N}");

        try
        {
            await DownloadFileAsync(downloadUrl, zipPath, progress, ct);
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            string packageRoot = ResolvePackageRoot(extractDir);
            string? updaterPs1 = FindUpdaterScript();
            if (updaterPs1 is null)
                throw new FileNotFoundException("找不到 rise-overlay-updater.ps1，请重新安装完整包。");

            string installDir = ClientInfo.ClientPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string restartExe = ClientInfo.ClientFileName;
            int pid = Environment.ProcessId;

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterPs1}\" " +
                    $"-TargetDir \"{installDir}\" " +
                    $"-SourceDir \"{packageRoot}\" " +
                    $"-RestartExe \"{restartExe}\" " +
                    $"-WaitPid {pid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = installDir,
            };

            if (Process.Start(psi) is null)
                throw new InvalidOperationException("无法启动更新助手。");
        }
        finally
        {
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private async Task DownloadFileAsync(
        string url,
        string destPath,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        using HttpResponseMessage resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long? total = resp.Content.Headers.ContentLength;
        await using Stream input = await resp.Content.ReadAsStreamAsync(ct);
        await using FileStream output = File.Create(destPath);
        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total is > 0)
                progress?.Report(readTotal / (double)total.Value);
        }
    }

    private static string ResolvePackageRoot(string extractDir)
    {
        string exeAtRoot = Path.Combine(extractDir, "HunterPie.exe");
        if (File.Exists(exeAtRoot))
            return extractDir;

        foreach (string dir in Directory.GetDirectories(extractDir))
        {
            if (File.Exists(Path.Combine(dir, "HunterPie.exe")))
                return dir;
        }

        throw new InvalidOperationException("更新包内找不到 HunterPie.exe，资源可能损坏。");
    }

    private static string? FindUpdaterScript()
    {
        string local = Path.Combine(ClientInfo.ClientPath, "rise-overlay-updater.ps1");
        if (File.Exists(local))
            return local;

        string sibling = Path.Combine(ClientInfo.ClientPath, "Scripts", "rise-overlay-updater.ps1");
        return File.Exists(sibling) ? sibling : null;
    }

    internal static string NormalizeVersion(string tagOrVersion)
    {
        string s = tagOrVersion.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s[1..];
        return s;
    }

    internal static bool IsRemoteNewer(string local, string remote)
    {
        if (!Version.TryParse(PadVersion(local), out Version? localVer))
            localVer = new Version(0, 0, 0, 0);
        if (!Version.TryParse(PadVersion(remote), out Version? remoteVer))
            return false;
        return remoteVer > localVer;
    }

    private static string PadVersion(string v)
    {
        string[] parts = NormalizeVersion(v).Split('.');
        while (parts.Length < 4)
            Array.Resize(ref parts, parts.Length + 1);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
                parts[i] = "0";
        }

        return string.Join('.', parts.Take(4));
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RiseOverlay", GetLocalVersionString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
