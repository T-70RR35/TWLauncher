# TW Launcher - gera instalador e pacotes Velopack no Windows 10/11 x64.
# Exemplo local: .\Publicar-TWLauncher.ps1 -Version 0.6.13 -RepositoryUrl https://github.com/USUARIO/REPOSITORIO
# Requer .NET 10 SDK e: dotnet tool install --global vpk --version 1.2.161
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://github\.com/[^/\s]+/[^/\s]+/?$')]
    [string]$RepositoryUrl,

    [switch]$Upload,
    [string]$GitHubToken = '',
    [string]$ReleaseTag = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$publish = Join-Path $root '.publish\win-x64'
$releases = Join-Path $root 'Releases'
$project = Join-Path $root 'TVLauncher.App\TVLauncher.App.csproj'
$icon = Join-Path $root 'TVLauncher.App\Assets\TWLauncher.ico'
$releaseNotes = Join-Path $root 'CHANGELOG.md'
$repositoryUrl = $RepositoryUrl.TrimEnd('/')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instale o SDK do .NET 10 (x64) para compilar o TW Launcher.'
}
$vpkTool = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpkTool) {
    $vpkPath = Join-Path $env:USERPROFILE '.dotnet\tools\vpk.exe'
    if (-not (Test-Path $vpkPath)) { throw 'Instale o Velopack CLI: dotnet tool install --global vpk --version 1.2.161' }
} else { $vpkPath = $vpkTool.Source }

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
New-Item -ItemType Directory -Path $publish -Force | Out-Null
New-Item -ItemType Directory -Path $releases -Force | Out-Null

Write-Host "Publicando TW Launcher v$Version para Windows x64..."
& dotnet publish $project -c Release -r win-x64 -f net10.0-windows10.0.17763.0 `
    --self-contained true -o $publish -p:Version=$Version -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'Falha no dotnet publish.' }

# O link público é embutido SOMENTE no aplicativo publicado; não precisa editar código.
@{ repositoryUrl = $repositoryUrl } | ConvertTo-Json -Depth 3 |
    Set-Content -Path (Join-Path $publish 'update-source.json') -Encoding utf8

# Delta: importa pacotes anteriores, caso já exista uma versão publicada.
Write-Host 'Consultando a versão anterior para gerar atualização diferencial, se existir...'
$downloadArgs = @('download', 'github', '--repoUrl', $repositoryUrl, '--outputDir', $releases, '--channel', 'win')
if ($GitHubToken) { $downloadArgs += @('--token', $GitHubToken) }
& $vpkPath @downloadArgs
if ($LASTEXITCODE -ne 0) { Write-Host 'Nenhuma versão anterior encontrada ou consulta indisponível; gerando pacote completo.' }

Write-Host 'Gerando instalador (Setup.exe) e arquivos de atualização...'
& $vpkPath pack `
    --packId TWLauncher.Desktop `
    --packVersion $Version `
    --packDir $publish `
    --mainExe TWLauncher.exe `
    --packTitle 'TW Launcher' `
    --packAuthors 'TW' `
    --icon $icon `
    --framework webview2 `
    --channel win `
    --outputDir $releases `
    --releaseNotes $releaseNotes
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar instalador Velopack.' }

if ($Upload) {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) { throw 'O upload precisa de um token (GitHub Actions GITHUB_TOKEN ou PAT com contents:write).' }
    if (-not $ReleaseTag) { $ReleaseTag = "v$Version" }
    Write-Host "Publicando $ReleaseTag no GitHub Releases..."
    & $vpkPath upload github `
        --repoUrl $repositoryUrl `
        --outputDir $releases `
        --channel win `
        --token $GitHubToken `
        --tag $ReleaseTag `
        --releaseName "TW Launcher $Version" `
        --publish
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar release no GitHub.' }
}
Write-Host "Concluído. Instalador e pacotes em: $releases"
