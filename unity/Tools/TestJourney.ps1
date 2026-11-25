$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs', 'JourneyRuins.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'RuinAtlas.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
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

function RuinSteer($game, $lane) {
    while ($game.RuinTargetLane -ne $lane) {
        Assert ($game.Touch([int]($game.RuinTargetLane -lt $lane))) 'The tunnel accepts a lateral target.'
    }
}

function FlyRuin($game, $depth, $height, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    RuinSteer $game $lane
    for ($i = 0; $i -lt 3600 -and $game.Distance -lt $depth -and $game.Phase -eq 'RuinExplore' -and !$game.RuinGuide; $i++) {
        if ($game.RuinLootOpen) {
            while ($game.RuinLootChoices[$game.RuinLootIndex].Action -ne 'Leave') { [void]$game.Touch(0) }
            [void]$game.Touch(1)
        }
        $game.Flight.Reset($height, [Echoes.Journey]::RuinHeightLimit)
        $game.Step(.02, .34)
    }
}

function CrossRuin($game) {
    Step $game .6 0
    Assert ($game.Phase -eq 'RuinTransfer' -and $game.Progress -eq 0) 'A selected passage stays at its mouth in silence.'
    for ($i = 0; $i -lt 240 -and $game.Phase -eq 'RuinTransfer'; $i++) { $game.Step(.02, .88) }
    Assert ($game.Phase -eq 'RuinExplore') 'Voice carries the player into the next tunnel.'
}

function FollowRuin($game, $portal, [switch]$AllowWall) {
    FlyRuin $game ($portal.Depth + .10) $portal.Altitude $portal.Lane
    if ($AllowWall -and $game.Phase -eq 'RuinRetry') { return }
    Assert ($game.RuinCanChoosePassage -and $game.RuinApproach -eq $portal -and $game.Phase -eq 'RuinExplore') 'The branch is optional until its entry is chosen.'
    [void]$game.Touch(1)
    if ($portal.Gate -eq 'Palm' -and !$portal.Opened) {
        Assert ($game.RuinGuide -eq $portal) 'A selected palm gate pauses at its prompt.'
        Pose $game Fist .48 0
        Pose $game Open .03 0
    }
    if ($portal.Gate -eq 'Voice' -and !$portal.Opened) {
        Assert ($game.RuinGuide -eq $portal) 'A selected voice gate pauses at its prompt.'
        Step $game .55 .30
    }
    Step $game .60 0
    Assert ($game.Phase -eq 'RuinTransfer' -and $game.RuinEntrance -eq $portal) 'The chosen threshold starts its directional transition.'
    CrossRuin $game
}
function Grant($game) {
    [void]$game.Touch(0)
    [void]$game.Touch(1)
    Assert ($game.Phase -eq 'StarsDelegated') 'A shown suggestion should be delegatable.'
}

