$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyStars.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs',
    'JourneyRuins.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'RuinAtlas.cs', 'OpeningTiming.cs',
    'SpiritFlight.cs', 'FireflyCourse.cs', 'ExhibitProtocol.cs', 'ForestLampShow.cs' |
    ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }
Add-Type -Path $sources

function Assert($condition, $message) {
    if (!$condition) { throw $message }
}

function Reach-Firefly($journey, $depth, $altitude, $cue) {
    for ($i = 0; $i -lt 550 -and $journey.Distance -lt $depth -and $journey.Cue -ne $cue; $i++) {
        $journey.Flight.Reset($altitude, $journey.ForestHeightLimit)
        $journey.Step(.02, .3)
    }
    Assert ($journey.Cue -eq $cue) "The sprite reached the firefly and emitted $cue."
}

$journey = [Echoes.Journey]::new(100)
$journey.SelectChapter(0)
$journey.SetHand([Echoes.Hand]::Open)
$show = [Echoes.ForestLampShow]::new()
Assert (!$show.Sample($journey, 0).Lit) 'The forest is dark before an interaction.'
Reach-Firefly $journey 4 .42 ForestAmber
$now = $journey.PoseTime
[void]$show.Sample($journey, $now)
$amber = $show.Sample($journey, $now + .12)
Assert ($amber.Red -gt $amber.Green -and $amber.Green -gt 0 -and $amber.Blue -eq 0) 'Amber contact produces a warm yellow pulse.'
Assert ([Echoes.ExhibitProtocol]::Output(31, $amber, $amber, 0, $false) -eq "EOL3 SET 31 12 8 0 12 8 0 -1`n") 'Both lamps receive the same yellow with the connected board limit.'
Assert (!$show.Sample($journey, $now + 1).Lit) 'A firefly pulse fades out.'

$fall = [Echoes.Journey]::new(100)
$fall.SelectChapter(0)
$checkpoint = $fall.Checkpoint
$checkpoint.forestReserve = 1
Assert ($fall.Restore($checkpoint)) 'Start the failure case with the last companion.'
$fall.SetHand([Echoes.Hand]::Open)
$fall.SetForestLateralInput($false, $true)
for ($i = 0; $i -lt 85; $i++) { $fall.Step(.02, 0) }
$fall.SetForestLateralInput($false, $false)
Reach-Firefly $fall 26 .43 ForestScarlet
Assert ($fall.Phase -eq 'ForestFalling') 'The last red collision starts the swallow sequence.'
$show.Reset()
$now = $fall.PoseTime
[void]$show.Sample($fall, $now)
$red = $show.Sample($fall, $now + .12)
Assert ($red.Red -ge 12 -and $red.Green -le 1 -and $red.Blue -eq 0) 'Red contact emits a red pulse.'
Assert ([Echoes.ExhibitProtocol]::Output(32, $red, $red, 0, $false) -eq "EOL3 SET 32 12 0 0 12 0 0 -1`n") 'Both lamps receive red together.'
while (!$fall.ForestSwallowing -and $fall.Phase -eq 'ForestFalling') {
    $fall.Step(.02, 0)
    [void]$show.Sample($fall, $fall.PoseTime)
}
Assert $fall.ForestSwallowing 'Swallowing is signalled at the actual animation beat.'
$now = $fall.PoseTime
Assert ($show.Sample($fall, $now + .13).Red -gt 0 -and !$show.Sample($fall, $now + .365).Lit -and
    $show.Sample($fall, $now + .50).Red -gt 0 -and !$show.Sample($fall, $now + .90).Lit) 'Swallowing produces two soft red flashes once.'

$plant = [Echoes.Journey]::new(100)
$plant.SelectChapter(0)
$checkpoint = $plant.Checkpoint
$checkpoint.forestBranch = 2
$checkpoint.forestDepth = 275
Assert ($plant.Restore($checkpoint) -and $plant.Phase -eq 'ForestPlanting') 'Start at the heart planting station.'
$plant.SetHand([Echoes.Hand]::Open)
$show.Reset()
$plant.Flight.Reset(.05, $plant.ForestHeightLimit)
$plant.Step(.02, 0)
Assert (!$show.Sample($plant, $plant.PoseTime).Lit) 'An aligned but silent player does not start the light show.'
$seen = [Collections.Generic.HashSet[string]]::new()
for ($i = 0; $i -lt 65; $i++) {
    if ($plant.Phase -eq 'ForestPlanting') { $plant.Flight.Reset(.05, $plant.ForestHeightLimit) }
    $plant.Step(.02, .35)
    $rgb = $show.Sample($plant, $plant.PoseTime)
    if (!$rgb.Lit) { continue }
    if ($rgb.Red -gt $rgb.Blue -and $rgb.Green -gt $rgb.Blue) { [void]$seen.Add('amber') }
    if ($rgb.Green -gt $rgb.Red -and $rgb.Green -gt $rgb.Blue) { [void]$seen.Add('mint') }
    if ($rgb.Blue -gt $rgb.Green -and $rgb.Green -gt $rgb.Red) { [void]$seen.Add('blue') }
    if ($rgb.Blue -gt $rgb.Red -and $rgb.Red -gt $rgb.Green) { [void]$seen.Add('violet') }
    if ($rgb.Red -gt $rgb.Blue -and $rgb.Blue -gt $rgb.Green) { [void]$seen.Add('rose') }
}
Assert ($plant.Plants -eq 1 -and $seen.Count -eq 5) 'Charging and completing planting displays five colors.'
Assert (!$show.Sample($plant, $plant.PoseTime + 1).Lit) 'The planting finish fades before the later swallow.'

$packet = [Echoes.LampRgb]::new(4, 18, 25)
Assert ([Echoes.ExhibitProtocol]::Output(33, $packet, $packet, 132, $false) -eq "EOL3 SET 33 2 9 12 2 9 12 -1`n") 'Bench firmware gets a color-preserving cap and no fan field.'
Assert ([Echoes.ExhibitProtocol]::Output(33, $packet, $packet, 132, $true) -eq "EOL3 IO 33 4 18 25 4 18 25 -1 132`n") 'Combined firmware uses the full dim palette and fan field.'
$seq = 0
Assert ([Echoes.ExhibitProtocol]::ReadAck('EOL3 ACK 65535', [ref]$seq) -and $seq -eq 65535) 'Outputs can be matched to their 16-bit ACK sequence.'
Assert (![Echoes.ExhibitProtocol]::ReadAck('EOL3 ACK 65536', [ref]$seq)) 'An out-of-range response cannot confirm a test.'
Write-Output 'Passed real forest contacts, swallow beats, planting colors, both-light packets, board limits and acknowledgements.'
