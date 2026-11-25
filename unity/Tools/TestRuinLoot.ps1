$ErrorActionPreference = 'Stop'
if (-not ('Echoes.Journey' -as [type])) {
    $lootSources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyStars.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs',
        'JourneyRuins.cs', 'RuinAtlas.cs', 'JourneyRuinLoot.cs', 'JourneyRuinRetry.cs', 'OpeningTiming.cs',
        'SpiritFlight.cs', 'FireflyCourse.cs' | ForEach-Object { Join-Path $PSScriptRoot "../Assets/Scripts/$_" }
    Add-Type -Path $lootSources
}

function AssertRuinLoot($condition, $message) {
    if (!$condition) { throw $message }
}

function InvokeRuinFixture($game, $name, [object[]]$arguments) {
    $instance = [Reflection.BindingFlags]'Instance, NonPublic'
    $values = [object[]]::new($arguments.Count)
    for ($index = 0; $index -lt $arguments.Count; $index++) { $values[$index] = $arguments[$index].PSObject.BaseObject }
    [Echoes.Journey].GetMethod($name, $instance).Invoke($game, $values)
}

function RuinFixtureObjects($game) {
    $instance = [Reflection.BindingFlags]'Instance, NonPublic'
    foreach ($room in [Echoes.Journey].GetField('ruinRooms', $instance).GetValue($game)) {
        $room.Objects
    }
}

function SelectRuinChest($game) {
    [void](InvokeRuinFixture $game 'EnterRuinRoom' @(1, $false))
    $game.SetHand([Echoes.Hand]::Open)
    $chest = $game.RuinObjects | Where-Object Kind -eq 'Chest'
    [void][Echoes.Journey].GetProperty('Distance').GetSetMethod($true).Invoke($game, @([float]$chest.Depth))
    [void][Echoes.Journey].GetProperty('RuinLane').GetSetMethod($true).Invoke($game, @([float]$chest.Lane))
    AssertRuinLoot (InvokeRuinFixture $game 'OfferRuinLoot' @($chest)) 'Found chest should open the loot menu.'
    $chest
}

$lootGame = [Echoes.Journey]::new(4412)
$lootGame.SelectChapter(1)
$allFinds = @(RuinFixtureObjects $lootGame)
$heart = $allFinds | Where-Object Kind -eq 'Heart' | Select-Object -First 1
InvokeRuinFixture $lootGame 'RecordRuinPickup' @($heart)
foreach ($device in ($allFinds | Where-Object Kind -in 'Phone','Computer' | Select-Object -First 13)) {
    InvokeRuinFixture $lootGame 'RecordRuinPickup' @($device)
}
AssertRuinLoot ($lootGame.RuinInventory -eq 14) 'Fixture should have one free slot and one swappable heart.'
foreach ($foundChest in ($allFinds | Where-Object Kind -eq 'Chest')) {
    $size = $lootGame.RuinChestItemCount($foundChest)
    AssertRuinLoot ($size -ge 2 -and $size -le 4) 'Every chest should stock two to four devices.'
}

$lootChest = SelectRuinChest $lootGame
$stock = $lootGame.RuinChestRemaining($lootChest)
AssertRuinLoot ($lootGame.RuinLootDevices.Count -eq $stock -and $lootGame.RuinChests -eq 1) 'Opening detects the chest without collecting its devices.'
AssertRuinLoot (@($lootGame.RuinLootDevices | Where-Object Kind -notin 'Phone','Computer').Count -eq 0) 'Chests should contain only phones and computers.'
[void]$lootGame.Touch(1)
AssertRuinLoot ($lootGame.RuinInventory -eq 15 -and $lootGame.RuinHearts -eq 1 -and $lootGame.RuinChestRemaining($lootChest) -eq $stock - 1) 'Take the first device into the available slot.'
[void]$lootGame.Touch(1)
AssertRuinLoot ($lootGame.RuinLootSwap -and $lootGame.RuinLootChoices.Count -eq 2 -and $lootGame.RuinHearts -eq 1) 'An overflow waits for a valid organ choice and explicit confirmation.'
[void]$lootGame.Touch(0)
[void]$lootGame.Touch(1)
AssertRuinLoot (!$lootGame.RuinLootSwap -and $lootGame.RuinHearts -eq 1 -and $lootGame.RuinChestRemaining($lootChest) -eq $stock - 1) 'Cancelling changes neither memory nor stock.'
[void]$lootGame.Touch(1)
[void]$lootGame.Touch(1)
AssertRuinLoot ($lootGame.RuinInventory -eq 15 -and $lootGame.RuinHearts -eq 0 -and $lootGame.RuinPhones + $lootGame.RuinComputers -eq 15) 'A confirmed swap exchanges exactly one organ for one device.'
AssertRuinLoot ($lootGame.RuinChestRemaining($lootChest) -eq $stock - 2) 'Only two confirmed pickups change the stock.'
if ($lootGame.RuinLootOpen) {
    [void]$lootGame.Touch(1)
    AssertRuinLoot (!$lootGame.RuinLootSwap -and $lootGame.RuinChestRemaining($lootChest) -eq $stock - 2) 'Fifteen locked devices cannot be discarded or exceeded.'
    for ($i=0; $i -lt $lootGame.RuinLootChoices.Count - 1; $i++) { [void]$lootGame.Touch(0) }
    [void]$lootGame.Touch(1)
}
AssertRuinLoot (!$lootGame.RuinLootOpen -and !(InvokeRuinFixture $lootGame 'OfferRuinLoot' @($lootChest))) 'Leaving does not reopen the same chest immediately.'
if ($stock -gt 2) {
    [void](SelectRuinChest $lootGame)
    AssertRuinLoot ($lootGame.RuinLootDevices.Count -eq $stock - 2 -and $lootGame.RuinChests -eq 1) 'A later visit offers exactly the devices left in the chest.'
}

