$ErrorActionPreference = 'Stop'
$sources = 'Journey.cs', 'AtmosphereMotion.cs', 'ExhibitProtocol.cs', 'SpiritFrame.cs', 'OpeningTiming.cs', 'SpiritFlight.cs', 'FireflyCourse.cs' |
    ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }
Add-Type -Path $sources

function Assert($condition, $message) {
    if (!$condition) { throw $message }
}

function Step($game, $seconds, $voice) {
    for ($i = 0; $i -lt [math]::Ceiling($seconds * 50); $i++) {
        $game.Step(.02, $voice)
    }
}

function Hand($game, $pose, $seconds, $voice) {
    $game.SetHand([Echoes.Hand]$pose)
    Step $game $seconds $voice
}

$game = [Echoes.Journey]::new()
Hand $game Fist 1 0
Assert ($game.Time -eq 0) 'A fist before an open hand must not start the game.'
Hand $game Open .7 0
Hand $game Fist .7 0
Hand $game Open .9 .3
Assert ($game.Lesson -eq 1) 'The first lesson should advance on release.'
Hand $game Fist .9 .7
Hand $game Open .1 0
Assert ($game.Lesson -eq 2) 'The voice lesson should accept the two ranges.'
[void]$game.Touch(0)
Assert ($game.Phase -eq 'Prologue' -and $game.OpeningTime -eq 0) 'Finishing the practice should begin the opening.'
$game.SetHand([Echoes.Hand]::Fist)
$game.Step(.5, .8, $false)
Assert ($game.OpeningTime -gt 0 -and $game.Gesture -eq 'Missing' -and !$game.Touch(1)) 'The opening plays through a lost input, with stage controls held.'
Step $game 38 0
Assert ($game.Phase -eq 'Gather' -and $game.Chapter -eq 0 -and $game.Lesson -eq -1) 'The opening must enter the actual first chapter, bypassing practice.'
Hand $game Open .05 .3

for ($round = 0; $round -lt 3; $round++) {
    Step $game 8 .3
    Assert ($game.Phase -eq 'Fork') 'Soft voice should attract the fireflies.'
    [void]$game.Touch(0)
    Hand $game Fist .35 .3
    Hand $game Open 11 .3
    Step $game 1.5 .3
    Assert ($game.Round -eq ($round + 1)) 'A successful escort should unlock the next round.'
}
Assert ($game.Phase -eq 'Complete') 'Forest should end after three escorts.'
[void]$game.Touch(0)
Assert ($game.Chapter -eq 1) 'The next choice should advance to the ruins.'

for ($round = 0; $round -lt 3; $round++) {
    Hand $game Open .1 .5
    [void]$game.Touch(0)
    for ($slot = 0; $slot -lt $round + 2; $slot++) {
        $pose = if ($slot % 2 -eq 0) { 'Open' } else { 'Fist' }
        Hand $game $pose 2.02 .5
    }
    Assert ($game.Phase -eq 'ReplayReady') 'Two distinct gestures should create an echo.'
    [void]$game.Touch(1)
    if ($round -eq 0) {
        Hand $game Open 2.04 .5
        Assert ($game.Phase -eq 'ReplayFailed' -and $game.Recording.Count -eq 2) 'A replay failure must preserve the recorded gestures.'
        [void]$game.Touch(1)
    }
    for ($slot = 0; $slot -lt $round + 2; $slot++) {
        Hand $game $game.RequiredHand 2.04 .5
    }
    Step $game 2.6 .5
    Assert ($game.Round -eq ($round + 1)) 'Complementary gestures and voice should open the gate.'
}
[void]$game.Touch(0)
Assert ($game.Chapter -eq 2) 'The next choice should advance to the stars.'

$responded = 0
while ($game.Phase -eq 'Wave' -and $responded -lt 7) {
    Hand $game Open .06 0
    $amplitude = if ($game.Storm) { $game.IncomingAmplitude } else { 1 - $game.IncomingAmplitude }
    $phase = if ($game.Storm) { 1 } else { 0 }
    [void]$game.Touch($phase)
    Hand $game Fist .9 $amplitude
    Assert ($game.Aligned) 'The opposite or reinforcing wave should be in the target range.'
    Hand $game Open 3.6 0
    $responded++
}
Assert ($responded -eq 7 -and $game.Phase -eq 'Compose') 'All seven waves should reach free composition.'
for ($word = 0; $word -lt 3; $word++) {
    Hand $game Fist .6 .65
    Hand $game Open .05 0
}
Assert ($game.Phase -eq 'Answer') 'Three composed waves should cause the finale.'
Step $game 6.1 0
Assert ($game.Phase -eq 'Complete' -and $game.Score -eq 2700) 'The complete journey should score all nine rounds.'

