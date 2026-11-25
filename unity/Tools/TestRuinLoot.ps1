$ErrorActionPreference = 'Stop'
if (-not ('Echoes.Journey' -as [type])) {
    $lootSources = 'Journey.cs', 'JourneyChapters.cs', 'JourneyForest.cs', 'ForestMaze.cs', 'JourneyMaze.cs',
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

Write-Output 'Passed deterministic ruins chest stock, confirmation, cancelled and confirmed organ trades, locked device capacity, revisit and save restore.'
