# Bloodroot infection zones

This prototype uses three small components and does not change the shared player,
enemy, wave, or scene code.

## Setup

1. Add `BloodrootInfectionController` to the player root.
2. Add `BloodrootInfectionFeedback` to the same object if camera, overlay, or
   heartbeat effects are wanted.
3. Add `BloodrootInfectionZone` to any object with a trigger collider.

The default zone fills the meter in five seconds. Infection slowly recovers after
the player leaves. While the player remains inside at 80% infection or higher, the
controller sends five damage through `IDamage` once per second.

The feedback script scales one FOV pulse, one optional red `CanvasGroup`, and one
optional heartbeat loop from the normalized infection amount. All presentation
references are optional.

## Player integration

The controller automatically finds an `IDamage` component on the player object. A
specific component can also be assigned to **Damage Receiver** in the Inspector.

UI can subscribe to `InfectionChanged`, which reports a value from 0 to 1, or read
`NormalizedInfection` directly.

Overlapping zones use the strongest infection rate so touching two trigger volumes
does not accidentally double the effect.
