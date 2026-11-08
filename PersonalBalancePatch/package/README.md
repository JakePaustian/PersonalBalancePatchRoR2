# PersonalBalancePatch

A personal balance patch for Risk of Rain 2 by SSF.

**Everyone in a lobby needs the same version of this mod.** It registers itself with R2API's network compatibility check, so players without it (or with a different version) are rejected from the lobby.

## Changes

### Lunar coins
- Your lunar coin balance is now **per run**. Every run starts at 0, and the balance is discarded afterwards. Your save file's coin count is never read or changed.
- Every kill has a flat **2% chance** to drop a lunar coin. The chance no longer halves after each drop.

### Kill bounty
- Gold from kills starts slightly above vanilla (**112.5%**) and falls toward **37.5% of vanilla** as the run's difficulty rises, so long runs and slow, full-map looting pay less.
- Gold from chests, barrels, and items is unchanged, so money items matter more later in a run.

### Singularity Band
- The black hole deals **150%** damage (+150% per stack), up from 100%.
- Recharge time is **13 seconds**, down from 20. The cooldown stack counter now counts real seconds.

### Defiant Gouge
- Now only triggers when you **fail a Shrine of Chance**, not on every shrine.
- Holders fail Shrines of Chance much less often: **25%** failure chance with one stack, multiplied by **0.8** for each additional stack (never zero).
- A failure summons enemies at the shrine. The encounter budget is **80 x difficulty x stacks** (vanilla: 40).
- You get a chat message when it happens.

### Transcendence
- Shields now take longer to start recharging and longer to refill:
  - Wait after taking damage: **7 + stacks** seconds (vanilla: 7)
  - Time to refill from empty: **2 + stacks** seconds (vanilla: 2)
- This applies to your whole shield bar, including shield from other sources while you hold Transcendence.
- The shield-recharge sound now plays when the recharge actually starts.

### Fall damage (everyone)
- Fall damage is now measured against **health plus shield** instead of health alone. Shields still absorb it first, but a large shield no longer makes falls nearly harmless.

### Commando
- **Tactical Dive** now also refreshes the cooldowns of your **secondary and special**, in addition to the primary reload it already did.

## Compatibility
- Requires **R2API Core** and **R2API Language** (installed automatically).
- Optional: **LookingGlass**. If installed, its item stat panels show the new Singularity Band, Defiant Gouge, and Transcendence numbers.
- Item and skill text is updated for English only.

## Notes
- This changes core balance. It is meant for groups who all want it.
- Report problems by describing your other installed mods and what you were doing.
