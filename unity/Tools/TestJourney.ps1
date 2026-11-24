$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Assets/Scripts/Journey.cs')

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
Hand $game Fist .35 0
Hand $game Open .02 0
Assert ($game.Phase -eq 'Gather') 'Route confirmation should enter the forest.'

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
Write-Output 'Unity C# journey passed all three chapters, including retry and checkpoint restore.'
