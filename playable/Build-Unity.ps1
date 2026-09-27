param(
    [Parameter(Mandatory=$true)][string]$Unity,
    [switch]$Bake
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'unity'
$receipt = Join-Path $PSScriptRoot ('local/build-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
New-Item -ItemType Directory -Path (Split-Path -Parent $receipt) -Force | Out-Null
$env:LOWER_BAY_BAKE = if ($Bake) { '1' } else { '0' }
$arguments = @('-batchmode', '-noUpm', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'StrikeMapStudio.Editor.LowerBayReviewBuilder.BuildBatch', '-logFile', ('"' + $receipt + '"'))
$job = Start-Process -FilePath $Unity -ArgumentList $arguments -WorkingDirectory $project -PassThru -WindowStyle Hidden
[pscustomobject]@{pid=$job.Id;log=$receipt;receipt=(Join-Path $PSScriptRoot 'local/build/build.json')} | ConvertTo-Json
