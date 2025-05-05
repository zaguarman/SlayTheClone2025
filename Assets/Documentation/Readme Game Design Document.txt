Game Design Document

I. Core Concept, Vision & High-Level Interactions

Game Premise: A card game focused on outsmarting the opponent through strategic planning, information control, and tactical positioning.

Genre: Roguelite Deckbuilder – progressively build and refine your deck during a run.

Central Conflict: Intelligence versus Counter-Intelligence, thematically mirrored by Control (Myth, State factions) versus Chaos (Shadow, Zeitgeist factions).

Primary Goal: Outsmart opponents by gathering/denying information, controlling the location, manipulating status effects, and executing strategic creature movement.

Key Interaction Space - Tactical Positioning:

Battlefield Layout: Each player controls one side of the battlefield. Each side consists of a single row containing 5 creature slots. The two rows are positioned opposite each other symmetrically.

Slot Occupation: Each slot can hold a maximum of one creature.

Adjacency: For a non-edge slot, there are typically two adjacent friendly slots and one opposing enemy slot directly in front. Edge slots have fewer adjacent slots.

Movement & Blocking: Positioning influences movement, targeting, adjacency effects, and board control. Creatures occupy slots, blocking movement unless an ability allows crossing, swapping, or pushing. Movement must be initiated via Spells or abilities.

Key Mechanic - Fog of War: Opponent pending actions are hidden in the queue. Intelligence reveals pending actions. Resolved actions are immediately visible. Revealed info typically lasts until end-of-turn.

II. Foundational Game Systems & Rules

Faction System:

Cards belong to factions (e.g., Statecraft, Shadow Factions, Myth, Zeitgeist).

Deckbuilding allows faction combinations.

Thematic Associations: Control (Myth, State), Chaos (Shadow, Zeitgeist).

Faction-based interactions: Soft weaknesses/resistances.

Action Economy & Tempo:

No resource cost (mana/energy) to play cards.

Tempo managed by Deployment Time (X): Prevents actions/passives for X turns. Applies on entry from hand or revival.

Action Types & Triggering:

Creatures have core stats: Attack, Health, Speed. Reset to base when reshuffled into deck.

Creature Abilities: Passive, automatic triggers (OnDamage, OnAttack, etc.). Movement via abilities/Spells.

Special Creature Archetypes: Support Units (Active non-Attack abilities), Automated Units (Automatic passive triggers).

Creature Attacks: Actively queued by the player.

Spells: Active actions played directly by the player.

Determinism: Game effects are deterministic.

Card Management:

Drawing Cards: Standard mechanic.

Discard Pile: Zone for used Spells and destroyed Creatures.

Discarding: From own hand. Can be an additional cost/resource. Goes to Discard Pile.

Banish: Removes card from the game permanently. Cannot be recovered.

Card Lifecycle & Reshuffling: Played Spells & destroyed Creatures go to Discard Pile. Empty Draw Deck triggers reshuffle. Creature stats reset on reshuffle.

Requisites: Conditions for play/activation (sacrifice, discard, board state).

III. Core Gameplay Loops & Mechanics

Information Warfare (Intelligence vs. Counter-Intelligence): Reveal vs. conceal pending actions/info.

Board Manipulation & Control:

Logistics (Movement): Abilities/Spells for moving/swapping. Includes Post-Action Repositioning, Positional Relay.

Movement Restriction: Heavy status, abilities, Slot Effects.

Slot Effects / Environmental Hazards: Single effect per slot (new replaces old). Visible or hidden. Triggers: On Move In/Out, Continuous, Reactive, One-Time. Can block actions.

Hazard Removal / Decontamination: Cleansing slot effects.

Location (Weather): Global modifiers.

Flooding: Using numerous weak creatures.

Creature Enhancement & Protection:

Setup: Increasing stats.

Survivability: Heal, Armor, redirection.

Immunity/Protection: Granular immunity.

Cleansing: Removing negative status.

Disruption & Control:

Status Effects Application: See Section IV.

Red Tape: Delaying actions, lowering priority.

Disable Archetype: Suppressed.

Counter / Interference: Stopping/altering queued actions.

Attack/Effect Redirection: Changing targets.

