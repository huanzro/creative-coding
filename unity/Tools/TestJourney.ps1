$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'JourneyChapters.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
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

$game=[Echoes.Journey]::new()
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
Assert ($game.Phase -eq 'ForestRoute' -and $game.EnteredFromOpening) 'The opening should land in the first chapter.'

Pose $game Open 2.2 .3
Assert ($game.Plants -eq 1) 'Soft open hands should plant the first root.'
$time = $game.Time
$game.SetHand([Echoes.Hand]::Missing)
Step $game 2 .7
Assert ($game.Time -eq $time -and $game.Plants -eq 1) 'Losing a hand pauses timing and preserves earned light.'
Pose $game Open 2.0 .3
Assert ($game.Phase -eq 'ForestCrossing' -and $game.Plants -eq 2) 'Planting both lights should make the crossing available.'
$frame=[Echoes.SpiritFrame]::Read($game, 1)
Assert ($frame.cast -eq 1 -and $frame.fireflyResponse -eq 'Amber' -and $frame.fireflyPulse -gt 0) 'The projection receives the cast and root handoff together.'
[void]$game.Touch(0)
[void]$game.Touch(1)
Step $game 2.5 0
Assert ($game.Phase -eq 'Complete') 'B can lend one crossing, then the forest ends normally.'
[void]$game.Touch(0)
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

$wind=[Echoes.Journey]::new()
$wind.SelectChapter(0)
Pose $wind Open .07 0
[void]$wind.Touch(1)
Assert ($wind.Phase -eq 'ForestRoute' -and $wind.HintReady) 'A raw B press first exposes the available shortcut.'
[void]$wind.Touch(1)
Assert ($wind.Phase -eq 'ForestWind') 'The next press enters the high route.'
Step $wind 1.6 0
Assert ($wind.GustWarning -gt .25) 'The warning must appear before impact.'
Pose $wind Fist .62 0
Pose $wind Open 4.2 0
Assert ($wind.Phase -eq 'ForestPlant' -and $wind.Plants -eq 1) 'One shield and one miss should return the player to a recoverable root.'
Step $wind 4.0 .3
Assert ($wind.Plants -eq 2) 'The lower route should allow a missed light to be replaced.'

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
Write-Output 'Passed the entire four-part Unity route, both forest routes, ruin repair/arcade/assist, star withdrawal and correction, checkpoint, flight, projection and EOL3.'
