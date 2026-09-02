using System.Text.Json.Serialization;

namespace RiseOverlay.UI.Update;

internal sealed class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAssetDto> Assets { get; set; } = [];
}

internal sealed class GitHubAssetDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public sealed record RiseUpdateCheckResult(
    bool HasUpdate,
    string LocalVersion,
    string? RemoteVersion,
    string? DownloadUrl,
    string? ReleaseNotes,
    string? ReleasePageUrl,
    string? ErrorMessage);
