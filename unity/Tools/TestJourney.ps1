$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyForest.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
    ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }
Add-Type -Path $sources

function Assert($condition, $message) {
    if (!$condition) { throw $message }
}

function Step($game, $seconds, $voice) {
    $count = [Math]::Max(0, [math]::Ceiling($seconds * 50))
    for ($i = 0; $i -lt $count; $i++) { $game.Step(.02, $voice) }
}

function Pose($game, $hand, $seconds, $voice) {
    $game.SetHand([Echoes.Hand]$hand)
    Step $game $seconds $voice
}

function FinishMainGate($game, $voice) {
    Pose $game Open .06 0
    Pose $game Fist 1.01 $voice
    Assert ($game.Phase -eq 'RuinRelease') 'The correct grip and sound should open the first gate.'
    Step $game ($game.Beat - $game.Age) 0
    Pose $game Open .04 0
    Assert ($game.Phase -eq 'RuinMisread') 'A timed open hand should reach the authored misread.'
}

function Grant($game) {
    [void]$game.Touch(0)
    [void]$game.Touch(1)
    Assert ($game.Phase -eq 'StarsDelegated') 'A shown suggestion should be delegatable.'
}

function Steer($game, $lane) {
    while ($game.ForestTargetLane -ne $lane) {
        $button = [int]($game.ForestTargetLane -lt $lane)
        Assert ($game.Touch($button)) 'An available forest route should accept the requested lane.'
    }
}

function PlayForest($game, $upperExit) {
    $riseForExit = $false
    for ($i = 0; $i -lt 6500 -and $game.InForest; $i++) {
        $game.SetHand([Echoes.Hand]::Open)
        if ($game.Phase -eq 'ForestTrapped' -or $game.Phase -eq 'ForestTransition') {
            $game.Step(.02, 0)
            continue
        }

        $site = $game.ForestFeatures | Where-Object { $_.Kind -eq 'Plant' -and !$_.Collected } | Select-Object -First 1
        $lane = $site.Lane
        $voice = 0
        $takeOff = !$game.ForestAirborne
        if ($game.Phase -eq 'ForestPlanting') {
            $aligned = [math]::Abs($game.ForestLane - $lane) -lt .25
            $highSite = $game.Plants -eq 2 -and $upperExit
            $takeOff = !$game.ForestAirborne -and (!$aligned -or ($highSite -and $game.ForestLights -gt 0))
            if ($highSite -and $takeOff -and $aligned) { $riseForExit = $true }
            if ($aligned -and ($riseForExit -or (!$highSite -and !$game.ForestAirborne))) { $voice = .3 }
        } else {
            $obstacle = $game.ForestFeatures | Where-Object {
                !$_.Collected -and $_.Kind -in @('Scarlet','Web','Maw') -and
                    $_.Depth -ge ($game.Distance - 1.08) -and $_.Depth -lt ($game.Distance + 5.3)
            } | Select-Object -First 1
            if ($obstacle -and $obstacle.Lane -eq $lane) { $lane = if ($lane -eq 1) { 0 } else { $lane + 1 } }
            $voice = .3
        }

        Steer $game $lane
        if ($takeOff) {
            $lift = if ($riseForExit) { .7 } else { .3 }
            Pose $game Fist .47 $lift
            $game.SetHand([Echoes.Hand]::Open)
        }
        $game.Step(.02, $voice)
    }
    Assert ($game.Chapter -eq 1 -and $game.Phase -eq 'RuinGather' -and $game.Plants -eq 3) 'All roots should lead to the existing ruins.'
    Assert ($game.ForestRoute -eq $(if ($upperExit) { 2 } else { 1 })) 'Planting altitude should select the matching web or flower descent.'
}