Recursion & Recovery:

Revive: Return creature from Discard Pile to battlefield (set HP, Deployment Time applies).

Return to Hand (from Battlefield): Move creature from battlefield to hand.

Return to Hand (from Discard): Move card from Discard Pile to hand.

Combat, Damage & Targeting:

Direct Damage: Reducing HP.

Chain/Spread Damage: Multi-target.

Armor: Temporary HP. Recoil hits Armor first.

Life Drain / Siphoning: Dealing damage heals the source (e.g., "Vampiric Strike"). Calculates based on damage dealt to HP or Armor.

Targeting Nuances: Patterns/restrictions.

Action Queue & Turn Flow Mechanics:

Queue Manipulation: Interacting with hidden queue.

Priority Enhancement: Higher priority.

Global Priority Modifiers: E.g., Coordinated Advance, System Scramble.

Normal Tie-Breaking: (Equal Prio/Speed) 1. Lowest current HP first. 2. Lowest Attack first. 3. Random.

Reversed Tie-Breaking: (System Scramble) 1. Highest current HP first. 2. Highest Attack first. 3. Random.

Priority Reduction: Lowering priority.

IV. Detailed Mechanics & Specific Systems

Traps:

Hidden effects triggered by opponent interaction.

Type 1: Trigger on successful reveal by enemy Intelligence.

Type 2: Queued action triggers drawback on opponent if manipulated.

Slot-based traps via hidden slot effects.

Status Effects (Detailed Definitions):

Addicted (X turns | Requires: Stat Alteration): At the beginning of its controller's turn, if this creature did not have its Attack, Health, or Speed stats directly altered (increase or decrease) by any effect during the previous turn, it gains Suppressed 1. Check occurs each turn for the duration X. Theme: Dependency, withdrawal.

Blessed: Persists until consumed. Negates the next negative status effect that would be applied, then Blessed is removed. Does not stack. Theme: Single-use ward, divine protection.

Bleeding / Corroding (X turns): At the End of its controller's Turn, this creature takes 1 damage. Lasts X turns. Stacks duration. Damage does trigger "OnDamage" passive abilities. Theme: Damage over time, wounds, decay.

Bored (X turns): At the beginning of its controller's turn, if not targeted last turn, skips action/passives this turn. Heals 1 HP End of Turn. Lasts X turns. Theme: Apathy, complacency.

Caffeinated (X turns): Actions +1 Priority. +1 Attack. Takes 1 damage End of Turn. Lasts X turns. Cannot be cleansed. Stacks duration. Cleanses Tired. Theme: Stimulant, hyperactive, burn out.

Compromised: Persists until triggered once. When targeted by enemy "Intelligence", triggers a negative effect for its controller (defined by source). Removed after triggering. Can be applied secretly. Theme: Leaky information, double agent, honeypot.

Concealed: Persists until next action resolves or removed/overridden. Planned action hidden/obscured in opponent's queue preview. Overrides Revealed. Theme: Secrets, hidden intent, misdirection.

Cursed (X turns): End of controller's turn, apply a random negative status from a predefined pool. Lasts X turns. Stacks duration. Theme: Bad luck, persistent misfortune.

Delayed (X turns): Next X turns, actions have base Priority -1. Stacks additively. Duration resets/extends. Theme: Sluggishness, lag.

Deployment Time (X turns): Applied on entry. For X turns, cannot perform actions, passives don't trigger (exceptions possible). Decreases by 1/turn. Theme: Initialization, mobilization.

Depressed (X turns): Attack stat halved (rounded). Lasts X turns. Stacks duration. 3+ total turns -> remove all Depressed, apply Doomed. Theme: Morale loss, reduced effectiveness.

Doomed (X turns): Destroyed after X turns. Gains +1 Attack start of each turn. Cannot gain Depressed. Cannot be cleansed. Cannot stack duration. Theme: Marked for death, final surge.

Heavy (X turns): Cannot perform Move actions, cannot be moved. Gains 1 Armor End of Turn. Lasts X turns. Stacks duration. Theme: Anchored, immovable.

Ostracized (X turns): Cannot be targeted by actions/abilities. Cannot gain new status effects. Existing effects remain. Can still act. Lasts X turns. Stacks duration. Theme: Isolation, untouchable, phased out.

