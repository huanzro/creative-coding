$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs', 'JourneyRuins.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'RuinAtlas.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
    ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }
Add-Type -Path $sources

function Assert($condition, $message) {
    if (!$condition) { throw $message }
}

function New-Maze($seed, $speed) {
    $journey = [Echoes.Journey]::new($seed)
    $journey.SelectChapter(0)
    $checkpoint = $journey.Checkpoint
    $checkpoint.forestBranch = 1
    $checkpoint.forestDepth = 112
    $checkpoint.forestLane = -1
    $checkpoint.forestSeed = $seed
    Assert ($journey.Restore($checkpoint)) 'The saved left approach loads.'
    $journey.MazeWalkScale = $speed
    $journey.SetHand([Echoes.Hand]::Open)
    for ($tick = 0; $tick -lt 300 -and !$journey.ForestMazeActive; $tick++) {
        $journey.Flight.Reset(.5, $journey.ForestHeightLimit)
        $journey.Step(.02, .3)
    }
    Assert ($journey.ForestMazeActive) 'The approach reaches the maze.'
    $journey.Flight.Reset(0)
    return $journey
}

function Get-SignedTurn($from, $to) {
    return (($to - $from + 540) % 360) - 180
}

function Get-ExitHeading($node, $exit) {
    return [Math]::Atan2($exit.X - $node.X, $exit.Z - $node.Z) * 180 / [Math]::PI
}

$slow = New-Maze 541 .65
$fast = New-Maze 541 2
for ($tick = 0; $tick -lt 100; $tick++) {
    $slow.Step(.02, .65)
    $fast.Step(.02, .65)
}
Assert ($slow.ForestMazeTravel -gt 2 -and $fast.ForestMazeTravel -gt 2 * $slow.ForestMazeTravel) 'The speed setting changes grounded travel at the same sound level.'
$fast.MazeWalkScale = 20
Assert ($fast.MazeWalkScale -eq [Echoes.Journey]::MaximumMazeWalkScale) 'The speed setting clamps the upper extreme.'
$fast.MazeWalkScale = -20
Assert ($fast.MazeWalkScale -eq [Echoes.Journey]::MinimumMazeWalkScale) 'The speed setting clamps the lower extreme.'
$fast.MazeWalkScale = [single]::NaN
Assert ($fast.MazeWalkScale -eq [Echoes.Journey]::MinimumMazeWalkScale) 'An invalid speed preserves the current setting.'

$turned = @(0, 0)
foreach ($seed in 541..547) {
    for ($button = 0; $button -lt 2; $button++) {
        $maze = New-Maze $seed 1.30
        for ($tick = 0; $tick -lt 800 -and $maze.MazeMotion -ne 'Choosing'; $tick++) { $maze.Step(.02, .65) }
        Assert ($maze.MazeMotion -eq 'Choosing') 'The initial junction waits for a choice.'

        $node = $maze.ForestMaze.Nodes[$maze.ForestMazeNode]
        $left = $maze.ForestMaze.Nodes[$maze.ForestMazeLeft]
        $right = $maze.ForestMaze.Nodes[$maze.ForestMazeRight]
        $facing = $maze.ForestMazeHeading
        $leftAngle = Get-SignedTurn $facing (Get-ExitHeading $node $left)
        $rightAngle = Get-SignedTurn $facing (Get-ExitHeading $node $right)
        Assert ($leftAngle -lt $rightAngle) 'A and B follow the visible opening order.'
        $exit = if ($button -eq 0) { $left } else { $right }
        $heading = Get-ExitHeading $node $exit
        $travelBeforeTurn = $maze.ForestMazeTravel
        Assert ($maze.Touch($button) -and $maze.ForestMazeTarget -eq $exit.Id) 'The input selects the intended physical passage.'

        if ([Math]::Abs((Get-SignedTurn $facing $heading)) -gt 1) {
            Assert ($maze.MazeMotion -eq 'FacingPassage') 'A nonstraight entrance starts a stationary turn.'
            $maze.Step(.12, 1)
            Assert ([Math]::Abs((Get-SignedTurn $facing $maze.ForestMazeHeading)) -gt .01 -and
                [Math]::Abs((Get-SignedTurn $maze.ForestMazeHeading $heading)) -gt .5) 'The actor turns progressively toward the opening.'
            for ($tick = 0; $tick -lt 60 -and $maze.MazeMotion -eq 'FacingPassage'; $tick++) {
                Assert ($maze.ForestMazeTravel -eq $travelBeforeTurn -and $maze.ForestMazeSpeed -eq 0) 'Sound cannot move the actor sideways through a junction during the turn.'
                $maze.Step(.02, 1)
            }
            Assert ($maze.MazeMotion -eq 'Walking' -and $maze.ForestMazeTravel -eq $travelBeforeTurn -and
                [Math]::Abs((Get-SignedTurn $maze.ForestMazeHeading $heading)) -lt .01) 'The actor finishes facing the physical entrance before stepping.'
            $turned[$button]++
        }

        $maze.Step(.02, .65)
        Assert ($maze.ForestMazeTravel -gt $travelBeforeTurn) 'Once aligned, sound advances into the chosen passage.'
        $stepX = $maze.ForestMazeX - $node.X
        $stepZ = $maze.ForestMazeZ - $node.Z
        Assert (($stepX * ($exit.X - $node.X) + $stepZ * ($exit.Z - $node.Z)) -gt 0) 'The movement vector points into the selected opening.'
    }
}
Assert ($turned[0] -gt 0 -and $turned[1] -gt 0) 'The suite exercises stationary turns in both directions.'

