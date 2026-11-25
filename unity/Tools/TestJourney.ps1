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

function FlyFlat($game, $depth, $height, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    Steer $game $lane
    for ($i = 0; $i -lt 1800 -and $game.Distance -lt $depth -and $game.Phase -eq 'ForestExplore'; $i++) {
        $game.Flight.Reset($height, $game.ForestHeightLimit)
        $game.Step(.02, .3)
    }
}

function EnterLeft($game) {
    FlyFlat $game 112 .76 -1
    Assert ($game.Branch -eq 'Left' -and $game.Section -eq 'DoubleRoots' -and $game.ForestCenter -eq 0) 'The shared route can commit to the maze without a camera jump.'
    FlyFlat $game 130 .39 -1
    Assert ($game.Section -eq 'EchoWell') 'The low left root arch leads into the well.'
    FlyFlat $game 144 .49 1
    Assert ($game.Section -eq 'BrokenBridges') 'The well route requires the right upper opening.'
    FlyFlat $game 159 .51 -1
    Assert ($game.Section -eq 'Canopy') 'The left raised bridge reaches the canopy.'
    FlyFlat $game 174 .39 1
    Assert ($game.Section -eq 'Clearing') 'Gliding below the right branches reaches the clearing.'
    FlyFlat $game 188 .38 0
    Assert ($game.Phase -eq 'ForestPlanting' -and $game.ForestPlantsRequired -eq 2) 'The maze ends at both roots.'
}