$game=[Echoes.Journey]::new(100)
Pose $game Fist 1 .3
Assert ($game.Time -eq 0) 'A returning hand must open to start.'
Pose $game Open .55 0
Pose $game Fist .57 0
Pose $game Open .05 0
Assert ($game.Lesson -eq 1) 'The first lesson should advance after seeing hold and release.'
Pose $game Open .74 .3
Assert ($game.Lesson -eq 1) 'Soft sound alone should not fill the second sound mark.'
Step $game .74 .7
Assert ($game.Lesson -eq 2) 'Hear both voice ranges before the tactile trial.'
[void]$game.Touch(0)
Assert ($game.HintReady -and $game.Cue -eq 'Observe') 'A should show where the help would land.'
[void]$game.Touch(1)
Assert ($game.Cue -eq 'Assist') 'B should then demonstrate a single action.'
Step $game 1.5 0
Assert ($game.Phase -eq 'Prologue' -and !$game.Touch(0)) 'The stone stair should lead into the opening.'
$game.Step(.5, .7, $false)
Assert ($game.OpeningTime -gt 0) 'The opening should advance independently of a missing sensor.'
Step $game 38 0
Assert ($game.Phase -eq 'ForestExplore' -and $game.EnteredFromOpening) 'The opening should land in the first chapter.'
Pose $game Open .36 .7
Steer $game -1
Step $game .8 .7
Assert ($game.Distance -eq 0 -and $game.ForestLane -eq 0 -and $game.ForestTargetLane -eq -1) 'The ground should accept lane planning without locomotion.'
Steer $game 0
Pose $game Fist .48 .3
Pose $game Open .56 .3
Assert ($game.Distance -gt 2 -and $game.ForestLights -eq 1 -and $game.Cue -eq 'ForestAmber') 'An airborne player should collect the first warm fly and emit the yellow cue.'
$depth = $game.Distance
$game.SetHand([Echoes.Hand]::Missing)
Step $game 2 .7
Assert ($game.Distance -eq $depth -and $game.ForestLights -eq 1) 'Losing a hand pauses movement and preserves earned light.'
$frame=[Echoes.SpiritFrame]::Read($game, 1)
Assert ($frame.cast -eq 1 -and $frame.forest -and $frame.forestAirborne) 'The projection should know the chosen cast and airborne forest state.'
PlayForest $game $false
FinishMainGate $game .3
[void]$game.Touch(0)
Assert ($game.Phase -eq 'RuinRepair') 'A at the misread presents the two hand traces and enters correction.'
Pose $game Fist .22 .5
Pose $game Open .05 0
Assert ($game.Phase -eq 'RuinRepair') 'The correction needs its silence and a new release.'
Step $game .72 0
Pose $game Fist .54 0
Pose $game Open .06 0
Assert ($game.Phase -eq 'RuinCrossing' -and $game.RuinResult -eq 'Repaired') 'A quiet held release should correct the record.'
Step $game 4.6 0
Assert ($game.Phase -eq 'Complete') 'The aligned stone bridge should advance.'
[void]$game.Touch(1)
Pose $game Open .1 0
Grant $game
[void]$game.Touch(1)
Assert ($game.Phase -eq 'StarsRoute' -and $game.StarCount -eq 0 -and $game.Withdrawals -eq 1) 'Withdrawal should leave the segment available.'
Grant $game
Step $game 1.59 0
Assert ($game.StarCount -eq 1 -and $game.PathAt(0) -eq 'Delegated') 'Delegation should commit one segment.'
Grant $game
Step $game 1.59 0
Assert ($game.StarCount -eq 1 -and $game.Mistakes -eq 2 -and !$game.RiskySuggestion) 'The red proposal leaves a trace and can be corrected.'
Pose $game Fist .54 .25
Pose $game Open 1.16 0
if ($game.StarCount -eq 1) { Step $game 2.1 0 }
Assert ($game.StarCount -eq 2 -and $game.PathAt(1) -eq 'Corrected') 'A fitted manual flight corrects the risky second segment.'
Grant $game
Step $game 1.6 0
Assert ($game.Phase -eq 'StarsSeal' -and $game.StarCount -eq 3) 'Three committed nodes should land on the manual closing gesture.'
[void]$game.Touch(1)
Assert ($game.Phase -eq 'StarsSeal') 'B must not finish the final personal action.'
Pose $game Fist .56 0
Pose $game Open .06 0
Assert ($game.Phase -eq 'Complete') 'A final authored release should close the constellation.'
Assert ($game.Touch(0) -and $game.IsObserving) 'A should revisit the completed constellation.'
Assert ($game.Touch(1) -and $game.Cue -eq 'Seal') 'B should replay the final echo without changing the route.'
$saved=[Echoes.Journey]::new()
Assert ($saved.Restore($game.Checkpoint)) 'An earned checkpoint should load.'
Assert ($saved.Phase -eq 'Complete' -and $saved.Plants -eq 3 -and $saved.RuinResult -eq 'Repaired' -and
    $saved.StarCount -eq 3 -and $saved.Withdrawals -eq 1 -and $saved.Mistakes -eq 2) 'Restore the completed route and consequences.'