$lootReload = [Echoes.Journey]::new(995)
AssertRuinLoot ($lootReload.Restore($lootGame.Checkpoint)) 'A stock checkpoint should load.'
$reloadChest = $lootReload.RuinObjects | Where-Object Kind -eq 'Chest'
AssertRuinLoot ($lootReload.RuinInventory -eq 15 -and $lootReload.RuinHearts -eq 0 -and $lootReload.RuinChestItemCount($reloadChest) -eq $stock -and
    $lootReload.RuinChestRemaining($reloadChest) -eq $stock - 2) 'A different process seed must restore the same inventory, stock and released memory.'

$looseGame = [Echoes.Journey]::new(88932)
$looseGame.SelectChapter(1)
$looseGame.SetHand([Echoes.Hand]::Open)
$looseFinds = @(RuinFixtureObjects $looseGame)
$looseBrain = $looseFinds | Where-Object Kind -eq 'Brain' | Select-Object -First 1
$looseDevices = @($looseFinds | Where-Object Kind -in 'Phone','Computer')
InvokeRuinFixture $looseGame 'RecordRuinPickup' @($looseBrain)
for ($i=1; $i -le 14; $i++) { InvokeRuinFixture $looseGame 'RecordRuinPickup' @($looseDevices[$i]) }
AssertRuinLoot (InvokeRuinFixture $looseGame 'OfferRuinLoot' @($looseDevices[0])) 'A loose device uses the same swap rules.'
[void]$looseGame.Touch(1)
[void]$looseGame.Touch(1)
AssertRuinLoot (!$looseGame.RuinLootOpen -and $looseGame.RuinBrains -eq 0 -and $looseGame.RuinInventory -eq 15 -and $looseDevices[0].Collected) 'Confirming a loose-device swap records a locked pickup and releases only the chosen brain.'

$expandedGame = [Echoes.Journey]::new(22371)
$expandedGame.SelectChapter(1)
[void](InvokeRuinFixture $expandedGame 'EnterRuinRoom' @(15, $false))
$expandedGame.SetHand([Echoes.Hand]::Open)
$expandedGame.RuinRightReach = [Echoes.Journey]::RuinMaximumReach
$topBitPhone = $expandedGame.RuinObjects | Where-Object Id -eq 127
[void][Echoes.Journey].GetProperty('Distance').GetSetMethod($true).Invoke($expandedGame, @([float]$topBitPhone.Depth))
[void][Echoes.Journey].GetProperty('RuinLane').GetSetMethod($true).Invoke($expandedGame, @([float]$topBitPhone.Lane))
$expandedGame.Flight.Reset($topBitPhone.Altitude, [Echoes.Journey]::RuinHeightLimit)
for ($i = 0; $i -lt 8; $i++) { $expandedGame.Step(.02, 0) }
AssertRuinLoot ($expandedGame.RuinCanTake -and $expandedGame.RuinSearchTarget -eq $topBitPhone) 'The last new stash is reachable as an ordinary interaction.'
[void]$expandedGame.Touch(1)
$highCheckpoint = $expandedGame.Checkpoint
AssertRuinLoot ($topBitPhone.Collected -and $expandedGame.RuinPhones -eq 1 -and $highCheckpoint.version -eq 10 -and
    $highCheckpoint.ruinFinds -eq 0 -and $highCheckpoint.ruinFindsExtra -eq [long]::MinValue) 'Taking ID 127 must set only the top bit of the expanded mask.'

