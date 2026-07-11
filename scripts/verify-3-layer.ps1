$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-NoMatch([string]$label, [string]$pattern, [string[]]$paths, [string[]]$globs) {
    $arguments = @('-n', $pattern) + $paths
    foreach ($glob in $globs) { $arguments += @('-g', $glob) }
    $output = & rg @arguments 2>$null
    if ($LASTEXITCODE -eq 0) { throw "$label`n$output" }
    if ($LASTEXITCODE -gt 1) { throw "Architecture search failed: $label" }
}

Push-Location $root
try {
    Assert-NoMatch 'Web must not reference DAL namespaces.' 'PRN222_FINAL\.DAL' @('Web') @('*.cs','*.cshtml','*.csproj')
    Assert-NoMatch 'DAL must not reference BLL or Web.' 'PRN222_FINAL\.(BLL|Web)' @('DAL') @('*.cs','*.csproj')
    Assert-NoMatch 'BLL must not reference Web.' 'PRN222_FINAL\.Web' @('BLL') @('*.cs','*.csproj')
    Assert-NoMatch 'Web/BLL must not own database, SMTP, filesystem, or HTTP transports.' '(Npgsql|SmtpClient|HttpClient|System\.IO\.File|File\.(Create|Read|Write|Delete|Move|Exists))' @('Web','BLL') @('*.cs')

    $webProject = Get-Content -Raw 'Web/Web.csproj'
    $bllProject = Get-Content -Raw 'BLL/BLL.csproj'
    $dalProject = Get-Content -Raw 'DAL/DAL.csproj'
    if ($webProject -notmatch 'BLL\\BLL\.csproj' -or $webProject -match 'DAL\\DAL\.csproj') { throw 'Web project dependency must be Web -> BLL only.' }
    if ($bllProject -notmatch 'DAL\\DAL\.csproj') { throw 'BLL project must reference DAL.' }
    if ($dalProject -match '<ProjectReference') { throw 'DAL must not reference another application layer.' }
    Write-Output '3-layer architecture verification passed.'
}
finally { Pop-Location }
