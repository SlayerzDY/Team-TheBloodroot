# Bloodroot Infection Zones

This folder is self-contained. It does not modify the player controller, enemy AI,
wave logic, or any scene.

## Prototype setup

1. Add `BloodrootInfectionController` to the player root.
2. Optionally add `BloodrootInfectionFeedback` to the same object and assign its
   camera, overlay, weapon, and audio references. Use a dedicated child object for
   the hallucination AudioSource so fake calls can be positioned around the player.
3. Create a GameObject with a 3D Collider, enable **Is Trigger**, and add
   `BloodrootInfectionZone`.
4. Ensure normal Unity trigger requirements are met: at least one participant
   needs a Rigidbody or CharacterController.

The default zone fills the meter in five seconds. At 35% the player enters the
Distorted stage, at 70% the Critical stage begins, and at 85% the zone emits a
five-damage tick each second. Infection recovers after leaving the zone.

## Integration hooks

- `InfectionChanged` reports a normalized value from 0 to 1.
- `StageChanged` reports Clear, Distorted, or Critical.
- `DamageTicked` fires whenever critical infection deals a tick.
- If the player has a component implementing the existing `IDamage` interface,
  assign it as the damage receiver. Otherwise the events still work for later
  integration.

Overlapping zones use the strongest exposure rate instead of stacking. This keeps
zone seams from punishing the player with accidental double exposure.