$canopy=[Echoes.Journey]::new(100)
$canopy.SelectChapter(0)
PlayForest $canopy $true

function FlyFlat($game, $depth, $height, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    Steer $game $lane
    while ($game.Distance -lt $depth -and $game.Phase -eq 'ForestExplore') {
        $game.Flight.Reset($height)
        $game.Step(.02, .3)
    }
}

$quiet=[Echoes.Journey]::new(100)
$loud=[Echoes.Journey]::new(100)
foreach ($flightTrial in @($quiet, $loud)) {
    $flightTrial.SelectChapter(0)
    Pose $flightTrial Open .05 0
    Pose $flightTrial Fist .48 .3
    $flightTrial.SetHand([Echoes.Hand]::Open)
}
Step $quiet .35 0
Step $loud .35 .7
Assert ($loud.Distance -gt $quiet.Distance + .8) 'Voice alone should noticeably accelerate an otherwise identical airborne path.'

$caught=[Echoes.Journey]::new(100)
$caught.SelectChapter(0)
$amber=$caught.ForestFeatures | Where-Object Kind -eq Amber | Select-Object -First 1
FlyFlat $caught $amber.Depth $amber.Altitude $amber.Lane
$scarlet=$caught.ForestFeatures | Where-Object Kind -eq Scarlet | Select-Object -First 1
FlyFlat $caught ($scarlet.Depth - 4) .10 $scarlet.Lane
FlyFlat $caught ($scarlet.Depth + .5) $scarlet.Altitude $scarlet.Lane
Assert ($caught.Cue -eq 'ForestScarlet' -and $caught.FireflyResponse -eq 'Scarlet') 'A red fly contact should independently emit the red cue.'
$maw=$caught.ForestFeatures | Where-Object Kind -eq Maw | Select-Object -First 1
FlyFlat $caught ($maw.Depth + .4) .08 $maw.Lane
$reserve=$caught.ForestLights
Assert ($caught.ForestTrap -eq 'Maw' -and $caught.Phase -eq 'ForestTrapped' -and $caught.ForestHasRescue) 'A low contact must use the flower snare with a reserved rescue.'
Step $caught 2.5 0
Assert ($caught.Phase -eq 'ForestExplore' -and $caught.ForestLights -eq ($reserve - 1) -and $caught.Cue -eq 'ForestRescued') 'The rescue should consume exactly one collected fly and resume the route.'

$empty=[Echoes.Journey]::new(100)
$empty.SelectChapter(0)
$seed=$empty.Checkpoint.forestSeed
$web=$empty.ForestFeatures | Where-Object Kind -eq Web | Select-Object -First 1
FlyFlat $empty ($web.Depth - 2.1) .10 $web.Lane
FlyFlat $empty ($web.Depth + .4) .77 $web.Lane
Assert ($empty.ForestTrap -eq 'Web' -and $empty.ForestWindFactor -gt 0 -and !$empty.ForestHasRescue) 'A high contact should enter the web snare with louder wind and no phantom reserve.'
$empty.Retry()
Step $empty 1.5 0
Assert ($empty.Phase -eq 'ForestTrapped') 'The failed rescue must remain visible before restarting.'
Step $empty 2.0 0
Assert ($empty.Phase -eq 'ForestExplore' -and $empty.Distance -eq 0 -and $empty.Checkpoint.forestSeed -ne $seed) 'No reserve should restart with a rerolled forest.'

$renew=[Echoes.Journey]::new(100)
$renew.SelectChapter(0)
$root=$renew.ForestFeatures | Where-Object Kind -eq Plant | Select-Object -First 1
$groundThreat=$renew.ForestFeatures | Where-Object Kind -eq Maw | Select-Object -First 1
$outOfReach = if ($groundThreat.Lane -eq 1) { 0 } else { 1 }
FlyFlat $renew $root.Depth .02 $outOfReach
Assert ($renew.Phase -eq 'ForestPlanting' -and $renew.ForestLights -eq 0) 'A reserve-free approach should stop beside the first root.'
Steer $renew $root.Lane
for ($i = 0; $i -lt 30; $i++) {
    $renew.Flight.Reset(.10)
    $renew.Step(.02, 0)
}
Assert ($renew.ForestLights -eq 1 -and $renew.Cue -eq 'ForestAmber') 'A missed route should have an accessible low gold fly at the clearing.'
Step $renew .7 .3
Assert ($renew.Plants -eq 1) 'The recovered light should be usable at the root.'
$forestSave=$renew.Checkpoint
$resumed=[Echoes.Journey]::new(42)
Assert ($resumed.Restore($forestSave) -and $resumed.InForest -and $resumed.Plants -eq 1 -and
    $resumed.Distance -eq $root.Depth -and $resumed.ForestLights -eq 0 -and
    $resumed.Checkpoint.forestSeed -eq $forestSave.forestSeed) 'Restore must replay the same planted clearing with the integer light reserve.'