Revealed (X turns): Planned action visible to opponent in queue preview. Decreases by 1/turn. Stacks duration. Overridden by Concealed. Theme: Information leak, surveillance.

Suppressed (X turns): Cannot perform actions (except Move). Passives don't trigger. Lasts X turns. Stacks duration. Theme: Major disablement, suppression.

Targeted (X intensity): Takes X additional damage from all sources. Stacks intensity. Duration refreshes/extends. Theme: Defenses breached, weak point.

Tired (X stacks): Adds stacks. 1 stack -> Suppressed 1 in 2 turns. 2 stacks -> Suppressed 1 next turn. 3+ stacks -> Suppressed 1 immediately this turn (removes queued actions except Move). Cleansed by Caffeinated. Theme: Gradual exhaustion to shutdown.

V. Game Design Philosophy & Balancing

Balancing Focus: Key priority. Rigorous playtesting and iteration needed.

Numerical Balance: Effects modifying stats or numerical values should use conservative numbers and stack carefully (additively/multiplicatively) to prevent exponential scaling and easily broken interactions.

Status Effect Design Principle: When designing new cards or effects, prioritize utilizing the existing pool of status effects (defined in Section IV). Avoid creating new, unique status effects unless the desired mechanic cannot be reasonably achieved by combining or slightly modifying existing ones. This promotes system coherence, reduces rule complexity, and encourages synergistic interactions.

Creature Ability Design Principle: Similarly, when designing new creature abilities (passive or active), prioritize reusing existing core ability mechanics (e.g., dealing damage, applying status, moving, healing, modifying stats) and established trigger types (e.g., OnAttack, OnDamage, OnTurnEnd). Avoid creating entirely novel ability mechanics if the desired gameplay effect can be achieved by applying existing mechanics in new ways, combining them, leveraging status effects, or using different trigger conditions. However, the specific trigger conditions themselves (e.g., 'OnFriendlyCreatureOfTypeX moves', 'OnTakingSpellDamage', 'WhileAdjacentToY') can be freely modified and combined to fit the unique theme and function of a card.

Balancing Levers:

Opportunity Cost: Card choice (deck slot), Slot Occupation (board space), Sacrifice mechanics (unit loss for gain), Discarding cards (hand resource cost).

Space Limitations: Finite creature slots (5 per side), hand size limits (implied).

Domino Effects: Intentionally designed chain reactions (abilities, statuses, positioning). Requires risk/reward assessment.

Development Goals & Player Experience:

Reward: Strategic Planning (long-term goals), Creative Deckbuilding & Synergies (finding combos), Adaptation (reacting to opponent and board state), opportunities for Comebacks (avoiding deterministic losses).

Avoid: Overly complex calculations required mid-turn (Complicated Math), situations that feel inherently unwinnable due to luck rather than strategy (Unfair Situations). Focus on clear cause-and-effect for actions and consequences.

Core Design Tenet - Double-Edged Sword:

Major positive effects should ideally have a drawback or risk.

Major negative effects or costs should ideally have a potential positive side-effect or compensation.

All cards should have at least one effect; ideally, always a drawback (even minor).

Examples: "OnMove: +1 Attack" + "OnAttacked: Controller takes 1 damage"; "Doomed" + Attack bonus; "Reckless Assault" (Attack bonus + recoil damage).

VI. Illustrative Content Examples

Example Status Groupings: Information, Debuffs/Control, Buffs/Utility, Initial State.

Example Card Concept 1: AI Psychologist (Cleansing + Reveal drawback)

Example Card Concept 2: Cult Ritualist (Status Transfer + Self-Curse drawback)

Example Passive Ability Concepts:

"All-Terrain Protocol" / "Ninja Step": Ignores negative slot effects on movement.

"Defiance Protocol" / "Resolute": Survive lethal damage from max HP once per deployment.

"Critical Response" / "Adrenal Surge": Heal on low HP once per deployment.

"Reckless Assault" / "Overcharge": Attack bonus + self-damage.

"Kinetic Backlash": Recoil damage based on damage dealt.

"Vampiric Strike" / "Essence Tap": Heal based on damage dealt.