$privateProperty = [System.Reflection.BindingFlags]'Instance,Public,NonPublic'
$distance = [Echoes.Journey].GetProperty('Distance', $privateProperty)
$lane = [Echoes.Journey].GetProperty('ForestLane', $privateProperty)
foreach ($kind in @('Web','Maw')) {
    foreach ($wide in @($false,$true)) {
        $trial=[Echoes.Journey]::new(541)
        $trial.SelectChapter(0)
        $trial.SetHand([Echoes.Hand]::Open)
        $trial.ForestWebRange=if ($kind -eq 'Web' -and $wide) { 1.65 } else { .45 }
        $trial.ForestMawRange=if ($kind -eq 'Maw' -and $wide) { 1.65 } else { .45 }
        $trap=$trial.ForestFeatures | Where-Object Kind -eq $kind | Select-Object -First 1
        $distance.SetValue($trial,[single]($trap.Depth-8))
        $trial.Flight.Reset(.42)
        $trial.Step(.02,0)
        Assert (!$trap.Collected -and $trial.ForestThreatPulse -eq 0) 'From a distance the full trap stays hidden.'

        $distance.SetValue($trial,[single]$trap.Depth)
        $trial.Flight.Reset(.46)
        $trial.Step(.02,0)
        Assert (!$trap.Collected -and $trial.ForestThreatPulse -eq 0 -and $trial.Phase -eq 'ForestExplore') 'Passing at a safe height never wakes a predator.'

        $distance.SetValue($trial,[single]($trap.Depth+.52))
        $lane.SetValue($trial,[single]($trap.Lane+.27))
        $trial.Flight.Reset($trap.Altitude)
        $trial.Step(.02,0)
        Assert ($trap.Collected -eq $wide -and ($trial.ForestThreatPulse -eq 1) -eq $wide -and
                ($trial.Phase -eq 'ForestTrapped') -eq $wide) 'A scare and a committed capture happen on the same tick, only within the configured range of that predator.'
        if ($wide) {
            $trial.Touch(0) | Out-Null
            $trial.SetHand([Echoes.Hand]::Fist)
            $trial.Step(.02,1)
            Assert ($trial.Phase -eq 'ForestTrapped' -and $trial.ForestThreatPulse -eq 1 -and
                    $trial.ForestTrap -eq $kind) 'Steering away after the scare cannot leave a harmless visible predator.'
        }
    }
}
Write-Output 'Passed configurable maze pace, oriented turns, committed hidden traps and independent capture ranges.'