$arcade=[Echoes.Journey]::new()
$arcade.SelectChapter(1)
FinishMainGate $arcade .71
Assert ($arcade.FastEcho) 'Full sound selects the shorter echo window.'
Pose $arcade Fist .71 0
Assert ($arcade.Phase -eq 'RuinSide') 'A sustained wrong response should open the alternate arcade.'
Pose $arcade Open .05 0
Pose $arcade Fist .35 0
Pose $arcade Open .05 0
Assert ($arcade.ReflectionIndex -eq 0) 'Releasing early should retry the current reflection.'
for ($i = 0; $i -lt 3; $i++) {
    Pose $arcade Fist ($arcade.Beat - $arcade.Age) 0
    Pose $arcade Open .06 0
}
Assert ($arcade.RuinResult -eq 'Arcade' -and $arcade.Phase -eq 'RuinCrossing') 'The three differently timed reflections should go to the shared bridge.'

$single=[Echoes.Journey]::new()
$single.SelectChapter(1)
Pose $single Open .12 0
[void]$single.Touch(0)
[void]$single.Touch(1)
Assert ($single.Phase -eq 'RuinRelease' -and $single.RuinResult -eq 'Unfinished') 'Borrowing the first gate must leave the second one active.'
Pose $single Fist .35 0
Pose $single Open .05 0
Assert ($single.Phase -eq 'RuinRelease' -and $single.TimingWindow -gt .6) 'The wide catch-up beat must preserve the first gate.'

$flight=[Echoes.SpiritFlight]::new()
$flight.Reset(.11)
$flight.Release(.3)
for ($i=0; $i -lt 18; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Height -gt .36 -and $flight.Speed -gt 0) 'Release should rise visibly.'
for ($i=0; $i -lt 72; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Speed -lt -.1 -and $flight.Glide -gt .6) 'Opening a hand allows a slow glide.'
for ($i=0; $i -lt 400; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Height -eq 0 -and [Echoes.SpiritFlight]::ScreenY($flight.Height) -gt .77) 'The flight settles near the bottom of the screen.'

$dancer=[Echoes.Journey]::new()
Pose $dancer Open .62 0
Pose $dancer Fist .71 .7
$dancer.SetHand([Echoes.Hand]::Open)
Assert (([Echoes.SpiritFrame]::Read($dancer)).turnAge -eq 0) 'A charged release starts one full turn.'
$release=$dancer.LastRelease
Pose $dancer Fist .51 .7
Pose $dancer Missing .27 .7
Assert ($dancer.LastRelease -eq $release -and $dancer.Held -eq 0) 'Signal loss must not release the stored charge.'

$touch = $enabled = 0
Assert ([Echoes.ExhibitProtocol]::ReadInputs('EOL3 IN 3 7', [ref]$touch, [ref]$enabled)) 'The serial input still accepts both contacts.'
Assert ($touch -eq 3 -and $enabled -eq 7) 'Both physical inputs remain distinct.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 1, .5, 200, $true) -eq "EOL3 IO 1 12 4 0 0 2 6 -1 166`n") 'Both lamps and bounded fan share the output packet.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 0, 0, 200, $false) -eq "EOL3 SET 1 0 0 0 0 0 0 -1`n") 'Lighting-only firmware still has a supported output.'
Assert ([Echoes.ExhibitProtocol]::Output(1, .5, .5, 0, $true, $true) -eq "EOL3 IO 1 6 4 0 6 0 0 -1 0`n") 'Forest EOL3 palette routes the fireflies to dim warm yellow and red lamps.'
Write-Output 'Passed the full Unity route, both forest descents, forest collect/avoid/rescue/restart, ruin correction/assistance, star withdrawal, checkpoints, flight, projection and EOL3.'