$saved = [Echoes.Journey]::new()
$saved.Restore(2, 1, -1, 2100)
Assert ($saved.Chapter -eq 2 -and $saved.Round -eq 1 -and $saved.Phase -eq 'Wave') 'A checkpoint should restore the correct round.'
$time = $saved.Time
$saved.Step(.03, .65)
Assert ($saved.Time -eq $time) 'Missing input should pause the restored game.'
$saved.SetHand([Echoes.Hand]::Open)
Step $saved .1 0
$saved.SetHand([Echoes.Hand]::Fist)
Step $saved .4 .7
$release = $saved.LastRelease
Hand $saved Missing .5 .65
Assert ($saved.Time -gt $time -and $saved.Held -eq 0 -and $saved.Charge -eq 0) 'Losing the hand must continue a started journey without retaining charge.'
Assert ($saved.LastRelease -eq $release -and !$saved.HasResponse) 'Hand loss must not trigger a release.'
$saved.SetHand([Echoes.Hand]::Fist)
Assert ($saved.Gesture -eq 'Missing') 'A returning hand must open before rearming.'

$dancer = [Echoes.Journey]::new()
Hand $dancer Open .65 0
Hand $dancer Fist .06 0
$dancer.SetHand([Echoes.Hand]::Open)
Assert (([Echoes.SpiritFrame]::Read($dancer)).releaseAge -eq -1) 'A brief interruption in tracking must not launch the character.'
Hand $dancer Fist .65 .3
$dancer.SetHand([Echoes.Hand]::Open)
$pose = [Echoes.SpiritFrame]::Read($dancer)
Assert ($pose.releaseAge -eq 0 -and $pose.turnAge -eq -1) 'A slow release should emit a ring without spinning.'
Hand $dancer Fist .7 .7
$dancer.SetHand([Echoes.Hand]::Open)
$pose = [Echoes.SpiritFrame]::Read($dancer)
Assert ($pose.releaseAge -eq 0 -and $pose.turnAge -eq 0) 'An accelerated charged release should start one turn alongside the ring.'
$turn = $dancer.LastSpin
Hand $dancer Fist .45 .7
$dancer.SetHand([Echoes.Hand]::Open)
Assert ($dancer.LastSpin -eq $turn) 'A nearby second release must not restart the facing mid-turn.'
Step $dancer 1.65 .7
Assert (([Echoes.SpiritFrame]::Read($dancer)).turnAge -eq -1) 'The turn should finish automatically while travel continues.'
$dancer.SelectChapter(2)
Assert (([Echoes.SpiritFrame]::Read($dancer)).releaseAge -eq -1) 'A different chapter must not inherit the previous ring.'
$pose = [Echoes.SpiritFrame]::Read($dancer)
Assert ($pose.touchAge -eq -1 -and $pose.hand -eq [Echoes.Hand]::Missing) 'A new chapter must start without a stale touch or gesture.'
Hand $dancer Open .12 .3
Assert ($dancer.Touch(1)) 'Touch B should reach the same journey input as its keyboard simulation.'
$pose = [Echoes.SpiritFrame]::Read($dancer)
Assert ($pose.touchAge -eq 0 -and $pose.touch -eq 1 -and $pose.voice -gt .1) 'The projection should carry touch and voice with the pose.'
Hand $dancer Fist .5 .7
Hand $dancer Missing .65 0
$pose = [Echoes.SpiritFrame]::Read($dancer)
Assert ($pose.hand -eq [Echoes.Hand]::Missing -and $pose.releaseAge -eq -1 -and $pose.touchAge -eq -1) 'Losing sight of the hand must settle the avatar without a release pose.'
$gameTime = $dancer.Time
$dancer.Step(.2, 0, $false)
Assert ($dancer.Time -eq $gameTime -and $dancer.PoseTime -gt $pose.motionTime) 'When the device is unavailable, the pose may settle without advancing the chapter.'

$flight = [Echoes.SpiritFlight]::new()
$flight.Reset(.11)
$flight.Release(.3)
for ($i=0; $i -lt 18; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Height -gt .36 -and $flight.Speed -gt 0) 'A release must visibly lift the character.'
for ($i=0; $i -lt 72; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Speed -lt -.1 -and $flight.Glide -gt .6) 'An open hand should glide through the downward phase.'
for ($i=0; $i -lt 400; $i++) { $flight.Step(.02,[Echoes.Hand]::Open) }
Assert ($flight.Height -eq 0 -and $flight.Glide -lt .01 -and [Echoes.SpiritFlight]::ScreenY($flight.Height) -gt .77) 'The glide must settle near the bottom rather than bounce.'

