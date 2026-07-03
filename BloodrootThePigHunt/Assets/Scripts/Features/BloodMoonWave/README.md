# Blood Moon wave modifier

This prototype adds one special modifier every three waves. The modifiers cycle in
order so testing is predictable:

1. Stampede
2. Thick Hide
3. Blood Frenzy

## Wave-manager hookup

Call `BeginWave` before calculating spawn count or enemy stats:

```csharp
BloodMoonModifier modifier = bloodMoonDirector.BeginWave(waveNumber);
int count = modifier == null
    ? baseEnemyCount
    : modifier.ModifyEnemyCount(baseEnemyCount);
```

Use `ModifyHealth`, `ModifyDamage`, and `ModifySpeed` when preparing each spawned
enemy. Always start from the enemy's base values so pooled enemies do not multiply
their stats more than once.

Call `EndWave(waveNumber)` after the last enemy is defeated.

`BloodMoonPresentation` is optional. It changes one light to red, briefly shows a
CanvasGroup, and plays one sound. All three references can be left empty while the
gameplay is being tested.
