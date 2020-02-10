[CmdletBinding()]
param
(
    [Parameter(Mandatory=$true)] [string] $Path
)

dotnet run --project "$PSScriptRoot\src\SuppressionCleanupTool" --configuration Release -- $Path