$expandedFinds = @(RuinFixtureObjects $expandedGame)
$expandedHeart = $expandedFinds | Where-Object Id -eq 126
$expandedTrade = $expandedFinds | Where-Object Id -eq 123
InvokeRuinFixture $expandedGame 'RecordRuinPickup' @($expandedHeart)
$fillDevices = $expandedFinds | Where-Object { $_.Kind -in 'Phone','Computer' -and $_ -ne $expandedTrade -and $_ -ne $topBitPhone } |
    Select-Object -First 13
foreach ($device in $fillDevices) { InvokeRuinFixture $expandedGame 'RecordRuinPickup' @($device) }
AssertRuinLoot ($expandedGame.RuinInventory -eq 15 -and (InvokeRuinFixture $expandedGame 'OfferRuinLoot' @($expandedTrade))) 'A high-ID cache uses the existing full-inventory swap menu.'
[void]$expandedGame.Touch(1)
AssertRuinLoot ($expandedGame.RuinLootSwap -and $expandedGame.RuinLootChoices[0].Slot -eq 126) 'The menu selects the high-ID heart explicitly.'
[void]$expandedGame.Touch(1)
$expandedSave = $expandedGame.Checkpoint
AssertRuinLoot ($expandedTrade.Collected -and $expandedGame.RuinHearts -eq 0 -and $expandedSave.ruinReleasedMemories -eq 0 -and
    $expandedSave.ruinReleasedMemoriesExtra -eq (1L -shl 62)) 'A high-ID trade releases one memory in the expanded mask.'

$expandedReload = [Echoes.Journey]::new(13)
AssertRuinLoot ($expandedReload.Restore($expandedSave)) 'An expanded checkpoint should load with another process seed.'
$expandedRestored = @(RuinFixtureObjects $expandedReload)
$expandedRestoredTaken = @($expandedRestored | Where-Object { $_.Id -in 123,126,127 -and $_.Collected })
AssertRuinLoot ($expandedRestoredTaken.Count -eq 3 -and $expandedReload.RuinInventory -eq 15 -and $expandedReload.RuinHearts -eq 0 -and
    $expandedReload.Checkpoint.ruinFindsExtra -eq $expandedSave.ruinFindsExtra -and
    $expandedReload.Checkpoint.ruinReleasedMemoriesExtra -eq $expandedSave.ruinReleasedMemoriesExtra) 'Restoring preserves both expanded device locks and the consumed heart without returning it to inventory.'

$legacyGame = [Echoes.Journey]::new(22371)
$legacyGame.SelectChapter(1)
$legacyFind = $legacyGame.RuinObjects | Where-Object Id -eq 0
InvokeRuinFixture $legacyGame 'RecordRuinPickup' @($legacyFind)
$legacySave = $legacyGame.Checkpoint
$legacySave.version = 7
$legacyReload = [Echoes.Journey]::new(98)
AssertRuinLoot ($legacyReload.Restore($legacySave)) 'A version 7 collectibles checkpoint should load.'
$legacyRestored = $legacyReload.RuinObjects | Where-Object Id -eq 0
AssertRuinLoot ($legacyRestored.Collected -and $legacyRestored.Kind -eq $legacyFind.Kind -and $legacyReload.RuinInventory -eq 1 -and
    $legacyReload.Checkpoint.ruinFindsExtra -eq 0 -and @($legacyReload.RuinObjects | Where-Object { $_.Id -ge 64 -and $_.Collected }).Count -eq 0) 'Old collection bits keep their original item and leave all expansion slots available.'

$maxReachGame = [Echoes.Journey]::new(52)
$maxReachGame.SelectChapter(1)
$conflictedSave = $maxReachGame.Checkpoint
$conflictedSave.ruinSeed = -148491
$conflictedSave.ruinRoom = 13
AssertRuinLoot ($maxReachGame.Restore($conflictedSave)) 'Load the layout whose chest originally fell entirely inside a right capture area.'
$maxReachGame.RuinRightReach = [Echoes.Journey]::RuinMaximumReach
$maxReachGame.SetHand([Echoes.Hand]::Open)
$maxReachChest = $maxReachGame.RuinObjects | Where-Object Kind -eq 'Chest'
[void][Echoes.Journey].GetProperty('Distance').GetSetMethod($true).Invoke($maxReachGame, @([float]$maxReachChest.Depth))
[void][Echoes.Journey].GetProperty('RuinLane').GetSetMethod($true).Invoke($maxReachGame, @([float]$maxReachChest.Lane))
$maxReachGame.Flight.Reset($maxReachChest.Altitude, [Echoes.Journey]::RuinHeightLimit)
$maxReachGame.Step(.02, 0)
AssertRuinLoot ($maxReachGame.RuinCanTake -and $maxReachGame.RuinSearchTarget -eq $maxReachChest -and
    $maxReachChest.Id -eq 55 -and $maxReachChest.ChestStyle -eq 7) 'Even at maximum right capture, a previously inaccessible chest retains its identity and offers pickup.'
