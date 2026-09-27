param(
    [Parameter(Mandatory=$true)][string]$Unity,
    [switch]$Bake,
    [switch]$Realism2026
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'unity'
$receipt = Join-Path $PSScriptRoot ('local/build-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
New-Item -ItemType Directory -Path (Split-Path -Parent $receipt) -Force | Out-Null
$env:LOWER_BAY_BAKE = if ($Bake) { '1' } else { '0' }
$method = if ($Realism2026) { 'StrikeMapStudio.Editor.LowerBayReviewBuilder.Build2026Batch' } else { 'StrikeMapStudio.Editor.LowerBayReviewBuilder.BuildBatch' }
$arguments = @('-batchmode', '-noUpm', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', $method, '-logFile', ('"' + $receipt + '"'))
$job = Start-Process -FilePath $Unity -ArgumentList $arguments -WorkingDirectory $project -PassThru -WindowStyle Hidden
$result = if ($Realism2026) { Join-Path $PSScriptRoot '../reimagine-2026/local/build/build.json' } else { Join-Path $PSScriptRoot 'local/build/build.json' }
[pscustomobject]@{pid=$job.Id;log=$receipt;receipt=$result} | ConvertTo-Json
