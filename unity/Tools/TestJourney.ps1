$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyStars.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs', 'JourneyRuins.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'RuinAtlas.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
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

function Launch-Gesture($game) {
    Pose $game Open .02 0
    Pose $game Fist .20 0
    $game.SetHand([Echoes.Hand]::Open)
}

function RuinSteer($game, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    for ($i = 0; $i -lt 120 -and [Math]::Abs($game.RuinLane - $lane) -gt .11; $i++) {
        $goLeft = $game.RuinLane -gt $lane
        $game.SetRuinSteering($goLeft, !$goLeft)
        $game.Step(.02, 0)
    }
    $game.SetRuinSteering($false, $false)
    Step $game .20 0
}

function FlyRuin($game, $depth, $height, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    RuinSteer $game $lane
    for ($i = 0; $i -lt 3600 -and $game.Distance -lt $depth -and $game.Phase -eq 'RuinExplore'; $i++) {
        $game.Flight.Reset($height, [Echoes.Journey]::RuinHeightLimit)
        $game.Step(.02, .46)
    }
}

function CrossRuin($game, $voice = .52) {
    for ($i = 0; $i -lt 1450 -and $game.Phase -eq 'RuinTransfer'; $i++) { $game.Step(.02, $voice) }
    Assert ($game.Phase -eq 'RuinExplore') 'The selected passage reaches its target gallery.'
}

function FollowRuin($game, $portal, [switch]$AllowWall) {
    FlyRuin $game ($portal.Depth - .32) 0 0
    if ($AllowWall -and $game.Phase -eq 'RuinRetry') { return }
    Assert ($game.Phase -eq 'RuinExplore') 'The branch stays optional until its directional input.'
    switch ($portal.Direction.ToString()) {
        'Left' { $game.SetRuinSteering($true,$false); $game.Step(.02,.46) }
        'Right' { $game.SetRuinSteering($false,$true); $game.Step(.02,.46) }
        'Upper' {
            $game.Flight.Reset($portal.Altitude, [Echoes.Journey]::RuinHeightLimit)
            $game.Step(.02, .34)
        }
        'Lower' {
            $game.Flight.Reset(.46, [Echoes.Journey]::RuinHeightLimit)
            Step $game .12 .34
        }
    }
    $game.SetRuinSteering($false,$false)
    Assert ($game.Phase -eq 'RuinTransfer' -and $game.RuinEntrance -eq $portal -and $portal.Opened) 'The direction opens the chosen passage directly.'
    CrossRuin $game
}
function Steer($game, $lane) {
    if ($game.Branch -eq 'Left' -and !$game.ForestMazeActive) { return }
    $rate = 1.35 * $game.ForestLateralScale
    for ($tick=0; $tick -lt 260 -and [Math]::Abs($lane-$game.ForestTargetLane) -gt .001; $tick++) {
        $delta = $lane - $game.ForestTargetLane
        $game.SetForestLateralInput($delta -lt 0, $delta -gt 0)
        $game.Step([Math]::Min(.02, [Math]::Abs($delta) / $rate), 0)
    }
    $game.SetForestLateralInput($false,$false)
}

function FlyFlat($game, $depth, $height, $lane) {
    $game.SetHand([Echoes.Hand]::Open)
    $game.Flight.Reset($game.ForestFlightFloor, $game.ForestHeightLimit)
    Steer $game $lane
    for ($i = 0; $i -lt 1800 -and $game.Distance -lt $depth -and $game.Phase -eq 'ForestExplore'; $i++) {
        $game.Flight.Reset($height, $game.ForestHeightLimit)
        $game.Step(.02, .3)
    }
}