[void]$maxReachGame.Touch(1)
AssertRuinLoot ($maxReachGame.RuinLootSource -eq $maxReachChest) 'B opens the reachable chest with the right capture slider at maximum.'

$entryGame = [Echoes.Journey]::new(414)
$entryGame.SelectChapter(1)
$entryGame.SetHand([Echoes.Hand]::Open)
$entryChest = $entryGame.RuinObjects | Where-Object Kind -eq 'Chest'
$allChests = @(RuinFixtureObjects $entryGame | Where-Object Kind -eq 'Chest')
AssertRuinLoot ($allChests.Count -eq [Echoes.Journey]::RuinChestCount -and
    @($allChests | Select-Object -ExpandProperty ChestStyle -Unique).Count -eq 16 -and
    @($allChests | Where-Object { $_.Altitude -gt .14 }).Count -eq 0) 'Every room has an independently saved chest reachable from floor height.'
AssertRuinLoot ($entryChest.Id -eq 3 -and $entryChest.Lane -eq 0 -and $entryChest.Depth -lt 5.1) 'The first chest is an early centered landmark.'
for ($i=0; $i -lt 160 -and $entryGame.Distance -lt ($entryChest.Depth - .18); $i++) { $entryGame.Step(.02, .68) }
for ($i=0; $i -lt 22; $i++) { $entryGame.Step(.02, 0) }
AssertRuinLoot ($entryGame.Flight.Height -lt .12 -and $entryGame.RuinCanTake -and $entryGame.RuinSearchTarget -eq $entryChest) 'Ordinary forward motion and a stop reach the first chest without a flight adjustment.'
[void]$entryGame.Touch(1)
AssertRuinLoot ($entryGame.RuinLootSource -eq $entryChest -and
    $entryGame.RuinLootDevices[0].Kind -eq 'Phone' -and $entryGame.RuinLootDevices[0].Label -ceq 'ipone18p' -and
    @($entryGame.RuinLootDevices | Where-Object { $_.Kind -eq 'Phone' -and $_.Label -cne 'ipone18p' }).Count -eq 0) 'The entry chest opens normally and each displayed phone uses the exact requested name.'
[void]$entryGame.Touch(1)
AssertRuinLoot ($entryGame.RuinPhones -eq 1 -and $entryGame.Message -cmatch 'ipone18p') 'A phone pickup reports the same name.'

$lastChest = $null
foreach ($seed in 140..180) {
    $lastGame = [Echoes.Journey]::new([int]$seed)
    $lastGame.SelectChapter(1)
    [void](InvokeRuinFixture $lastGame 'EnterRuinRoom' @(10, $false))
    $candidate = $lastGame.RuinObjects | Where-Object Kind -eq 'Chest'
    if ($lastGame.RuinChestItemCount($candidate) -eq 4) { $lastChest = $candidate; break }
}
AssertRuinLoot ($null -ne $lastChest -and $lastChest.ChestStyle -eq 15) 'An expanded stock fixture exercises bit 63.'
$lastGame.SetHand([Echoes.Hand]::Open)
AssertRuinLoot (InvokeRuinFixture $lastGame 'OfferRuinLoot' @($lastChest)) 'The sixteenth chest opens.'
1..3 | ForEach-Object { [void]$lastGame.Touch(0) }
[void]$lastGame.Touch(1)
$lastSave = $lastGame.Checkpoint
AssertRuinLoot ($lastSave.ruinChestLootTaken -eq [long]::MinValue -and $lastGame.RuinInventory -eq 1) 'The last chest slot uses the top bit without aliasing earlier stock.'
$lastReload = [Echoes.Journey]::new(999)
AssertRuinLoot ($lastReload.Restore($lastSave)) 'A checkpoint with the top stock bit restores.'
$lastRestored = $lastReload.RuinObjects | Where-Object Kind -eq 'Chest'
AssertRuinLoot ($lastReload.RuinChestRemaining($lastRestored) -eq 3 -and $lastReload.RuinInventory -eq 1 -and
    $lastReload.RuinChests -eq 1) 'Expanded chest stock and discovery survive reload.'

Write-Output 'Passed sixteen chests, the entry path and ipone18p labels, top-bit stock and reload, explicit trades, locked capacity, and older saves.'
