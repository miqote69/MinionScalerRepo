param(
    [Parameter(Mandatory = $true)]
    [string] $Owner,

    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$downloadUrl = "https://github.com/$Owner/$Repository/releases/download/$Tag/MinionScaler.zip"
$repoUrl = "https://github.com/$Owner/$Repository"
$lastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$entry = [ordered]@{
    Author = $Owner
    Name = "Minion Scaler"
    Description = "Adjusts the local display scale of minions. Use /minionscaler to configure."
    InternalName = "MinionScaler"
    AssemblyVersion = $Version
    TestingAssemblyVersion = $null
    RepoUrl = $repoUrl
    ApplicableVersion = "any"
    DalamudApiLevel = 15
    Punchline = "Changes visible minion size locally."
    Tags = @(
        "minion",
        "companion",
        "cosmetic"
    )
    MinimumDalamudVersion = "15.0.0.0"
    IsHide = $false
    IsTestingExclusive = $false
    IconUrl = "https://raw.githubusercontent.com/$Owner/$Repository/main/Assets/icon-v3.png"
    DownloadLinkInstall = $downloadUrl
    DownloadLinkTesting = $downloadUrl
    DownloadLinkUpdate = $downloadUrl
    LastUpdate = $lastUpdate
}

$json = ConvertTo-Json -InputObject @($entry) -Depth 5
$absoluteOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.File]::WriteAllText($absoluteOutputPath, $json, [System.Text.UTF8Encoding]::new($false))