function Aim-MazeExit($game, $exit) {
    $node=$game.ForestMaze.Nodes[$game.ForestMazeNode]
    $door=$game.ForestMaze.Nodes[$exit]
    $heading=[Math]::Atan2($door.X-$node.X,$door.Z-$node.Z) * 180 / [Math]::PI
    $angle=($heading-$game.ForestMazeHeading+540)%360-180
    $game.SetHand([Echoes.Hand]::Fist)
    $game.SetMazeSteering($angle -lt 0, $angle -gt 0)
    for ($tick=0; $tick -lt [Math]::Round([Math]::Abs($angle)/2.24); $tick++) { $game.Step(.02,0) }
    $game.SetMazeSteering($false,$false)
    $game.SetHand([Echoes.Hand]::Open)
}
function EnterLeft($game) {
    FlyFlat $game 112 .76 -1
    Assert ($game.Branch -eq 'Left' -and $game.ForestCenter -lt -1 -and $game.ForestCenter -gt -2) 'The shared route commits beside the actual left opening.'
    FlyFlat $game 124 .50 -1
    Assert ($game.ForestMazeActive -and $game.ForestMazeNode -eq $game.ForestMaze.Entry) 'The left entry reaches the actual maze floor.'
    $start = $game.Time
    for ($i=0; $i -lt 6200 -and $game.Phase -ne 'ForestPlanting'; $i++) {
        if ($game.MazeMotion -eq 'Choosing') {
            Aim-MazeExit $game ($game.ForestMazeNode + 1)
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
for ($frame = 0; $frame -lt 2500 -and $game.IsOpening; $frame++) { $game.Step(.02, 0) }
Assert ($game.Phase -eq 'ForestExplore' -and $game.EnteredFromOpening) 'The opening should land in the first chapter.'
Pose $game Open .36 .7
Assert ($game.ForestLights -eq 3) 'The forest begins with three companion lights.'
$game.SetForestLateralInput($true,$false)
Step $game .32 .7
Assert ($game.ForestLane -lt -.04 -and $game.ForestLane -gt -.15) 'Ground motion eases gently through intermediate positions.'
$game.SetForestLateralInput($false,$true)
Step $game .02 .7
Assert ($game.ForestLane -lt 0) 'A steering reversal keeps position and momentum.'
Steer $game 1
Step $game 1.5 .7
Assert ($game.Distance -eq 0 -and $game.ForestLane -gt .98 -and $game.ForestTargetLane -gt .999) 'The ground accepts held lateral movement without advancing.'
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
Step $game 10.3 0
Assert ($game.Chapter -eq 1 -and $game.Phase -eq 'RuinExplore') 'Both roots lead into the flight-based ruins.'
Assert ($game.RuinDiscipline -eq 0 -and $game.RuinDisciplinesSeen -eq 1) 'The entrance opens on physics.'
$galleryOrder = @{}
$galleryOrder[0] = 0
$crossed = 0
foreach ($nextRoom in 1,11,12,3,13,14,5,8,10) {
    $expectRoom = if ($game.RuinWallPassages -eq $game.RuinWallPassagesRequired - 1) { 9 } else { $nextRoom }
    $through = $game.RuinPortals | Where-Object TargetRoom -eq $expectRoom | Sort-Object Depth | Select-Object -First 1
    FollowRuin $game $through -AllowWall
    $crossed++
    Assert ($game.RuinRoomId -eq $expectRoom) 'Ordinary branches reach their own room; the last branch reaches the terminal tunnel.'
    if ($galleryOrder.Count -lt 9) {
        Assert ($game.RuinDiscipline -eq $galleryOrder.Count) 'New galleries advance the exhibit topics in order.'
        $galleryOrder[$expectRoom] = $game.RuinDiscipline
    }
    Assert ($game.RuinWallPassages -eq $crossed) 'A finished transfer counts once.'
    if ($game.RuinWallPassages -eq $game.RuinWallPassagesRequired) { break }
}
Assert ($game.RuinWallPassagesRequired -in 6..9 -and $game.RuinWallPassages -eq $game.RuinWallPassagesRequired -and
    $game.Phase -eq 'RuinExplore' -and $game.RuinRoomId -eq 9 -and $game.RuinRoomLength -eq 51) 'The sixth through ninth transfer reaches the original-length terminal tunnel without skipping to its end.'
$queued = [Echoes.Journey]::new(13)
Assert ($queued.Restore($game.Checkpoint) -and $queued.RuinWallPassages -eq $queued.RuinWallPassagesRequired) 'The queued wall survives a checkpoint.'
FlyRuin $queued 7 .05 0
Assert ($queued.RuinRetryState -eq 'Inactive') 'The wall does not trigger near the final tunnel entrance.'
FlyRuin $queued 51 .05 0
Assert ($queued.RuinRetryState -eq 'Impact' -and $queued.RuinRetryWallDepth -eq 51) 'Continuing to the final tunnel end triggers the restored wall.'
FlyRuin $game 51 .6 0
Assert ($game.RuinRetryState -eq 'Impact' -and $game.RuinRetryWallDepth -eq 51) 'The final wall catches the player at its terminal end.'
$debugWall = [Echoes.Journey]::new(29)
$debugWall.JumpToRuinWallDebugPoint()
Assert ($debugWall.RuinRetryState -eq 'Impact' -and $debugWall.Age -eq 0 -and $debugWall.RuinRetryWallDepth -eq 51) 'The debug point starts the actual final-tunnel collision.'
Step $debugWall .46 0
Assert ($debugWall.Cue -eq 'RuinWall') 'The debug point plays the same wall impact cue.'
Step $game 1.60 0
Assert ($game.RuinRetryState -eq 'Gate') 'Collision splits the wall into a door.'
Step $game 3.25 0
Assert ($game.RuinRetryState -eq 'Survey') 'Crossing the wall reveals the archive framework.'
$choiceSave = $game.Checkpoint
$refused = [Echoes.Journey]::new(17)
Assert ($refused.Restore($choiceSave) -and $refused.RuinRetryState -eq 'Gate') 'Restoring at the wall restarts the entrance without reopening the old replay.'
foreach ($room in $galleryOrder.Keys) {
    Assert ($refused.RuinDisciplineFor($room) -eq $galleryOrder[$room]) 'The saved route retains each exhibit assignment.'
}
$stand = $game.Distance
$ground = $game.Flight.Height
$game.RuinDirectPalmLook = $false
$game.SetRuinSteering($true, $false)
Step $game .50 0
Assert ($game.RuinLookYaw -lt -12 -and !$game.Touch(0)) 'A steers left and cannot accept the bargain early.'
$game.SetRuinSteering($false, $true)
Pose $game Fist .20 0
Pose $game Open .65 0
Assert ($game.RuinLookYaw -gt 0 -and $game.RuinLookPitch -gt 12) 'B and a fresh open palm turn right and tilt up.'
$game.SetRuinSteering($false, $false)
$beforeVoice = $game.RuinLookPitch
Step $game .72 .48
Assert ($game.RuinLookPitch -lt $beforeVoice - 12 -and $game.Distance -eq $stand -and
    $game.Flight.Height -eq $ground) 'Voice looks downward while the actor stays in place.'
Pose $refused Open 3.76 0
Assert ($refused.RuinRetryState -eq 'Survey' -and $refused.RuinLookPitch -gt 8) 'An already visible camera palm tilts upward when exploration begins.'
$beforeEnd = $game.Age
Step $game ([Echoes.Journey]::RuinSurveyDuration - $beforeEnd - .08) 0
Assert ($game.RuinRetryState -eq 'Survey') 'The player keeps camera controls through the ten-second window.'
Step $game .18 0
Assert ($game.RuinRetryState -eq 'Arrival') 'Only after the survey does the broker descend.'
Step $game ([Echoes.Journey]::RuinArrivalDuration - $game.Age + [Echoes.Journey]::RuinValuationFirstLineDuration - .4) 0
Assert ($game.RuinRetryState -eq 'Valuation' -and $game.Message.StartsWith('我会把')) 'The broker holds the first line for the full spoken introduction.'
Step $game ([Echoes.Journey]::RuinValuationDuration - $game.Age + .2) 0
$retainedCoins = if ($null -eq $game.RuinRetainedKind) { 0 } else { [Echoes.Journey]::RuinCoinValue($game.RuinRetainedKind) }
Assert ($game.RuinRetryState -eq 'Bargain' -and $game.RuinTokenCost -eq $game.RuinCoins - $retainedCoins) 'The exact toll reserves at most one organic artifact.'
Assert ($game.Touch(0) -and $game.RuinRetryState -eq 'Accepted' -and $game.RuinTradeCommitted -and !$game.Touch(1)) 'Consent commits once and starts the extraction scene.'
$acceptedSave = $game.Checkpoint
$acceptedReload = [Echoes.Journey]::new(92)
Assert ($acceptedReload.Restore($acceptedSave) -and $acceptedReload.RuinRetryState -eq 'Accepted' -and
    !$acceptedReload.Touch(0)) 'Loading an accepted trade resumes the exit without requesting consent again.'
Step $game 2.72 0
Assert ($game.RuinRevealedTokens -eq $game.RuinTokenCost -and $game.RuinRetryState -eq 'Accepted') 'Replacing the organs with device tokens settles the gap.'
Step $game 6.96 0
Assert ($game.RuinRetryState -eq 'Accepted') 'The accepted cinematic remains active for ten seconds.'
Step $game .42 0
Assert ($game.InStars -and $game.RuinRetryOutcome -eq 'Traded' -and $game.RuinResult -eq 'Explored') 'Accepting opens Star Sea automatically.'
Step $refused ([Echoes.Journey]::RuinSurveyDuration + [Echoes.Journey]::RuinArrivalDuration + [Echoes.Journey]::RuinValuationDuration + .2) 0
Assert ($refused.RuinRetryState -eq 'Bargain' -and $refused.Touch(1) -and !$refused.RuinTradeCommitted) 'Refusal is available even if the hand is out of frame.'
Assert ($refused.Message -eq '你的大脑和我比不过是玩具') 'Refusal begins with the broker addressing the player.'
Step $refused 2.2 0
Assert ($refused.Message -eq '你的心脏终有停跳的那一天') 'Refusal preserves the second line.'
Step $refused 2.66 0
Assert ($refused.RuinRetryState -eq 'Declined' -and $refused.Message -eq '何必如此执着呢') 'The refusal holds the slower last line before the forest handoff.'
Step $refused 5.3 0
Assert ($refused.InStars -and $refused.RuinRetryOutcome -eq 'Kept' -and !$refused.RuinTradeCommitted) 'Refusing waives the toll and reaches Star Sea.'
Pose $game Open .1 0
Assert ($game.Phase -eq 'StarsCanvas' -and $game.Started) 'Ruins lead directly into the open Star Sea.'
$game.SetHand([Echoes.Hand]::Missing)
$initial = $game.Distance
Assert ($game.Touch(0) -and $game.StarLastStamp -in 1..4) 'A immediately places one of its four original trails.'
$first = $game.StarLastStamp
Assert ($game.Touch(1) -and $game.StarLastStamp -in 5..8) 'B works without waiting for A or a gesture.'
Step $game 3.2 0
Assert ([Math]::Abs($game.Distance - $initial - 3.2 * $game.StarCruiseSpeed) -lt .02 -and $game.Phase -eq 'StarsCanvas') 'Motifs extend while the player cruises at the ordinary forward speed.'
Assert ($game.Touch(0) -and $game.StarLastStamp -in 1..4 -and $game.StarLastStamp -ne $first) 'Repeated A taps sample the same group without an immediate repeat.'
$game.SetStarPointer([Echoes.StarTip]::new(.38,.62))
Assert ($game.StarPointer.Valid -and $game.Gesture -eq 'Missing') 'The position of the index finger is independent from open-hand and fist tracking.'
Step $game 1 .30
$soft = $game.StarSpeed
Step $game 1 .78
Assert ($soft -gt 0 -and $game.StarSpeed -gt $soft -and $game.Distance -gt 1) 'Greater audio level drives deeper movement.'
$game.SetStarPointer([Echoes.StarTip]::new(2,2))
Assert (!$game.StarPointer.Valid) 'The pen can lose steering input independently of forward movement.'
Step $game .4 0
$beforeCruise = $game.Distance
Step $game 1 0
$cruiseDepth = $game.Distance
Assert ([Math]::Abs($cruiseDepth - $beforeCruise - $game.StarCruiseSpeed) -lt .015 -and $game.StarSpeed -eq $game.StarCruiseSpeed) 'Silence returns to continuous forward cruising.'
$saved = [Echoes.Journey]::new()
Assert ($saved.Restore($game.Checkpoint) -and $saved.Phase -eq 'StarsCanvas' -and $saved.Distance -eq $cruiseDepth -and
    $saved.Plants -eq 2 -and $saved.RuinResult -eq 'Explored' -and $saved.RuinRetryOutcome -eq 'Traded') 'Restore the existing route and precise Star Sea depth.'
Assert ($saved.Checkpoint.ruinFindsExtra -eq $choiceSave.ruinFindsExtra -and
    $saved.Checkpoint.ruinExhibitOrder -eq $choiceSave.ruinExhibitOrder) 'The Star Sea checkpoint preserves the Ruins exhibition history.'
$legacy = $game.Checkpoint
$legacy.version = 10
$legacy.ruinRetryOutcome = [int][Echoes.RuinRetryResolution]::Remembered
$legacy.starPaths = 21
$legacy.starMotifs = 1073
$legacy.complete = $true
Assert ($saved.Restore($legacy) -and $saved.Phase -eq 'StarsCanvas' -and $saved.Distance -eq 0 -and
    $saved.RuinRetryOutcome -eq 'Remembered') 'A legacy constellation opens in the free-drawing level.'

foreach ($seed in 1..40) {
    $draw = [Echoes.Journey]::new($seed)
    $draw.SelectChapter(2)
    Assert ($draw.Touch(0) -and $draw.StarLastStamp -in 1..4 -and $draw.Touch(1) -and $draw.StarLastStamp -in 5..8) 'Both randomized motif pools are available immediately in each run.'
}

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
        $corridors=$maze.Connections($at)
        $tunnel=$corridors | ForEach-Object { $_.Other($at) } | Where-Object { (ReachesWall $maze $at $_) } | Select-Object -First 1
        if ($null -ne $tunnel) {
            $checkpointX=$canopy.ForestMazeX
            $checkpointZ=$canopy.ForestMazeZ
            $marked=$true
            Aim-MazeExit $canopy $tunnel
        } else {
            Aim-MazeExit $canopy ($at + 1)
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
$from=$canopy.ForestMazeNode
$reverse=$maze.Connections($from)[0].Other($from)
Aim-MazeExit $canopy $reverse
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
$captureTime = $caught.ForestCaptureTime
$spendTime = [Echoes.Journey]::ForestRescueSpendAt * $captureTime / [Echoes.Journey]::ForestRescueDuration
Assert ($captureTime -lt 7) 'The flower rescue runs on its own shorter clock.'
Step $caught ($spendTime - .1) 0
Assert ($caught.ForestLights -eq $reserve -and $caught.Phase -eq 'ForestTrapped') 'The same fly struggles before it is spent.'
Step $caught .2 0
Assert ($caught.ForestLights -eq $reserve - 1 -and $caught.Cue -eq 'ForestRescueLight') 'Only the final rescue beat spends the companion.'
Step $caught ($captureTime - $caught.Age + .1) 0
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
Step $empty 2.15 0
Assert ($empty.Phase -eq 'ForestFalling') 'The shortened defeat still gives the spider enough time to finish its catch.'
Step $empty .42 0
Assert ($empty.Phase -eq 'ForestExplore' -and $empty.Distance -eq 0 -and $empty.ForestLights -eq 3 -and
    $empty.Checkpoint.forestSeed -ne $seed) 'Zero reserve restarts the forest with three lights and new hazard placements.'

$lastMaw=[Echoes.Journey]::new(100)
$lastMaw.SelectChapter(0)
$last=$lastMaw.Checkpoint
$last.forestReserve=1
Assert ($lastMaw.Restore($last)) 'Restore the flower scene with one light.'
$maw=$lastMaw.ForestFeatures | Where-Object Kind -eq Maw | Select-Object -First 1
FlyFlat $lastMaw $maw.Depth $maw.Altitude $maw.Lane
Step $lastMaw ($lastMaw.ForestCaptureTime + .04) 0
Assert ($lastMaw.Phase -eq 'ForestFalling' -and $lastMaw.ForestTrap -eq 'Maw' -and $lastMaw.ForestLights -eq 0) 'The flower rescue reaches its end before the last-light defeat starts.'
Step $lastMaw 2.53 0
Assert ($lastMaw.Phase -eq 'ForestExplore' -and $lastMaw.ForestLights -eq 3) 'The flower swallow also finishes on the shorter restart clock.'

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
Pose $resumed Fist .55 .30
Pose $resumed Open .76 .30
Assert ($resumed.ForestPlantCharge -gt .08 -and $resumed.Plants -eq 1) 'The upper root starts to charge while the player hovers in reach.'
Pose $resumed Open 2.9 0
Assert ($resumed.ForestPlantCharge -eq 0 -and $resumed.Flight.Height -lt .02 -and $resumed.ForestLights -eq 1) 'Silence cancels the hover and charge without spending the companion.'
Pose $resumed Fist .55 .30
Pose $resumed Open 1.7 .3
Assert ($resumed.ForestLights -eq 0 -and $resumed.Phase -eq 'ForestTransition' -and $resumed.ForestTrap -eq 'Web') 'One regular jump and a light voice complete the upper root, spend the last companion, and select the web ending.'
Step $resumed ($resumed.ForestTransitionTime - $resumed.Age - .16) 0
Assert ($resumed.Phase -eq 'ForestTransition') 'The spider still has a visible beat before the descent.'
Step $resumed .35 0
Assert ($resumed.Chapter -eq 1) 'The upper warp enters the same existing ruins.'

$mawEnding=[Echoes.Journey]::new(100)
$mawEnding.SelectChapter(0)
$mawSave=$mawEnding.Checkpoint
$mawSave.forestBranch=1
$mawSave.forestDepth=188
$mawSave.forestSites=3
$mawSave.forestRoute=1
$mawSave.forestReserve=0
Assert ($mawEnding.Restore($mawSave) -and $mawEnding.ForestTrap -eq 'Maw' -and $mawEnding.Phase -eq 'ForestTransition') 'A lower-root save restores the flower swallow.'
Step $mawEnding ($mawEnding.ForestTransitionTime + .2) 0
Assert ($mawEnding.Chapter -eq 1) 'The extended flower descent carries the character into the ruins.'

$jumpTuning = [Echoes.Journey]::new(100)
$jumpTuning.SelectChapter(0)
$jumpTuning.JumpHeightScale = .55
$jumpTuning.ForestDoorJumpHeightScale = 4
Launch-Gesture $jumpTuning
$lowLaunch = $jumpTuning.Flight.Speed
$jumpTuning.Flight.Reset(0)
$jumpTuning.JumpHeightScale = 1.65
Launch-Gesture $jumpTuning
Assert ([Math]::Abs($lowLaunch - 1.13 * [Math]::Sqrt(.55)) -lt .001 -and
    [Math]::Abs($jumpTuning.Flight.Speed / $lowLaunch - [Math]::Sqrt(3)) -lt .001) 'The ordinary setting multiplies peak ascent and ignores the right door setting in the forest trail.'

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
$doorTuning = [Echoes.Journey]::new(101)
Assert ($doorTuning.Restore($machine.Checkpoint)) 'The door tuning test starts at the saved ground floor.'
$doorTuning.JumpHeightScale = 2
$doorTuning.ForestDoorJumpHeightScale = .75
Launch-Gesture $doorTuning
Assert ([Math]::Abs($doorTuning.Flight.Speed - 1.13 * [Math]::Sqrt(.75)) -lt .001) 'At the machine doors, the separate height overrides the ordinary setting.'
Assert ($doorTuning.Restore($machine.Checkpoint)) 'An actual upper floor attempt can reuse the ground floor save.'
$doorTuning.JumpHeightScale = .50
$doorTuning.ForestDoorJumpHeightScale = [Echoes.Journey]::DefaultForestDoorJumpHeightScale
Launch-Gesture $doorTuning
Pose $doorTuning Open 1.08 0
Pose $doorTuning Fist .20 0
Pose $doorTuning Open 1.52 0
Assert ($doorTuning.Section -eq 'Brain' -and $doorTuning.ForestDoor -eq -1) 'Two quiet gestures from the ground reach the upper floor at the default door setting.'

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
Step $machine ($machine.ForestTransitionTime + .2) 0
Assert ($machine.Chapter -eq 1) 'The right branch also enters the existing ruins.'

$ruins=[Echoes.Journey]::new(92)
$ruins.SelectChapter(1)
Assert ($ruins.InRuins -and $ruins.RuinLayer -eq 0 -and $ruins.RuinVisitedCount -eq 1) 'The entrance is its own level with a threshold to restore.'
$ruins.SetHand([Echoes.Hand]::Open)
Step $ruins 75 0
Assert ($ruins.Distance -eq 0 -and $ruins.RuinForwardSpeed -eq 0 -and $ruins.Phase -eq 'RuinExplore') 'Idle exploration has no initial speed or ending countdown.'
$quietJump = [Echoes.Journey]::new(92)
$loudJump = [Echoes.Journey]::new(92)
foreach ($jump in @($quietJump, $loudJump)) {
    $jump.SelectChapter(1)
    RuinSteer $jump -1.30
    Launch-Gesture $jump
}
Step $quietJump 1 0
Step $loudJump 1 .7
Assert ($quietJump.Flight.Speed -lt 0 -and $quietJump.Distance -gt 2 -and $quietJump.RuinForwardSpeed -gt 4) 'A quiet hand release advances through the rising and gliding portions of the jump.'
Assert ($loudJump.Distance -gt $quietJump.Distance -and $loudJump.RuinForwardSpeed -gt $quietJump.RuinForwardSpeed) 'Voice boosts the same ruins jump.'
Step $quietJump 5 0
Assert ($quietJump.Flight.Height -eq 0 -and $quietJump.RuinForwardSpeed -eq 0 -and $quietJump.Phase -eq 'RuinExplore') 'A quiet jump settles back to a stop after landing.'
Step $ruins 1 .30
$quietSpeed = $ruins.RuinForwardSpeed
Step $ruins 1 .78
Assert ($quietSpeed -gt 0 -and $ruins.RuinForwardSpeed -gt $quietSpeed) 'Volume controls walking pace.'
Step $ruins .26 0
$rest = $ruins.Distance
Step $ruins 2 0
Assert ($ruins.Distance -eq $rest -and $ruins.RuinForwardSpeed -eq 0) 'Silence brings travel to a full stop.'
$ruins.SetRuinSteering($true, $false)
Step $ruins .29 0
$atRelease = $ruins.RuinLane
$ruins.SetRuinSteering($false, $false)
Step $ruins .13 0
Assert ($ruins.RuinLane -lt $atRelease - .075 -and $ruins.RuinSteering -lt 0) 'Releasing A preserves noticeable lateral inertia.'
Step $ruins 1.2 0
$side = $ruins.RuinLane
Assert ($side -lt -.32 -and $side -gt -.75 -and $ruins.Distance -eq $rest) 'Coasting settles between the old lanes without voice.'
Step $ruins .4 0
Assert ($ruins.RuinLane -eq $side) 'The lateral position is stable after braking.'
$ruins.SetRuinSteering($false, $true)
Step $ruins .65 0
$beforeReversal = $ruins.RuinLane
$ruins.SetRuinSteering($true, $false)
$ruins.Step(.02,0)
Assert ($ruins.RuinLane -gt $beforeReversal) 'The character first decelerates in the old direction when reversing.'
Step $ruins .26 0
Assert ($ruins.RuinSteering -lt 0) 'Reversal then flows into the new direction.'
$ruins.SetRuinSteering($false, $false)

$ruins.Retry()
$gate = $ruins.RuinPortals | Where-Object Gate -eq Palm
FlyRuin $ruins ($gate.Depth - 4.38) 0 0
$ruins.SetRuinSteering($true, $false)
$incoming = $ruins.RuinForwardSpeed
for ($i = 0; $i -lt 60 -and $ruins.Phase -eq 'RuinExplore'; $i++) { $ruins.Step(.02,.46) }
$ruins.SetRuinSteering($false,$false)
Assert ($ruins.RuinEntrance -eq $gate -and $ruins.Phase -eq 'RuinTransfer' -and $gate.Opened -and
    $ruins.RuinPassageStartForwardSpeed -eq $incoming -and $ruins.RuinPassageStartLateralSpeed -lt -.20 -and $ruins.Progress -eq 0) 'Holding A ahead of a left portal carries the existing movement into it.'
$sway = [Math]::Abs($ruins.RuinSteering)
Step $ruins .58 .46
Assert ($ruins.Progress -gt 0 -and [Math]::Abs($ruins.RuinSteering) -lt $sway) 'Lateral banking eases away along the branch.'
CrossRuin $ruins 0
Assert ($ruins.RuinRoomId -eq 1 -and $ruins.RuinRoomStyle -eq 2 -and $ruins.RuinVisitedCount -eq 2) 'A left turn continues into a distinct gallery without another command.'
$voiceGate = $ruins.RuinPortals | Where-Object Gate -eq Voice
FollowRuin $ruins $voiceGate
Assert ($ruins.RuinRoomId -eq 6 -and $ruins.RuinLayer -eq -2) 'A roof action also opens a stylized gate.'

$rightRoute = [Echoes.Journey]::new(92)
$rightRoute.SelectChapter(1)
$right = $rightRoute.RuinPortals | Where-Object Direction -eq Right
FlyRuin $rightRoute ($right.Depth - 5.10) 0 0
$rightRoute.RuinRightReach = .65
$rightRoute.SetRuinSteering($false,$true)
Step $rightRoute .13 0
Assert ($rightRoute.Phase -eq 'RuinExplore') 'A small right range does not capture a held B too early.'
$rightRoute.SetRuinSteering($false,$false)
$rightRoute.RuinRightReach = 1.65
Step $rightRoute .4 0
[void]$rightRoute.Touch(1)
Step $rightRoute .14 0
Assert ($rightRoute.Phase -eq 'RuinExplore' -and !$rightRoute.RuinEntrance) 'An old or momentary button event does not select the branch.'
$rightRoute.SetRuinSteering($false,$true)
$rightRoute.Step(.02,0)
$rightRoute.SetRuinSteering($false,$false)
Assert ($rightRoute.Phase -eq 'RuinTransfer' -and $rightRoute.RuinEntrance -eq $right -and
    $rightRoute.RuinPassageStartForwardSpeed -eq 0 -and $rightRoute.Progress -eq 0) 'A currently held B turns right from rest.'
$rightRoute.Step(.02, 0)
Assert ($rightRoute.Progress -gt 0 -and $rightRoute.Progress -lt .005) 'A stationary turn eases into movement.'
CrossRuin $rightRoute 0

foreach ($direction in 'Left','Right') {
    $airborne = [Echoes.Journey]::new(92)
    $airborne.SelectChapter(1)
    $sideGate = $airborne.RuinPortals | Where-Object Direction -eq $direction | Select-Object -First 1
    FlyRuin $airborne ($sideGate.Depth - .82) 0 0
    Pose $airborne Fist .21 0
    $airborne.Flight.Reset(1.42, [Echoes.Journey]::RuinHeightLimit)
    $airborne.Step(.02, .12)
    Assert ($airborne.Phase -eq 'RuinExplore') 'A high actor needs the matching held input to enter a side mouth.'
    $turnLeft = $direction -eq 'Left'
    if ($turnLeft) { $airborne.SetHand([Echoes.Hand]::Open) }
    $airborne.SetRuinSteering($turnLeft, !$turnLeft)
    $airborne.Step(.02, .12)
    $airborne.SetRuinSteering($false, $false)
    Assert ($airborne.Phase -eq 'RuinTransfer' -and $airborne.RuinEntrance -eq $sideGate -and
        $airborne.RuinPassageStartAltitude -gt 1.2 -and
        $(if ($turnLeft) { $airborne.RuinPassageStartVerticalSpeed -gt .20 } else { $airborne.RuinPassageStartVerticalSpeed -lt -.01 })) 'A held side input captures from high flight and keeps the direction of the vertical momentum.'
    CrossRuin $airborne
    Assert ($airborne.RuinRoomId -eq $sideGate.TargetRoom) 'The airborne side passage reaches the matching room.'
}

$visit = [Echoes.Journey]::new(92)
$visit.SelectChapter(1)
foreach ($destination in 2,12,2) {
    FollowRuin $visit ($visit.RuinPortals | Where-Object TargetRoom -eq $destination | Select-Object -First 1)
}
Assert ($visit.RuinRoomId -eq 2 -and $visit.RuinDiscipline -eq 1 -and $visit.RuinDisciplinesSeen -eq 3) 'Revisiting a tunnel keeps its discipline without advancing the sequence.'

$ceilingRoute=[Echoes.Journey]::new(92)
$ceilingRoute.SelectChapter(1)
$top = $ceilingRoute.RuinPortals | Where-Object { $_.Direction -eq 'Upper' -and $_.TargetRoom -eq 3 } | Select-Object -First 1
FlyRuin $ceilingRoute ($top.Depth - 2.5) 0 0
Pose $ceilingRoute Fist .22 0
Assert ($ceilingRoute.Phase -eq 'RuinExplore') 'A fist while near the upper passage does not select it.'
Pose $ceilingRoute Open .8 0
if ($ceilingRoute.Phase -eq 'RuinExplore') {
    Pose $ceilingRoute Fist .18 0
    Pose $ceilingRoute Open 1.2 0
}
Assert ($ceilingRoute.Phase -eq 'RuinTransfer' -and $ceilingRoute.RuinEntrance -eq $top -and
    $ceilingRoute.RuinPassageStartAltitude -ge .45) 'Real hand releases enter the upper passage automatically without pressing B.'
CrossRuin $ceilingRoute
Assert ($ceilingRoute.RuinLayer -eq 1) 'A ceiling branch leads to an independent upper floor.'

$earlyUpper = [Echoes.Journey]::new(92)
$earlyUpper.SelectChapter(1)
$earlyGate = $earlyUpper.RuinPortals | Where-Object Id -eq 80
Assert ($earlyGate -and $earlyGate.Direction -eq 'Upper' -and $earlyGate.Depth -lt 9 -and !$earlyGate.Hidden) 'The first hall has a visible upper route before the main junctions.'
FlyRuin $earlyUpper ($earlyGate.Depth - 1.75) 0 0
Pose $earlyUpper Fist .21 0
Pose $earlyUpper Open 1.18 0
Assert ($earlyUpper.Phase -eq 'RuinTransfer' -and $earlyUpper.RuinEntrance -eq $earlyGate) 'A natural hand release enters the new early upper route.'
CrossRuin $earlyUpper
Assert ($earlyUpper.RuinRoomId -eq 8 -and $earlyUpper.RuinLayer -eq 2) 'The new early upper route reaches an independent higher floor.'

$floorRoute = [Echoes.Journey]::new(92)
$floorRoute.SelectChapter(1)
$low = $floorRoute.RuinPortals | Where-Object Direction -eq Lower
FlyRuin $floorRoute ($low.Depth - 2.55) 0 .53
Step $floorRoute .40 0
Assert ($floorRoute.Phase -eq 'RuinExplore' -and $floorRoute.Flight.Speed -eq 0) 'Being on the floor at a lower entrance does not count as falling.'
$floorRoute.Flight.Reset(.47, [Echoes.Journey]::RuinHeightLimit)
Step $floorRoute .12 0
Assert ($floorRoute.Phase -eq 'RuinTransfer' -and $floorRoute.RuinEntrance -eq $low -and
    $floorRoute.RuinPassageStartLane -gt .3 -and $floorRoute.RuinPassageStartVerticalSpeed -lt 0) 'Descending into the lower range captures the passage from a loose lateral approach.'
CrossRuin $floorRoute

$circuit = [Echoes.Journey]::new(44)
$circuit.SelectChapter(1)
$circuit.SetHand([Echoes.Hand]::Open)
for ($sample = 0; $sample -lt 4000 -and $circuit.RuinLoops -eq 0; $sample++) { $circuit.Step(.02,.80) }
Assert ($circuit.RuinLoops -eq 1 -and $circuit.RuinRoomId -eq 0 -and $circuit.Phase -eq 'RuinExplore') 'Passing the seam continues the same room without forced transfer.'
$resumed = [Echoes.Journey]::new(99)
Assert ($resumed.Restore($circuit.Checkpoint) -and $resumed.RuinLoops -eq 1) 'Returning through a save retains completed circuits.'
$resumed.SetHand([Echoes.Hand]::Open)
for ($sample = 0; $sample -lt 4000 -and $resumed.RuinLoops -lt 2; $sample++) { $resumed.Step(.02,.80) }
Assert ($resumed.Phase -eq 'RuinExplore' -and $resumed.RuinLoops -eq 2 -and $resumed.RuinWallPassages -eq 0) 'Circling the main gallery never substitutes for a passage transfer.'

$heartbeat=[Echoes.Journey]::new(92)
$heartbeat.SelectChapter(1)
$heart = $heartbeat.RuinObjects | Where-Object Kind -eq Heart | Select-Object -First 1
FlyRuin $heartbeat ($heart.Depth - 1.7) $heart.Altitude 0
Assert (!$heart.Collected -and $heartbeat.RuinHeartAge -lt 0 -and $heart.Reveal -lt .2) 'From the center only a trace of an alcove is visible.'
RuinSteer $heartbeat $heart.Lane
Step $heartbeat .75 0
Assert ($heartbeat.Cue -eq 'RuinHeartbeat' -and $heartbeat.RuinHeartAge -ge 0 -and !$heart.Collected -and $heart.Reveal -gt .3) 'The first heart reveals and startles before contact.'
$heartSave=$heartbeat.Checkpoint
FlyRuin $heartbeat $heart.Depth $heart.Altitude $heart.Lane
Assert ($heartbeat.RuinHearts -eq 1 -and $heart.Collected -and $heartbeat.Cue -eq 'RuinCollect') 'Approaching the alcove collects the heart without a button.'
Assert ($heartbeat.Restore($heartSave)) 'The pre-pickup heart checkpoint restores.'
$heart = $heartbeat.RuinObjects | Where-Object Kind -eq Heart | Select-Object -First 1
FlyRuin $heartbeat ($heart.Depth - 1.36) $heart.Altitude $heart.Lane
Assert ($heartbeat.RuinHeartAge -lt 0 -and $heartbeat.Cue -ne 'RuinHeartbeat' -and !$heart.Collected) 'The scare stays consumed after a checkpoint restore.'
FlyRuin $heartbeat $heart.Depth $heart.Altitude $heart.Lane
Step $heartbeat .28 0
Assert ($heartbeat.RuinHearts -eq 1 -and $heartbeat.Cue -eq 'RuinCollect') 'A previously heard heart remains collectible without a second scare.'

$atlas = [Echoes.Journey]::new(77)
$atlas.SelectChapter(1)
$atlasSave = $atlas.Checkpoint
$passages = [System.Collections.Generic.Dictionary[int,int[]]]::new()
$directions = [System.Collections.Generic.HashSet[string]]::new()
$styles = [System.Collections.Generic.HashSet[int]]::new()
$depths = [System.Collections.Generic.HashSet[int]]::new()
$kinds = @{ Phone=0; Computer=0; Heart=0; Brain=0 }
$originalLengths = @(48,40,44,46,50,43,42,49,45,51,40,27,30,29,31,32)
for ($room=0; $room -lt 16; $room++) {
    $atlasSave.ruinRoom = $room
    Assert ($atlas.Restore($atlasSave)) 'Each authored room is valid as a saved starting threshold.'
    $scale = if ($room -in 0,9) { 1 } else { .5 }
    Assert ($atlas.RuinRoomLength -eq $originalLengths[$room] * $scale) 'The entrance and final gallery keep their lengths and ordinary galleries are halved.'
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
Assert (($kinds.Values | Measure-Object -Sum).Sum -eq 112) 'Seven side caches in each of 16 galleries yield 112 loose finds.'
Assert ($directions.Count -eq 4 -and $depths.Count -eq 7 -and $styles.Count -eq [Echoes.Journey]::RuinChestCount) 'The atlas has four voluntary entry directions, seven elevations and sixteen distinct chests.'
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
    if ($start -ne 9) { Assert ($reached.Contains(0)) 'Every ordinary room can return to the entrance hall through optional branches.' }
    else { Assert ($reached.Count -eq 1) 'The terminal room is a route to the wall with no divergent portals.' }
    if ($start -eq 0) { Assert ($reached.Count -eq 15 -and !$reached.Contains(9)) 'All ordinary galleries remain reachable and the terminal gallery waits for the last transfer.' }
}

$atlasSave.ruinRoom = 11
Assert ($atlas.Restore($atlasSave)) 'A hidden storage layer can be visited.'
$chest = $atlas.RuinObjects | Where-Object Kind -eq Chest
FlyRuin $atlas $chest.Depth $chest.Altitude $chest.Lane
Step $atlas .28 0
Assert ($atlas.RuinCanTake -and !$atlas.RuinLootOpen -and $atlas.RuinChests -eq 0) 'Stopping next to a chest leaves it closed until the player chooses.'
[void]$atlas.Touch(1)
Assert ($atlas.RuinLootDisplaySource -eq $chest -and $atlas.RuinChests -eq 1 -and $atlas.RuinChestOpening -and !$atlas.RuinLootOpen) 'B opens the chest and starts automatic pickup.'
Step $atlas 2.1 0
Assert ($atlas.RuinChestRemaining($chest) -eq 0 -and !$atlas.RuinLootOpen) 'A chest with capacity clears automatically without a choice panel.'

$atlasSave.ruinFinds = -1
$atlasSave.ruinRoom = 15
Assert ($atlas.Restore($atlasSave) -and $atlas.RuinInventory -eq [Echoes.Journey]::RuinCapacity -and
    $atlas.RuinChests -eq [Echoes.Journey]::RuinChestCount) 'Restoring a crowded inventory caps artifacts independently of the chests.'
$overflow = $atlas.RuinObjects | Where-Object { !$_.Collected -and $_.Kind -ne 'Chest' } | Select-Object -First 1
FlyRuin $atlas $overflow.Depth $overflow.Altitude $overflow.Lane
Step $atlas .28 0
Assert (!$overflow.Collected -and $atlas.RuinInventory -eq [Echoes.Journey]::RuinCapacity) 'Automatic pickup cannot exceed the capacity.'
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

foreach ($chapter in 0, 1) {
    $traveler = [Echoes.Journey]::new(81)
    $traveler.SelectChapter($chapter)
    Pose $traveler Open .25 0
    Pose $traveler Fist .35 .3
    $traveler.SetHand([Echoes.Hand]::Open)
    $quietRelease = [Echoes.SpiritFrame]::Read($traveler)
    Assert ($quietRelease.releaseAge -eq 0 -and $quietRelease.turnAge -lt 0) 'A quiet jump emits its ring without a spin.'
    Pose $traveler Fist .35 .7
    $traveler.SetHand([Echoes.Hand]::Open)
    $fastRelease = [Echoes.SpiritFrame]::Read($traveler)
    Assert ($fastRelease.releaseAge -eq 0 -and $fastRelease.turnAge -eq 0) 'An accelerated jump starts the ring and spin together in the Forest and Ruins.'
}

$starFlight = [Echoes.Journey]::new(83)
$starFlight.StarCruiseSpeed = 1.2
$starFlight.SelectChapter(2)
$starFlight.SetStarPointer([Echoes.StarTip]::new(.32,.59))
Step $starFlight 1 0
Assert ([Math]::Abs($starFlight.Distance - 1.2) -lt .015) 'Cruise tuning applies immediately upon entering Star Sea.'
$starFlight.SetStarPointer([Echoes.StarTip]::new(2,2))
Step $starFlight .6 .8
Assert ($starFlight.StarSpeed -gt $starFlight.StarCruiseSpeed -and ([Echoes.SpiritFrame]::Read($starFlight)).turnAge -lt 0 -and
    [Single]::IsNegativeInfinity($starFlight.LastRelease)) 'Sound accelerates Star Sea flight without a spin or release ring.'
$starFlight.StarCruiseSpeed = .1
Step $starFlight .8 0
Assert ($starFlight.StarSpeed -eq [Echoes.Journey]::MinimumStarCruiseSpeed) 'A live tuning change still preserves forward flight.'

$ending = [Echoes.Journey]::new(72)
$ending.StarCruiseSpeed = [Echoes.Journey]::MaximumStarCruiseSpeed
$ending.SelectChapter(2)
Step $ending 29 1
Assert (!$ending.StarFinaleActive -and $ending.Distance -lt [Echoes.Journey]::StarFinaleDistance) 'The finale waits for distance, including acceleration from cruise.'
Step $ending 2 1
Assert ($ending.StarFinaleActive -and [Math]::Abs($ending.Distance - 94.5) -lt .001 -and
    $ending.StarSpeed -eq 0 -and !$ending.Touch(0) -and !$ending.Touch(1)) 'At 30 seconds of maximum-speed equivalent distance the pen stops and the finale begins.'
$endingSave = $ending.Checkpoint
$restoredEnding = [Echoes.Journey]::new(72)
Assert ($restoredEnding.Restore($endingSave) -and $restoredEnding.StarFinaleActive -and
    $restoredEnding.StarFinaleAge -eq 0) 'A reached finale restarts from its first shot after restoring progress.'
Step $ending 18 0
Assert ($ending.Distance -eq [Echoes.Journey]::StarFinaleDistance -and
    $ending.StarFinaleAge -eq [Echoes.Journey]::StarFinaleDuration) 'The keepsake holds on screen until the player starts again.'
$ending.Retry()
Assert (!$ending.StarFinaleActive -and $ending.Distance -eq 0 -and $ending.Touch(1)) 'Retry opens a fresh Star Sea canvas.'

$touch = $enabled = 0
Assert ([Echoes.ExhibitProtocol]::ReadInputs('EOL3 IN 3 7', [ref]$touch, [ref]$enabled)) 'The serial input still accepts both contacts.'
Assert ($touch -eq 3 -and $enabled -eq 7) 'Both physical inputs remain distinct.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 1, .5, 200, $true) -eq "EOL3 IO 1 12 4 0 0 2 6 -1 166`n") 'Both lamps and bounded fan share the output packet.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 0, 0, 200, $false) -eq "EOL3 SET 1 0 0 0 0 0 0 -1`n") 'Lighting-only firmware still has a supported output.'
Assert ([Echoes.ExhibitProtocol]::Output(1, .5, .5, 0, $true, $true) -eq "EOL3 IO 1 6 4 0 6 0 0 -1 0`n") 'Forest EOL3 palette routes the fireflies to dim warm yellow and red lamps.'
Write-Output 'Passed forest controls, rescue, seeded maze, both roots, machine doors and upper gallery, layered ruins, broker choices, stars, checkpoints, projection and EOL3.'
