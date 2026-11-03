[CmdletBinding()]
param([Parameter(Mandatory)][string]$CatalogPath)
$ErrorActionPreference = 'Stop'
$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
$root = 'https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en'
$statuses = (Invoke-WebRequest -Uri "$root/Status.csv").Content | ConvertFrom-Csv
$duties = (Invoke-WebRequest -Uri "$root/ContentFinderCondition.csv").Content | ConvertFrom-Csv
foreach ($buff in $catalog.Buffs) {
    $eligible = @($statuses | Where-Object {
        $_.Name -ieq $buff.EnglishName -and $_.StatusCategory -eq '1' -and $_.CanStatusOff -eq 'True'
    })
    if ($eligible.Count -eq 0) { throw "No cancellable positive Global status for $($buff.EnglishName)." }
    Write-Output ("Validated cancellable raid buff: " + $buff.EnglishName + " [" + ($eligible.'#' -join ', ') + "]")
}
foreach ($duty in $catalog.Ultimates) {
    $match = @($duties | Where-Object {
        $_.'#' -eq [string]$duty.DutyId -and $_.TerritoryType -eq [string]$duty.TerritoryId -and
        $_.ContentType -eq '28' -and $_.Name -ieq $duty.EnglishName
    })
    if ($match.Count -ne 1) { throw "Global Ultimate mapping mismatch: $($duty.Code)." }
    Write-Output ("Validated Ultimate: " + $duty.Code + " / Territory " + $duty.TerritoryId)
}
Write-Output 'Global game-data catalogue validation passed.'
