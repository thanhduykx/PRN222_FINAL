$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $root '.chatbot-verify'

Push-Location $root
try {
    dotnet test 'PRN222_FINAL.sln' --no-restore `
        "-p:BaseOutputPath=$outputRoot\bin\" `
        '-p:TreatWarningsAsErrors=true'
    if ($LASTEXITCODE -ne 0) { throw 'Chatbot tests failed.' }

    dotnet build 'Web\Web.csproj' --no-restore `
        "-p:BaseOutputPath=$outputRoot\bin\" `
        '-p:TreatWarningsAsErrors=true'
    if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }

    & (Join-Path $PSScriptRoot 'verify-3-layer.ps1')

    Write-Output 'Chatbot release gates passed.'
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $outputRoot) {
        $resolvedOutput = (Resolve-Path -LiteralPath $outputRoot).Path
        $resolvedRoot = (Resolve-Path -LiteralPath $root).Path
        if (-not $resolvedOutput.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar)) {
            throw 'Verification output escaped the workspace.'
        }

        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
}