$flies = [Echoes.FireflyCourse]::new(17)
for ($i=0; $i -lt 129; $i++) { [void]$flies.Step(.02,.3,.27,[Echoes.Hand]::Open,$false) }
Assert ($flies.Kind -eq 'None') 'Allow initial breathing room before the first firefly.'
$last = 'None'
$appearances = 0
$amber = $scarlet = 0
for ($i=0; $i -lt 4000; $i++) {
    $active = $flies.Kind.ToString()
    if ($active -ne 'None' -and $last -eq 'None') {
        $appearances++
        if ($active -eq 'Scarlet') { $scarlet++ }
    }
    $last = $active
    $height = if ($active -eq 'Scarlet') { $flies.Altitude } else { .27 }
    $result = $flies.Step(.02,.3,$height,[Echoes.Hand]::Open,$false)
    if ($result -eq 'Amber') { $amber++ }
}
Assert ($appearances -ge 5 -and $appearances -lt 18 -and $amber -gt 0 -and $scarlet -gt 0) 'A seeded run should offer sparse yellow and red encounters with catchable yellow.'

$projection = [Echoes.Journey]::new()
$projection.SelectChapter(0)
for ($i = 0; $i -lt 500 -and $projection.Fireflies.Pulse -eq 0; $i++) {
    [void]$projection.Fireflies.Step(.02, .3, .27, [Echoes.Hand]::Open, $false)
}
$handoff = [Echoes.SpiritFrame]::Read($projection, 1)
Assert ($handoff.cast -eq 1 -and $handoff.fireflyResponse -eq 'Amber' -and $handoff.fireflyPulse -gt 0) 'Both displays should get the same cast and live firefly handoff.'
$projection.SelectChapter(1)
$handoff = [Echoes.SpiritFrame]::Read($projection, 1)
Assert ($handoff.cast -eq 1 -and $handoff.fireflyPulse -eq 0) 'A chapter change must clear the old fly without changing the cast.'

$skip = [Echoes.Journey]::new()
$skip.PlayOpening()
$skip.FinishOpening()
Assert ($skip.Phase -eq 'Gather' -and $skip.EnteredFromOpening -and $skip.Flight.Height -lt .1) 'Skipping enters the same forest arrival position.'

$noHand = [Echoes.Journey]::new()
$noHand.SelectChapter(1)
Hand $noHand Open .1 .4
[void]$noHand.Touch(0)
Hand $noHand Missing 2.1 .4
Assert ($noHand.Phase -eq 'RecordReady' -and $noHand.Recording.Count -eq 0) 'A time slot without a hand must not enter the recording.'

$openMotion = [Echoes.AtmosphereMotion]::new()
$fistMotion = [Echoes.AtmosphereMotion]::new()
$quietMotion = [Echoes.AtmosphereMotion]::new()
for ($i = 0; $i -lt 100; $i++) {
    $openMotion.Step(.02, [Echoes.Hand]::Open, .8, $true, 1)
    $fistMotion.Step(.02, [Echoes.Hand]::Fist, .8, $true, 1)
    $quietMotion.Step(.02, [Echoes.Hand]::Open, 0, $true, 1)
}
Assert ($openMotion.Wake -gt $fistMotion.Wake -and $openMotion.Drift -gt $fistMotion.Drift) 'A fist should dim and slow the background.'
Assert ($openMotion.Drift -gt $quietMotion.Drift) 'Voice should make visual parallax faster.'

$touch = $enabled = 0
Assert ([Echoes.ExhibitProtocol]::ReadInputs('EOL3 IN 3 7', [ref]$touch, [ref]$enabled)) 'The shared EOL3 connection must parse touch inputs.'
Assert ($touch -eq 3 -and $enabled -eq 7) 'The two touch channels must remain distinct.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 1, .5, 200, $true) -eq "EOL3 IO 1 12 4 0 0 2 6 -1 166`n") 'Combined firmware should receive the two lamps and bounded fan in one packet.'
Assert ([Echoes.ExhibitProtocol]::Output(1, 0, 0, 200, $false) -eq "EOL3 SET 1 0 0 0 0 0 0 -1`n") 'Old EOL3 firmware must receive the compatible command.'
Write-Output 'Unity C# passed practice, opening handoff, flight, sparse fireflies, all three chapters, and EOL3.'