function Steer($game, $lane) {
    for ($i=0; $i -lt 2 -and $game.ForestTargetLane -ne $lane; $i++) {
        $button = [int]($game.ForestTargetLane -lt $lane)
        Assert ($game.Touch($button)) 'An available forest route should accept the requested lane.'
        if ($game.ForestTargetLane -ne $lane) { Step $game 1.10 0 }
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
    Assert ($game.Branch -eq 'Left' -and $game.ForestCenter -lt -1 -and $game.ForestCenter -gt -2) 'The shared route commits beside the actual left opening.'
    FlyFlat $game 124 .50 -1
    Assert ($game.ForestMazeActive -and $game.ForestMazeNode -eq $game.ForestMaze.Entry) 'The left entry reaches the actual maze floor.'
    $start = $game.Time
    for ($i=0; $i -lt 6200 -and $game.Phase -ne 'ForestPlanting'; $i++) {
        if ($game.MazeMotion -eq 'Choosing') {
            $direction = [int]($game.ForestMazeRight -eq $game.ForestMazeNode + 1)
            Assert ($game.Touch($direction)) 'The fork should accept the route along the spine.'
        }
        if ($game.Phase -eq 'ForestExplore') { $game.Flight.Reset(.35,$game.ForestHeightLimit) }
        $game.Step(.02,1)
    }
    Assert ($game.Phase -eq 'ForestPlanting' -and $game.ForestPlantsRequired -eq 2) 'The maze ends at both roots.'
    Assert ($game.Time - $start -ge 60 -and $game.ForestMazeTravel -ge 204) 'The fastest available maze route remains at least a minute of actual travel.'
}
function PlantRoot($game, $lane, $height, $voice = .30) {
    Steer $game $lane
    for ($i = 0; $i -lt 150 -and $game.Phase -eq 'ForestPlanting'; $i++) {
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
Assert ($game.ForestLane -lt -.04 -and $game.ForestLane -gt -.15) 'Ground motion eases gently through intermediate positions.'
[void]$game.Touch(1)
Step $game .02 .7
Assert ($game.ForestLane -lt 0) 'A steering reversal keeps position and momentum.'
Steer $game 1
Step $game 1.5 .7
Assert ($game.Distance -eq 0 -and $game.ForestLane -gt .98 -and $game.ForestTargetLane -eq 1) 'The ground accepts lateral movement without advancing.'
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
Assert ($game.Chapter -eq 1 -and $game.Phase -eq 'RuinExplore') 'Both roots lead into the flight-based ruins.'
Assert ($game.RuinRetryRequired -in 6..9) 'The wall is seeded between six and nine distinct side tunnels.'
foreach ($nextRoom in 1,11,12,3,13,14,5,8,10) {
    $through = $game.RuinPortals | Where-Object TargetRoom -eq $nextRoom
    FollowRuin $game $through -AllowWall
    if ($game.Phase -eq 'RuinRetry') { break }
    Assert ($game.RuinRoomId -eq $nextRoom) 'The branch opens its own tunnel.'
}
if ($game.Phase -eq 'RuinExplore') { FlyRuin $game ($game.RuinRoomLength - 2) .60 0 }
Assert ($game.Phase -eq 'RuinRetry' -and $game.RuinRetryVisited -ge $game.RuinRetryRequired) 'Exploring distinct passages triggers the wall in a clear stretch.'
Step $game 4.50 0
Assert ($game.RuinRetryState -eq 'Choice') 'Impact and the archived choice lead to two available branches.'
$choiceSave = $game.Checkpoint
$guide = [Echoes.Journey]::new(17)
Assert ($guide.Restore($choiceSave) -and $guide.RuinRetryState -eq 'Choice') 'The wall can be resumed after restoring.'
Pose $guide Open .1 0
[void]$guide.Touch(0)
Step $guide 11 0
Assert ($guide.Phase -eq 'Complete' -and $guide.RuinRetryOutcome -eq 'Guided') 'The player can complete the guided route.'
Assert ($guide.Restore($choiceSave)) 'The same checkpoint can exercise taking back control.'
Pose $guide Open .1 0
[void]$guide.Touch(0)
Step $guide 4.25 0
[void]$guide.Touch(1)
Step $guide 3.65 0
Assert ($guide.Phase -eq 'Complete' -and $guide.RuinRetryOutcome -eq 'Takeover') 'Taking over records a deviation in the system route.'
[void]$game.Touch(1)
Step $game 1.20 0
$correctSide = [int]($game.RuinRetryTraceLane -gt 0)
[void]$game.Touch(1 - $correctSide)
Assert ($game.RuinRetryState -eq 'Fault') 'A wrong replay direction pauses on its remembered trace.'
Step $game 2.85 0
[void]$game.Touch($correctSide)
Assert ($game.RuinRetryState -eq 'Listen') 'The second attempt can follow the original movement.'
Step $game 2.65 0
Assert ($game.RuinRetryState -eq 'Release') 'Silence allows both beats to sound before breath opens the wall.'
Step $game .59 .33
Step $game 3.55 0
Assert ($game.Phase -eq 'Complete' -and $game.RuinResult -eq 'Explored' -and $game.RuinRetryOutcome -eq 'Remembered' -and $game.RuinRetryAttempts -eq 2) 'The remembered route completes without a quota and preserves retries.'
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
Assert ($saved.Phase -eq 'Complete' -and $saved.Plants -eq 2 -and $saved.RuinResult -eq 'Explored' -and
    $saved.RuinRetryOutcome -eq 'Remembered' -and $saved.StarCount -eq 3 -and $saved.Withdrawals -eq 1 -and $saved.Mistakes -eq 2) 'Restore the completed route and consequences.'

function ReachesWall($maze,$node,$next) {
    while ($maze.Nodes[$next].Kind -eq 'Corridor') {
        $edge=$maze.Connections($next) | Where-Object { $_.Other($next) -ne $node } | Select-Object -First 1
        if (!$edge) { return $false }
        $exit=$edge.Other($next)
        $node=$next
        $next=$exit
    }
    return $maze.Nodes[$next].Kind -eq 'DeadEnd'
}

$canopy=[Echoes.Journey]::new(100)
$canopy.SelectChapter(0)
FlyFlat $canopy 112 .76 -1
FlyFlat $canopy 124 .50 -1
$maze=$canopy.ForestMaze
$leftRooms=@($maze.Nodes | Where-Object Kind -eq Junction)
Assert ($leftRooms.Length -ge 8 -and $maze.Edges.Count -ge $maze.Nodes.Count + 1) 'The generated layout must contain physical junctions and loops.'
$seedSignatures=@{}
for ($seed=1; $seed -le 16; $seed++) {
    $candidate=[Echoes.ForestMaze]::new($seed)
    Assert ($candidate.ShortestDistance() / [Echoes.ForestMaze]::MaximumSpeed -ge 60) 'Seeded layouts keep the full playable minimum at maximum speed.'
    $signature=($candidate.Edges | ForEach-Object { "$($_.A)-$($_.B)" }) -join ','
    $seedSignatures[$signature]=$true
}
Assert ($seedSignatures.Count -gt 1) 'Different seeds change connections as well as the room dimensions.'
$checkpointX=0
$checkpointZ=0
$marked = $false
for ($i=0; $i -lt 4200 -and $canopy.MazeMotion -ne 'WaitingAtWall'; $i++) {
    if ($canopy.MazeMotion -eq 'Choosing') {
        $at=$canopy.ForestMazeNode
        $wallLeft=ReachesWall $maze $at $canopy.ForestMazeLeft
        $wallRight=ReachesWall $maze $at $canopy.ForestMazeRight
        if ($wallLeft -or $wallRight) {
            $checkpointX=$canopy.ForestMazeX
            $checkpointZ=$canopy.ForestMazeZ
            $marked=$true
            [void]$canopy.Touch([int]$wallRight)
        } else {
            [void]$canopy.Touch([int]($canopy.ForestMazeRight -eq $canopy.ForestMazeNode + 1))
        }
    }
    if ($canopy.Phase -eq 'ForestExplore') { $canopy.Flight.Reset(.31,$canopy.ForestHeightLimit) }
    $canopy.Step(.02,1)
}
Assert ($marked -and $canopy.MazeMotion -eq 'WaitingAtWall') 'A dead end is physically reachable.'
$wallX=$canopy.ForestMazeX
$wallZ=$canopy.ForestMazeZ
$heading=$canopy.ForestMazeHeading
Step $canopy .86 0
Assert ($canopy.MazeMotion -eq 'WaitingAtWall' -and $canopy.ForestMazeX -eq $wallX -and
    $canopy.ForestMazeZ -eq $wallZ -and $canopy.ForestMazeHeading -eq $heading) 'The dead end holds for a full second before the turn.'
Step $canopy .60 0
Assert ($canopy.MazeMotion -eq 'TurningBack' -and $canopy.ForestMazeHeading -ne $heading -and $canopy.ForestMazeX -eq $wallX) 'The character progressively rotates on the spot.'
Step $canopy 1.2 0
Assert ($canopy.MazeMotion -eq 'Retracing' -and ($canopy.ForestMazeX -ne $wallX -or $canopy.ForestMazeZ -ne $wallZ)) 'The character traverses the path back, preserving position continuity.'
for ($i=0; $i -lt 1000 -and $canopy.Phase -eq 'ForestLoop'; $i++) { $canopy.Step(.02,0) }
Assert ($canopy.MazeMotion -eq 'Choosing' -and [Math]::Abs($canopy.ForestMazeX - $checkpointX) -lt .05 -and
    [Math]::Abs($canopy.ForestMazeZ - $checkpointZ) -lt .05) 'The character returns to the last choice, not the start of the maze.'
Assert ($canopy.TurnAroundForest()) 'The same turn is available by long press from a choice.'
Step $canopy 1.05 1
$x=$canopy.ForestMazeX
$z=$canopy.ForestMazeZ
Step $canopy .5 1
Assert (($canopy.ForestMazeX -ne $x -or $canopy.ForestMazeZ -ne $z) -and $canopy.Phase -eq 'ForestExplore') 'After manually facing back the player can walk backwards in the graph.'
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
Step $caught 3 0
Assert ($caught.ForestLights -eq $reserve -and $caught.Phase -eq 'ForestTrapped') 'The same fly struggles before it is spent.'
Step $caught 3.85 0
Assert ($caught.ForestLights -eq $reserve - 1 -and $caught.Cue -eq 'ForestRescueLight') 'Only the final rescue beat spends the companion.'
Step $caught 2.6 0
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
Assert (!$web.Collected -and $empty.ForestThreatPulse -eq 0 -and $empty.Phase -eq 'ForestExplore') 'A player below the spider travels past its concealed route without revealing it.'
FlyFlat $empty ($web.Depth + .4) .91 $web.Lane
Assert ($empty.ForestTrap -eq 'Web' -and $empty.ForestWindFactor -gt 0 -and $empty.ForestHasRescue) 'A top approach reveals and triggers the canopy web.'
$empty.Retry()
Step $empty 9.48 0
Assert ($empty.Phase -eq 'ForestFalling' -and $empty.ForestLights -eq 0 -and $empty.ForestTrap -eq 'Web') 'The last rescue releases the sprite before the same spider returns in the defeat.'
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
    Step $machine 13.5 0
    Assert ($machine.Distance -eq 168 -and $machine.ForestLights -eq $atEntry -and $machine.Phase -eq 'ForestExplore') 'The arcade restores the door threshold and the same reserve after a false choice.'
}
Pose $machine Fist .35 .3
Pose $machine Open .44 .3
Pose $machine Fist .19 .3
Pose $machine Open .39 .3
Pose $machine Fist .18 .3
Pose $machine Open .38 .3
Pose $machine Fist .18 .3
Pose $machine Open .90 .3
Assert ($machine.Section -eq 'Brain' -and $machine.Flight.Height -gt 1 -and $machine.ForestDoor -eq -1) 'Real hand releases and sound reach the hidden path above the usual ceiling.'
FlyFlat $machine 205 1.10 0
Assert ($machine.Section -eq 'Shaft') 'The brain axis reaches the descent.'
Step $machine 4.60 .3
Assert ($machine.Section -eq 'Shaft' -and $machine.ForestShaftProgress -gt .30 -and $machine.ForestShaftProgress -lt .62) 'The enlarged machine spine has a continuous long middle section.'
Step $machine 9 0
Assert ($machine.Section -eq 'Heart' -and $machine.Flight.Height -lt .1 -and $machine.Phase -eq 'ForestPlanting') 'The shaft descends continuously into the heart, leaving voice to plant.'
PlantRoot $machine 0 .05
Assert ($machine.Phase -eq 'ForestTransition' -and $machine.ForestTrap -eq 'Cable' -and $machine.Plants -eq 1 -and
    $machine.ForestCableAnchor -in 0,1,2) 'The machine heart accepts one light and activates an authored cable port.'
Step $machine 8.9 0
Assert ($machine.Chapter -eq 1) 'The right branch also enters the existing ruins.'

$ruins=[Echoes.Journey]::new(92)
$ruins.SelectChapter(1)
Assert ($ruins.InRuins -and $ruins.RuinLayer -eq 0 -and $ruins.RuinVisitedCount -eq 1) 'The entrance is its own level with a threshold to restore.'
$ruins.SetHand([Echoes.Hand]::Open)
Step $ruins 75 0
Assert ($ruins.Distance -eq 0 -and $ruins.RuinForwardSpeed -eq 0 -and $ruins.Phase -eq 'RuinExplore') 'Idle exploration has no initial speed or ending countdown.'
Step $ruins 1 .30
$quietSpeed = $ruins.RuinForwardSpeed
Step $ruins 1 .78
Assert ($quietSpeed -gt 0 -and $ruins.RuinForwardSpeed -gt $quietSpeed) 'Volume controls walking pace.'
Step $ruins .26 0
$rest = $ruins.Distance
Step $ruins 2 0
Assert ($ruins.Distance -eq $rest -and $ruins.RuinForwardSpeed -eq 0) 'Silence brings travel to a full stop.'
[void]$ruins.Touch(0)
Step $ruins .8 0
Assert ($ruins.RuinLane -lt -.8 -and $ruins.Distance -eq $rest) 'The player can inspect the sides while stopped.'
$ruins.Retry()
$gate = $ruins.RuinPortals | Where-Object Gate -eq Palm
FlyRuin $ruins $gate.Depth $gate.Altitude $gate.Lane
Assert ($ruins.RuinApproach -eq $gate -and !$ruins.RuinGuide) 'A nearby locked gate is optional.'
[void]$ruins.Touch(1)
Assert ($ruins.RuinGuide -eq $gate -and !$gate.Opened) 'A closed side passage is encountered with an explicit guide.'
Pose $ruins Fist .22 0
Pose $ruins Open .03 0
Assert (!$gate.Opened) 'A short shake does not consume the optional gesture.'
Pose $ruins Fist .45 0
Pose $ruins Open .03 0
Assert ($gate.Opened -and $ruins.Cue -eq 'RuinDoor') 'A timed release opens the palm door.'
Assert ($ruins.Touch(0)) 'A explicitly leaves the opened gate.'
RuinSteer $ruins 0
Step $ruins .8 0
Assert ($ruins.Phase -eq 'RuinExplore' -and $ruins.RuinRoomId -eq 0) 'Turning away can skip even an unlocked branch.'
$ruins.Retry()
Assert (($ruins.RuinPortals | Where-Object Gate -eq Palm).Opened) 'Retry returns to the threshold and preserves opened doors.'
FollowRuin $ruins $gate
Assert ($ruins.RuinRoomId -eq 1 -and $ruins.RuinRoomStyle -eq 2 -and $ruins.RuinVisitedCount -eq 2) 'An optional side branch enters a distinct nested gallery.'
$voiceGate = $ruins.RuinPortals | Where-Object Gate -eq Voice
FollowRuin $ruins $voiceGate
Assert ($ruins.RuinRoomId -eq 6 -and $ruins.RuinLayer -eq -2) 'A simple voice prompt gains access to another distinct layer.'

$ceilingRoute=[Echoes.Journey]::new(92)
$ceilingRoute.SelectChapter(1)
$top = $ceilingRoute.RuinPortals | Where-Object Direction -eq Upper
Pose $ceilingRoute Open (($top.Depth - 7.0) / 3.20) .70
for ($attempt = 0; $attempt -lt 14 -and $ceilingRoute.Phase -eq 'RuinExplore'; $attempt++) {
    Pose $ceilingRoute Fist .18 .30
    Pose $ceilingRoute Open .00 .30
    for ($scan = 0; $scan -lt 18 -and $ceilingRoute.Phase -eq 'RuinExplore'; $scan++) {
        $ceilingRoute.Step(.02,.30)
        if ($ceilingRoute.RuinCanChoosePassage -and $ceilingRoute.RuinApproach -eq $top) { [void]$ceilingRoute.Touch(1) }
    }
}
Assert ($ceilingRoute.Phase -eq 'RuinTransfer' -and $ceilingRoute.RuinEntrance -eq $top -and
    $ceilingRoute.Flight.Height -ge .64) 'Actual repeated releases make the ceiling entrance reachable without a test altitude override.'
CrossRuin $ceilingRoute
Assert ($ceilingRoute.RuinLayer -eq 1) 'A ceiling branch leads to an independent upper floor.'

$floorRoute = [Echoes.Journey]::new(92)
$floorRoute.SelectChapter(1)
$low = $floorRoute.RuinPortals | Where-Object Direction -eq Lower
FlyRuin $floorRoute ($low.Depth - 4.32) .46 0
[void]$floorRoute.Touch(1)
for ($i=0; $i -lt 50; $i++) {
    $floorRoute.Flight.Reset(.46, [Echoes.Journey]::RuinHeightLimit)
    $floorRoute.Step(.02, .34)
    if ($floorRoute.RuinLane -gt .7 -and $floorRoute.RuinCanChoosePassage) { break }
}
Assert ($floorRoute.RuinLane -gt .4 -and $floorRoute.RuinCanChoosePassage -and $floorRoute.RuinApproach -eq $low) 'The upper edge of the floor opening accepts a lateral offset before its mouth.'
[void]$floorRoute.Touch(1)
Assert ($floorRoute.Phase -eq 'RuinTransfer' -and $floorRoute.RuinEntrance -eq $low) 'The widened lower opening accepts a glancing approach.'
CrossRuin $floorRoute

$circuit = [Echoes.Journey]::new(44)
$circuit.SelectChapter(1)
$circuit.SetHand([Echoes.Hand]::Open)
for ($sample = 0; $sample -lt 4000 -and $circuit.RuinLoops -eq 0; $sample++) { $circuit.Step(.02,.80) }
Assert ($circuit.RuinLoops -eq 1 -and $circuit.RuinRoomId -eq 0 -and $circuit.Phase -eq 'RuinExplore') 'Passing the seam continues the same room without forced transfer.'
$resumed = [Echoes.Journey]::new(99)
Assert ($resumed.Restore($circuit.Checkpoint) -and $resumed.RuinLoops -eq 1) 'Returning through a save retains completed circuits.'
$resumed.SetHand([Echoes.Hand]::Open)
for ($sample = 0; $sample -lt 4000 -and $resumed.Phase -eq 'RuinExplore'; $sample++) { $resumed.Step(.02,.80) }
Assert ($resumed.RuinRetryState -eq 'Impact' -and $resumed.RuinLoops -eq 1 -and $resumed.RuinRetryVisited -eq 0) 'Finishing the main gallery triggers the ending even without committing to a branch.'
Step $resumed 4.3 0
[void]$resumed.Touch(0)
Step $resumed 10.5 0
Assert ($resumed.Phase -eq 'Complete' -and $resumed.RuinRetryOutcome -eq 'Guided') 'The main-circuit ending remains finishable.'

$heartbeat=[Echoes.Journey]::new(92)
$heartbeat.SelectChapter(1)
$heart = $heartbeat.RuinObjects | Where-Object Kind -eq Heart
FlyRuin $heartbeat ($heart.Depth - 1.7) $heart.Altitude 0
Assert (!$heart.Collected -and $heartbeat.RuinHeartAge -lt 0 -and $heart.Reveal -lt .2) 'From the center only a trace of an alcove is visible.'
RuinSteer $heartbeat $heart.Lane
Step $heartbeat .75 0
Assert ($heartbeat.Cue -eq 'RuinHeartbeat' -and $heartbeat.RuinHeartAge -ge 0 -and !$heart.Collected -and $heart.Reveal -gt .3) 'The first heart reveals and startles before contact.'
$heartSave=$heartbeat.Checkpoint
FlyRuin $heartbeat $heart.Depth $heart.Altitude $heart.Lane
Assert (!$heart.Collected -and $heartbeat.RuinHearts -eq 0) 'Passing right through a nearby cache cannot collect it.'
Step $heartbeat .28 0
Assert ($heartbeat.RuinCanTake -and $heartbeat.RuinSearchTarget -eq $heart) 'Stopping beside the revealed alcove makes a collect action available.'
[void]$heartbeat.Touch(1)
Assert ($heartbeat.RuinHearts -eq 1 -and $heart.Collected) 'The heart is taken only when explicitly picked up.'
Assert ($heartbeat.Restore($heartSave)) 'The pre-pickup heart checkpoint restores.'
$heardCue=$heartbeat.CueId
$heart = $heartbeat.RuinObjects | Where-Object Kind -eq Heart
FlyRuin $heartbeat ($heart.Depth - 1.3) $heart.Altitude $heart.Lane
Assert ($heartbeat.RuinHeartAge -lt 0 -and $heartbeat.CueId -eq $heardCue) 'The scare stays consumed after a checkpoint restore.'
FlyRuin $heartbeat $heart.Depth $heart.Altitude $heart.Lane
Step $heartbeat .28 0
[void]$heartbeat.Touch(1)
Assert ($heartbeat.RuinHearts -eq 1 -and $heartbeat.Cue -eq 'RuinCollect') 'A previously heard heart remains collectible without a second scare.'

$atlas = [Echoes.Journey]::new(77)
$atlas.SelectChapter(1)
$atlasSave = $atlas.Checkpoint
$passages = [System.Collections.Generic.Dictionary[int,int[]]]::new()
$directions = [System.Collections.Generic.HashSet[string]]::new()
$styles = [System.Collections.Generic.HashSet[int]]::new()
$depths = [System.Collections.Generic.HashSet[int]]::new()
$kinds = @{ Phone=0; Computer=0; Heart=0; Brain=0 }
for ($room=0; $room -lt 16; $room++) {
    $atlasSave.ruinRoom = $room
    Assert ($atlas.Restore($atlasSave)) 'Each authored room is valid as a saved starting threshold.'
    [void]$depths.Add($atlas.RuinLayer)
    foreach ($portal in $atlas.RuinPortals) { [void]$directions.Add($portal.Direction.ToString()) }
    $passages[$room] = [int[]]@($atlas.RuinPortals | ForEach-Object { $_.TargetRoom })
    foreach ($item in $atlas.RuinObjects) {
        if ($item.Kind -eq 'Chest') { [void]$styles.Add($item.ChestStyle) }
        else {
            Assert ([Math]::Abs($item.Lane) -eq 1) 'All four loose relic kinds sit away from the central travel line.'
            $kinds[$item.Kind.ToString()]++
        }
    }
}
Assert ($directions.Count -eq 4 -and $depths.Count -eq 7 -and $styles.Count -eq 10) 'The atlas has four voluntary entry directions, seven elevations and ten distinct chests.'
Assert ($kinds.Phone -gt $kinds.Heart -and $kinds.Phone -gt $kinds.Brain -and $kinds.Computer -gt $kinds.Heart -and $kinds.Computer -gt $kinds.Brain) 'Both technological finds individually appear more frequently.'
foreach ($start in 0..15) {
    $reached = [System.Collections.Generic.HashSet[int]]::new()
    $pending = [System.Collections.Generic.Queue[int]]::new()
    $pending.Enqueue($start)
    while ($pending.Count -gt 0) {
        $found = $pending.Dequeue()
        if (!$reached.Add($found)) { continue }
        foreach ($next in $passages[$found]) { $pending.Enqueue($next) }
    }
    Assert ($reached.Contains(0)) 'Every room can return to the entrance hall through optional branches.'
    if ($start -eq 0) { Assert ($reached.Count -eq 16) 'Every layer and secret room is reachable from the entry.' }
}

$atlasSave.ruinRoom = 11
Assert ($atlas.Restore($atlasSave)) 'A hidden storage layer can be visited.'
$chest = $atlas.RuinObjects | Where-Object Kind -eq Chest
FlyRuin $atlas $chest.Depth $chest.Altitude $chest.Lane
Step $atlas .28 0
Assert ($atlas.RuinCanTake -and !$atlas.RuinLootOpen -and $atlas.RuinChests -eq 0) 'Stopping next to a chest leaves it closed until the player chooses.'
[void]$atlas.Touch(1)
Assert ($atlas.RuinLootSource -eq $chest -and $atlas.RuinChests -eq 1) 'B still opens the ten-style chest and its choice panel.'

$atlasSave.ruinFinds = -1
$atlasSave.ruinRoom = 15
Assert ($atlas.Restore($atlasSave) -and $atlas.RuinInventory -eq 15 -and $atlas.RuinChests -eq 10) 'Restoring a crowded inventory still caps artifacts at 15, independently of the chests.'
$overflow = $atlas.RuinObjects | Where-Object { !$_.Collected -and $_.Kind -ne 'Chest' } | Select-Object -First 1
FlyRuin $atlas $overflow.Depth $overflow.Altitude $overflow.Lane
Step $atlas .28 0
Assert ($atlas.RuinCanTake) 'A full bag does not prevent inspecting the alcove.'
[void]$atlas.Touch(1)
Assert (!$overflow.Collected -and $atlas.RuinInventory -eq 15) 'A confirmed pickup cannot add a sixteenth artifact.'
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
Write-Output 'Passed forest controls, rescue, seeded maze, both roots, machine doors and upper gallery, layered ruins, all wall outcomes, stars, checkpoints, projection and EOL3.'
