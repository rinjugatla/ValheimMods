# Add All Fuel And Ore For Smelter Charcoal kiln etc

## Features

* You no longer need to press the button repeatedly to get to the furnace.
* When using the facility normally (default: E), fuel is fed one by one.
* When using the facility, pressing the modifier key (default: Left Shift) will load the fuel, etc. in batches.

### Conflict

* I have tried to avoid conflicts with the Craft Build Smelt Cook Fuel From Containers mod as much as possible, but there may be more conflicts due to feature changes.
* The ability to refill items from containers in the "Craft Build Smelt Cook Fuel From Containers" mod has been disabled in versions 1.3.0 and later, except for the input of charcoal.

## Setting

* The configuration file is "BepInEx/config/rin_jugatla.AddAllFuel.cfg".It will be created automatically when you start the game after installing the mod.

1. Enabled
   * true: MOD enabled
   * false: Mod disabled
2. NexusID
   * ID for automatic update notification, no need to change.
3. ModifierKey
   * Modifier key used to control batch injection.
4. IsReverseModifierMode
   * true: use key (E) for batch input
   * false: use modifier key (left shift) + use key (E)
5. ExcludeNames
   * Item name to exclude from submission.
   * Example: $item_finewood,$item_roundlog
   * Refer to this document for item names.
6. AllowAddOneExcludeItem
   * true: Exclude items from the list if they are submitted individually.
   * false: Do not allow items to be submitted regardless of whether they are submitted individually or in batches

## Supported Facilities

1. Smelter
2. Charcoal kiln
3. Blast furnace
4. Windmill
5. Spinning wheel
6. Campfire
7. Various torches

## Changelog

### 1.4.0

* Batch feeding to campfire and torch is now supported.

### 1.3.0

* Added an option to exclude items used to input fuel, etc.
* Added option to throw in even excluded items when throwing in fuel, etc. one at a time.
* The ability to refill items from containers in the "Craft Build Smelt Cook Fuel From Containers" mod has been disabled, except for the input of charcoal.

### 1.2.1

* Fixed a bug that caused items to be duplicated when used in conjunction with the "Craft Build Smelt Cook Fuel From Containers" mod.

### 1.2.0

* Added an option to change the switching between feeding items one at a time or in batches.
* Change "IsReverseModifierMode" in the configuration file (rin_jugatla.AddAllFuel.cfg).
* false: Batch submit with Modifier key (left shift) + Use key (E)
* true: Use key (E) to submit all

### 1.1.0

* Changed to use the mod when using the facility while holding down the modifier key (default: left shift).
* Added support for NexusUpdateMOD.