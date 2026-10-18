param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

# Runs dotnet with caches redirected into the repository.
# This keeps the user profile clean and avoids sandbox write denials.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:NUGET_PACKAGES = Join-Path $root '.nuget\packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

& dotnet @Arguments
exit $LASTEXITCODE
