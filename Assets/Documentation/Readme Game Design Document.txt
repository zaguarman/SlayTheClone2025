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

Addicted (X turns | Requires: Stat Alteration):

At the beginning of its controller's turn, if this creature did not have its Attack, Health, or Speed stats directly altered (increase or decrease) by any effect during the previous turn, it gains Suppressed 1.

Check occurs each turn for the duration X.

Theme: Dependency, withdrawal.

Blessed:

Persists until consumed.

Negates the next negative status effect that would be applied to this creature, then Blessed is removed.

Does not stack (applying Blessed to an already Blessed creature has no effect).

Theme: Single-use ward, divine protection.

Bleeding / Corroding (X turns):

At the End of its controller's Turn, this creature takes 1 damage.

Lasts X turns. Stacks duration (applying Bleeding 2 to a creature with Bleeding 3 results in Bleeding 5).

Damage taken from this effect does trigger "OnDamage" passive abilities.

Theme: Damage over time, wounds, decay.

Bored (X turns):

At the beginning of its controller's turn, if this creature was not attacked or targeted by an ability during the previous turn, it skips performing its queued action this turn, and its passive abilities do not trigger this turn.

Heals 1 HP at the End of Turn regardless of whether it acted or not.

Lasts X turns.

Theme: Apathy, complacency, disinterest leading to inaction.

Caffeinated (X turns):

This creature's actions have +1 Priority.

This creature gains +1 Attack.

Takes 1 damage at the End of its controller's Turn.

Lasts X turns. Cannot be cleansed. Stacks duration.

Applying Caffeinated cleanses (removes) the Tired status effect.

Theme: Stimulated, hyperactive, temporary boost followed by burnout.

Compromised:

Persists until triggered once.

When this creature is successfully targeted by an enemy "Intelligence" action, Compromised triggers a negative effect for its controller (e.g., controller draws 1 fewer card next turn, controller takes 1 damage, apply Revealed 1 to this creature or its slot). The specific drawback is defined by the card that applied Compromised.

After triggering, Compromised is removed.

Can potentially be applied secretly (opponent doesn't know it's there until triggered).

Theme: Leaky information, double agent, hidden vulnerability, honeypot.

Concealed:

Persists until the creature's next action resolves or the status is removed/overridden.

This creature's planned action (target, destination slot for movement) is hidden or obscured (e.g., shows as "?", or provides false information like targeting a different slot) in the opponent's preview of the action queue.

Overrides the Revealed status (if both are present, Concealed takes precedence).

Theme: Secrets, hidden intent, misdirection, plausible deniability.

Cursed (X turns):

At the end of its controller's turn, apply a random negative status effect to this creature from a predefined pool of possibilities (e.g., Bleeding 1, Delayed 1, Depressed 1).

Lasts X turns. Stacks duration.

Theme: Bad luck, persistent misfortune, plagued.

Delayed (X turns):

For the next X turns, actions performed by this creature have their base Priority lowered by 1.

Stacks additively (Delayed 1 + Delayed 1 = Delayed 2, resulting in -2 Priority). Duration resets/extends with new applications.

Theme: Sluggishness, lag, slow response.

Deployment Time (X turns):

Applied automatically when a creature enters the battlefield (from hand or Revive).

For X turns, the creature cannot perform any actions (Attacks, Active non-Attack Abilities), and its passive abilities do not trigger (exceptions may exist for specific passives explicitly stated to work during deployment).

The duration decreases by 1 at the start of its controller's turn. When it reaches 0, the creature can act normally.

Theme: Initialization, mobilization, summoning sickness, arrival time.

Depressed (X turns):

This creature's Attack stat is halved (rounded down or up, consistently defined).

Lasts X turns. Stacks duration.

If applying Depressed causes the creature to have 3 or more total turns of Depressed active simultaneously, all stacks of Depressed are immediately removed, and the creature gains the Doomed status effect instead.

Theme: Morale loss, reduced combat effectiveness, despair.

Doomed (X turns):

The creature will be destroyed after X turns (typically at the end of the Xth turn after application).

While Doomed, the creature gains +1 Attack at the start of each of its controller's turns.

While Doomed, the creature cannot gain the Depressed status effect (attempts to apply Depressed fail).

Cannot be cleansed.

Cannot stack duration; subsequent applications of Doomed on an already Doomed creature are ignored.

Theme: Marked for death, final desperate surge, inevitable fate.

Heavy (X turns):

This creature cannot perform Move actions (actions primarily focused on changing slots) and cannot be moved by other card effects (friendly or enemy).

Gains 1 Armor at the end of each of its controller's turns while Heavy remains.

Lasts X turns. Stacks duration.

Theme: Anchored, immovable, fortified but immobile.

Ostracized (X turns):

This creature cannot be targeted by any actions or abilities (friendly or enemy). It cannot gain any new status effects.

Existing status effects on the creature remain and function normally (e.g., Bleeding continues to deal damage). Passives continue to trigger if conditions met. Creature can still perform actions if able.

Lasts X turns. Stacks duration.

Theme: Isolation, untouchable, shunned, phased out.

Revealed (X turns):

This creature's planned action (target, destination slot for movement) is visible to the opponent in their preview of the action queue.

Decreases by 1 turn at the end of each turn. Can stack duration.

Is overridden by Concealed if both are present.

Theme: Information leak, under surveillance, exposed plans.

Suppressed (X turns):

This creature cannot perform any actions (including Attacks, Active non-Attack Abilities), except for Move actions (actions primarily focused on changing slots initiated by abilities).

This creature's passive abilities do not trigger.

Lasts X turns. Stacks duration.

Represents being significantly hindered or shut down in most capacities but retaining basic mobility.

Theme: Major disablement, suppression, neutralized, censored, silenced.

Targeted (X turns):

This creature takes X additional damage from all sources (Attacks, Abilities, Status Effects like Bleeding, Slot Effects).

Stacks intensity (Targeted 1 + Targeted 1 = Targeted 2, meaning +2 damage taken). Duration refreshes/extends with new applications.

Theme: Defenses breached, weak point identified, vulnerable.

Tired (X stacks):

This status uses stacks rather than turns. Each application adds a stack.

Effect depends on stack count at the time of potential action resolution:

1 stack: Will gain Suppressed 1 in 2 turns (countdown starts now).

2 stacks (gained in same turn or across turns): Will gain Suppressed 1 next turn.

3+ stacks (gained in same turn or across turns): Immediately gain Suppressed 1 this turn. If the creature had an action queued (other than Move), that action is removed from the queue.

Applying Caffeinated removes all stacks of Tired. Resolving Suppressed from Tired likely removes Tired stacks.

Theme: Gradual exhaustion leading to shutdown.

V. Game Design Philosophy & Balancing

Balancing Focus: Key priority.

Numerical Balance: Conservative numbers, careful stacking.

Balancing Levers: Opportunity Cost (Card choice, slot occupation, sacrifice, discarding), Space Limitations, Domino Effects.

Development Goals & Player Experience:

Reward: Strategic Planning, Creative Deckbuilding & Synergies, Adaptation, Comebacks.

Avoid: Complicated Math, Unfair Situations (focus on clear cause-and-effect).

Core Design Tenet - Double-Edged Sword: Positives have drawbacks; negatives have upsides.

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