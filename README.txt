# Talentless Hero

A 2D tile-based RPG built in Unity (C#) where a failed hero can increase their power indefinietly.

## About the Game

Talentless Hero is a turn-based RPG with a combat system inspired by games like Epic Seven. Instead of simple back-and-forth turns, combat uses a **Combat Readiness gauge** — faster characters can take multiple turns in a row, and slower ones might not get to act at all. Stat investment, buffs, and debuffs all feed into this system, making every build decision meaningful.

The demo *will* feature exploration across multiple tile maps, NPC interactions, and a boss encounter against the **Cave Wraith** — a magic-focused enemy that opens fights by locking down the player before draining their life.

## Core Systems

### Stats & Leveling
Five core stats drive everything in the game:

| Stat | What It Does |
|------|-------------|
| **Strength** | Physical damage, physical defense, bonus HP |
| **Agility** | Turn speed — how fast your CR gauge fills |
| **Intelligence** | Magic damage, magic defense, bonus MP |
| **Luck** | Dodge chance, loot drop multiplier |
| **Hit** | Accuracy (counters dodge), crit chance |

The player earns 5 stat points per level and can distribute them freely. EXP requirements scale by 10% per level, so early levels come quickly while later ones require more effort. HP and MP scale through both leveling and stat investment, reaching roughly 10,000 HP and 1,000 MP by level 100 with a balanced build.

Crit damage can only be increased through quest rewards and items — not through leveling — to prevent crit builds from becoming overpowered.

### Combat Readiness Turn System
Every combatant has a CR gauge (0–100%) that fills each tick based on their Agility. When it hits 100%, that combatant takes a turn and the gauge resets. A character with twice the Agility of another will get roughly twice as many turns. Skills can also push or pull CR directly, adding another layer of speed control.

### Status Effects
Buffs, debuffs, silence, guaranteed hit, and damage/heal over time. Effect durations tick on the **affected unit's** turns, so slowing someone down also makes their debuffs last longer in practice. Stat debuffs stack multiplicatively.

### Enemy AI
Enemies follow a priority-based decision system: they attempt setup moves first (self-buffs, player debuffs), then offensive skills, then utility, and fall back to a basic attack if everything is on cooldown. Enemies do not use MP — their skills are gated by cooldowns only.

### Demo Boss: Cave Wraith (Level 5)
A magic-focused boss with four skills:
- **Wraith's Third Eye** — Guarantees all attacks hit for 2 turns
- **Anchor** — Reduces the player's Agility by 25% per use (stacks)
- **Silence** — Blocks player skill usage for 1 turn
- **Soul Absorb** — Deals magic damage and heals for 20% of damage dealt

The Wraith always sets up before attacking, creating a learnable pattern the player can strategize around.

### Resting
The Innkeeper NPC fully restores HP, MP, and clears all status effects. Resting also respawns all enemies, creating a natural explore → fight → rest loop.

## Tech Stack

- **Engine:** Unity
- **Language:** C#
- **Input:** Unity Input System (keyboard + controller)
- **Camera:** Cinemachine with 2D confiner boundaries
- **Architecture:** Event-driven (C# events for loose coupling between systems)

## Project Structure

```
Scripts/
├── StatType.cs                 # Enum: STR, AGI, INT, LCK, HIT
├── PlayerStats.cs              # Stats, HP/MP, leveling, EXP, allocation, save/load
├── PlayerMovement.cs           # Input handling and walk animations
├── MapTransitions.cs           # Tile map transitions with camera updates
├── EnemyStats.cs               # Enemy stat block, damage, healing, combat rolls
├── EnemyAI.cs                  # Priority-based skill selection
├── EnemySkill.cs               # Abstract base for enemy skills (ScriptableObject)
├── EnemyManager.cs             # Respawns all enemies on rest
├── StatusEffect.cs             # Buff/debuff data class
├── StatusEffectHandler.cs      # Manages active effects per combatant
├── CombatTurnManager.cs        # CR gauge turn system
├── CombatExample.cs            # Reference wiring for combat encounters
├── InnkeeperNPC.cs             # Rest mechanic trigger
├── WraithBossSetup.cs          # Cave Wraith stat reference and config
└── Skills/
    ├── SkillSoulAbsorb.cs      # Damage + 20% lifesteal
    ├── SkillAnchor.cs          # AGI debuff (25%, 2 turns, stacks)
    ├── SkillSilence.cs         # Blocks skills for 1 turn
    └── SkillWraithsThirdEye.cs # Guaranteed hit self-buff (2 turns)
```

## Status

This project is in active development toward a playable demo. See [LICENSE](LICENSE) for usage terms.

## Author

Estarlin Ramos (Gabriel)