function PlantRoot($game, $lane, $height, $voice = .30) {
    Steer $game $lane
    for ($i = 0; $i -lt 65 -and $game.Phase -eq 'ForestPlanting'; $i++) {
        $game.Flight.Reset($height, $game.ForestHeightLimit)
        $game.Step(.02, $voice)
    }
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
Assert ($game.ForestLights -eq 3) 'The forest begins with three companion lights.'
Steer $game -1
Step $game .10 .7
Assert ($game.ForestLane -lt -.15 -and $game.ForestLane -gt -.85) 'Ground motion eases through intermediate positions.'
Steer $game 1
Step $game .02 .7
Assert ($game.ForestLane -lt 0) 'A steering reversal keeps position and momentum.'
Step $game .8 .7
Assert ($game.Distance -eq 0 -and $game.ForestLane -eq 1 -and $game.ForestTargetLane -eq 1) 'The ground accepts lateral movement without advancing.'
Steer $game 0
Pose $game Fist .48 .3
Pose $game Open .65 .3
Assert ($game.Distance -gt 2 -and $game.ForestLights -eq 4 -and $game.Cue -eq 'ForestAmber') 'The first air collection joins the three starter lights.'
$depth = $game.Distance
$game.SetHand([Echoes.Hand]::Missing)
Step $game 2 .7
Assert ($game.Distance -eq $depth -and $game.ForestLights -eq 4) 'Losing a hand pauses movement and preserves earned light.'
$frame=[Echoes.SpiritFrame]::Read($game, 1)
Assert ($frame.cast -eq 1 -and $frame.forest -and $frame.forestAirborne -and $frame.forestReserve -eq 4) 'The projection knows cast, flight state and the new orbit reserve.'
EnterLeft $game
PlantRoot $game 1 .63 0
Assert ($game.Plants -eq 0) 'Quiet hovering does not plant.'
PlantRoot $game 1 .63
Assert ($game.Plants -eq 1 -and $game.Phase -eq 'ForestPlanting') 'One root alone cannot finish the maze.'
PlantRoot $game -1 .055
Assert ($game.Plants -eq 2 -and $game.ForestTrap -eq 'Maw') 'Planting the low root second selects the flower ending.'
$plantedLeft = [Echoes.Journey]::new(25)
Assert ($plantedLeft.Restore($game.Checkpoint) -and $plantedLeft.ForestTrap -eq 'Maw' -and $plantedLeft.ForestLane -eq -1) 'The completed meadow checkpoint replays its last root and ending.'
Step $game 5.3 0
Assert ($game.Chapter -eq 1 -and $game.Phase -eq 'RuinGather') 'Both roots lead into the existing ruins.'
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
Assert ($saved.Phase -eq 'Complete' -and $saved.Plants -eq 2 -and $saved.RuinResult -eq 'Repaired' -and
    $saved.StarCount -eq 3 -and $saved.Withdrawals -eq 1 -and $saved.Mistakes -eq 2) 'Restore the completed route and consequences.'

$canopy=[Echoes.Journey]::new(100)
$canopy.SelectChapter(0)
FlyFlat $canopy 112 .76 -1
FlyFlat $canopy 130 .38 0
Assert ($canopy.Phase -eq 'ForestLoop') 'A wrong path begins a local maze loop.'
Step $canopy 2.3 0
Assert ($canopy.Distance -eq 118 -and $canopy.Branch -eq 'Left') 'A wrong turn circles back to the same maze.'

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
$reserve=$caught.ForestLights
FlyFlat $caught ($scarlet.Depth - 2) .70 $scarlet.Lane
FlyFlat $caught ($scarlet.Depth + .5) $scarlet.Altitude $scarlet.Lane
Assert ($caught.Cue -eq 'ForestScarlet' -and $caught.FireflyResponse -eq 'Scarlet' -and
    $caught.ForestLights -eq $reserve - 1 -and $caught.ForestContact -eq $scarlet) 'A red collision removes one fly and retains the contact anchor.'
$maw=$caught.ForestFeatures | Where-Object Kind -eq Maw | Select-Object -First 1
FlyFlat $caught ($maw.Depth + .4) .08 $maw.Lane
$reserve=$caught.ForestLights
Assert ($caught.ForestTrap -eq 'Maw' -and $caught.Phase -eq 'ForestTrapped' -and $caught.ForestHasRescue) 'A low contact must use the flower snare with a reserved rescue.'
Step $caught .74 0
Assert ($caught.ForestLights -eq $reserve - 1 -and $caught.Cue -eq 'ForestRescueLight') 'The rescue takes a companion as it departs.'
Step $caught 1.8 0
Assert ($caught.Phase -eq 'ForestExplore' -and $caught.ForestLights -eq ($reserve - 1) -and $caught.Cue -eq 'ForestRescued') 'The rescue should consume exactly one collected fly and resume the route.'

$empty=[Echoes.Journey]::new(100)
$empty.SelectChapter(0)
$last=$empty.Checkpoint
$last.forestReserve=1
Assert ($empty.Restore($last)) 'One saved companion can resume.'
$seed=$empty.Checkpoint.forestSeed
$web=$empty.ForestFeatures | Where-Object Kind -eq Web | Select-Object -First 1
FlyFlat $empty ($web.Depth - 6.5) .03 1
FlyFlat $empty ($web.Depth - 4.2) .79 $web.Lane
Assert ($web.Reveal -lt .1 -and $empty.Phase -eq 'ForestExplore') 'Low passage leaves a top web hidden.'
FlyFlat $empty ($web.Depth + .4) .91 $web.Lane
Assert ($empty.ForestTrap -eq 'Web' -and $empty.ForestWindFactor -gt 0 -and $empty.ForestHasRescue) 'A top approach reveals and triggers the canopy web.'
$empty.Retry()
Step $empty 2.48 0
Assert ($empty.Phase -eq 'ForestFalling' -and $empty.ForestLights -eq 0) 'A final rescue frees the actor and then begins the uncontrolled fall.'
Step $empty 3.3 0
Assert ($empty.Phase -eq 'ForestExplore' -and $empty.Distance -eq 0 -and $empty.ForestLights -eq 3 -and
    $empty.Checkpoint.forestSeed -ne $seed) 'Zero reserve restarts the forest with three lights and new hazard placements.'

$redOut=[Echoes.Journey]::new(100)
$redOut.SelectChapter(0)
$last=$redOut.Checkpoint
$last.forestReserve=1
Assert ($redOut.Restore($last)) 'A saved reserve can be exhausted in flight.'
$red=$redOut.ForestFeatures | Where-Object Kind -eq Scarlet | Select-Object -First 1
FlyFlat $redOut ($red.Depth - 3) .035 1
FlyFlat $redOut ($red.Depth - 1.5) .78 $red.Lane
FlyFlat $redOut ($red.Depth + .4) $red.Altitude $red.Lane
Assert ($redOut.Cue -eq 'ForestScarlet' -and $redOut.ForestLights -eq 0 -and $redOut.Phase -eq 'ForestFalling') 'A last red hit starts falling and still emits the red hardware cue.'

$renew=[Echoes.Journey]::new(100)
$renew.SelectChapter(0)
$clearing=$renew.Checkpoint
$clearing.forestBranch=1
$clearing.forestDepth=188
$clearing.forestReserve=1
$clearing.forestLane=-1
Assert ($renew.Restore($clearing) -and $renew.Phase -eq 'ForestPlanting') 'A clearing checkpoint restores its correct branch and station.'
Pose $renew Open .02 0
PlantRoot $renew -1 .055
Assert ($renew.Plants -eq 0 -and $renew.ForestLights -eq 1) 'The first root leaves the last flight light in reserve.'
PlantRoot $renew 0 .13 0
Assert ($renew.ForestLights -eq 2 -and $renew.Cue -eq 'ForestAmber') 'A needed light is reachable in the shelter of the clearing.'
PlantRoot $renew -1 .055
Assert ($renew.Plants -eq 1 -and $renew.ForestLights -eq 1) 'The low root can be planted first.'
$forestSave=$renew.Checkpoint
$resumed=[Echoes.Journey]::new(42)
Assert ($resumed.Restore($forestSave) -and $resumed.InForest -and $resumed.Plants -eq 1 -and
    $resumed.Distance -eq 188 -and $resumed.ForestLights -eq 1 -and
    $resumed.Checkpoint.forestSeed -eq $forestSave.forestSeed) 'Restore must replay the same planted clearing with the integer light reserve.'
Pose $resumed Open .01 0
Steer $resumed 1
Pose $resumed Fist .55 .7
Pose $resumed Open 1.7 .3
Assert ($resumed.ForestLights -eq 0 -and $resumed.Phase -eq 'ForestTransition' -and $resumed.ForestTrap -eq 'Web') 'The final upper root may spend the last companion and selects the web ending.'
Step $resumed 5.3 0
Assert ($resumed.Chapter -eq 1) 'The upper warp enters the same existing ruins.'

$machine=[Echoes.Journey]::new(100)
$machine.SelectChapter(0)
FlyFlat $machine 112 .76 1
Assert ($machine.Branch -eq 'Right' -and $machine.Section -eq 'MachineApproach') 'The right fork selects the machine route.'
FlyFlat $machine 168 .76 0
Assert ($machine.Section -eq 'Vestibule' -and $machine.ForestHeightLimit -gt 1) 'The door area quietly allows additional ascent.'
for ($lane=-1; $lane -le 1; $lane++) {
    $atEntry = $machine.ForestLights
    FlyFlat $machine 179 .36 $lane
    Assert ($machine.Phase -eq 'ForestDoor' -and $machine.ForestDoor -eq $lane + 1) 'Each lower entrance plays its own machine event.'
    Step $machine 3.1 0
    Assert ($machine.Distance -eq 168 -and $machine.ForestLights -eq $atEntry -and $machine.Phase -eq 'ForestExplore') 'The arcade restores the door threshold and the same reserve after a false choice.'
}
Pose $machine Fist .35 .3
Pose $machine Open .44 .3
Pose $machine Fist .19 .3
Pose $machine Open .39 .3
Pose $machine Fist .18 .3
Pose $machine Open .90 .3
Assert ($machine.Section -eq 'Brain' -and $machine.Flight.Height -gt 1 -and $machine.ForestDoor -eq -1) 'Real hand releases and sound reach the hidden path above the usual ceiling.'
FlyFlat $machine 194 1.10 0
Assert ($machine.Section -eq 'Shaft') 'The brain axis reaches the descent.'
Step $machine 2.70 .3
Assert ($machine.Section -eq 'Heart' -and $machine.Flight.Height -lt .1 -and $machine.Phase -eq 'ForestPlanting') 'The shaft descends continuously into the heart, leaving voice to plant.'
PlantRoot $machine 0 .05
Assert ($machine.Phase -eq 'ForestTransition' -and $machine.ForestTrap -eq 'Cable' -and $machine.Plants -eq 1 -and
    $machine.ForestCableAnchor -in 0,1,2) 'The machine heart accepts one light and activates an authored cable port.'
Step $machine 5.3 0
Assert ($machine.Chapter -eq 1) 'The right branch also enters the existing ruins.'

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
Write-Output 'Passed forest ground and flight controls, fly reserve and rescue, both maze endings, all three false doors, real-input upper entry, circuit descent, ruins, stars, checkpoints, projection and EOL3.'
