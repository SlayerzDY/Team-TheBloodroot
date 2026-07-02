# Blood Moon Wave Modifier

This feature decides when a Blood Moon happens and which rule changes for that
wave. It deliberately does not start waves, spawn enemies, keep score, or decide
when a wave is over. Those jobs should stay in the team's wave manager.

## What is included

- `BloodMoonWaveDirector` schedules special waves and selects a modifier.
- `BloodMoonModifier` stores the enemy-count, health, damage, speed, and pig-part
  reward multipliers for one rule set.
- `BloodMoonPresentation` is optional. It handles a red lighting transition, a
  short UI banner, and a start sound without owning any gameplay state.
- `IBloodMoonModifierTarget` is the handoff point for systems that prefer to be
  notified instead of reading the director.

The default schedule starts on wave 3 and repeats every three waves. Three sample
modifiers are created when the director is first added: Stampede, Thick Hide, and
Blood Frenzy. All values can be changed in the Inspector.

## Recommended scene setup

1. Create one `Blood Moon Wave Director` object in the gameplay scene.
2. Add `BloodMoonWaveDirector` to it. The director should exist for the full run,
   not be recreated for every wave.
3. Add `BloodMoonPresentation` to the same object if the scene has final lighting,
   UI, and audio references. It is safe to leave any of those references empty.
4. Do not put the director on an enemy prefab. There should be exactly one active
   director for a run.

## Best place to connect the wave manager

The wave manager should call `BeginWave` before it calculates spawn counts. It
should call `EndWave` after the final enemy has been removed and before the build
phase begins.

```csharp
private void StartWave(int waveNumber)
{
    BloodMoonModifier modifier = bloodMoonDirector.BeginWave(waveNumber);

    int spawnCount = GetBaseSpawnCount(waveNumber);
    if (modifier != null)
    {
        spawnCount = modifier.ModifyEnemyCount(spawnCount);
    }

    SpawnWave(spawnCount, modifier);
}

private void FinishWave(int waveNumber)
{
    bloodMoonDirector.EndWave(waveNumber);
    StartBuildPhase();
}
```

Pass the selected modifier into the enemy setup code when an enemy is spawned.
Always calculate from the prefab's original stats. Do not multiply a value that
was already modified, or pooled enemies will become stronger every time they are
reused.

```csharp
private void PrepareEnemy(EnemyStats enemy, BloodMoonModifier modifier)
{
    enemy.ResetToBaseStats();
    if (modifier == null)
    {
        return;
    }

    enemy.SetMaxHealth(modifier.ModifyHealth(enemy.BaseHealth));
    enemy.SetDamage(modifier.ModifyDamage(enemy.BaseDamage));
    enemy.SetMoveSpeed(modifier.ModifySpeed(enemy.BaseMoveSpeed));
}
```

The class names in that example are placeholders for the team's final enemy API.
The important part is the order: reset base values, apply the current modifier,
then activate the enemy.

## Alternative: register a target

If the wave spawner or stat manager already has a central place for temporary
rules, it can implement `IBloodMoonModifierTarget` and register itself once.

```csharp
public sealed class WaveRuleAdapter : MonoBehaviour, IBloodMoonModifierTarget
{
    private BloodMoonModifier currentModifier;

    private void OnEnable()
    {
        bloodMoonDirector.RegisterTarget(this);
    }

    private void OnDisable()
    {
        bloodMoonDirector.UnregisterTarget(this);
    }

    public void ApplyBloodMoonModifier(BloodMoonModifier modifier)
    {
        currentModifier = modifier;
    }

    public void ClearBloodMoonModifier()
    {
        currentModifier = null;
    }
}
```

Use either direct queries or registered targets for gameplay state. Mixing both on
the same stat can apply a multiplier twice.

## UI, audio, and rewards

- `BloodMoonStarted` supplies the wave number and full modifier. A HUD can use
  `DisplayName` and `Description` for its announcement.
- `NormalWaveStarted` is useful for clearing a previous HUD label.
- `ModifierCleared` fires when `EndWave` removes the special rule.
- Pig-part rewards should call `ModifyPartReward` once, at the point where the
  reward is awarded. Keep the base drop amount on the pig or loot table.

Inspector events are also available for simple effects that do not need event
data. C# events are the better choice for gameplay and UI text.

## Selection and networking notes

Random selection is deterministic for a given wave number and selection seed. A
restart using the same seed will choose the same modifier. If multiplayer is added,
the host should still be the only machine that calls `BeginWave`; replicate the
chosen modifier ID to clients rather than letting every client decide separately.

When this branch is merged, the only required code change outside this folder is
the two wave-manager calls shown above. Enemy stat changes and reward changes can
be connected when those systems settle, without changing the director.
