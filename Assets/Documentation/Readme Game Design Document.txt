Game Design Document

I. Core Concept, Vision & High-Level Interactions

Game Premise: A card game focused on outsmarting the opponent through strategic planning, information control, and tactical positioning.

Genre: Roguelite Deckbuilder – progressively build and refine your deck during a run.

Central Conflict: Intelligence versus Counter-Intelligence, thematically mirrored by Control (Myth, State factions) versus Chaos (Shadow, Zeitgeist factions).

Primary Goal: Outsmart opponents by gathering/denying information, controlling the location, manipulating status effects, and executing strategic creature movement.

Key Interaction Space - Tactical Positioning: Creature placement on the battlefield is crucial. Positioning influences movement blocking, target validity, adjacency effects, and board control. Creatures physically occupy slots, preventing movement into/through unless specific abilities allow crossing or swapping.

Key Mechanic - Fog of War: Opponent pending actions (movements, attack targets, ability targets) for the current turn are hidden in the action queue by default. Intelligence actions aim to reveal these pending actions before they resolve. Resolved actions become immediately visible to both players upon resolution. Revealed information from Intelligence effects typically persists only until the end of the turn unless actively concealed again.

II. Foundational Game Systems & Rules

Faction System:

Cards belong to factions (e.g., Statecraft, Shadow Factions, Myth, Zeitgeist).

Deckbuilding allows faction combinations (like MtG colors).

Thematic Associations: Control (Myth, State), Chaos (Shadow, Zeitgeist).

Faction-based interactions: Soft weaknesses/resistances (e.g., weak to 'Attacks', resistant to 'Mental Debuffs', bonus vs. specific faction).

Action Economy & Tempo:

No resource cost (mana/energy) to play cards.

Tempo managed by Deployment Time (X): (See Section IV for full definition) Represents initialization/mobilization. Prevents actions and passive triggers for X turns. Applies to creatures entering play from hand or via revival effects.

Action Types & Triggering:

Creatures have core stats: Attack, Health, Speed. These stats are reset to their base values when a creature card is reshuffled from the discard pile back into the deck.

Creature Abilities:

Always passive, triggering automatically based on game events. Targeting predetermined or random.

Example Triggers: OnDamage, OnAttack, OnFriendlyCreatureMove, OnRevealed, OnTurnStart/OnTurnEnd, OnHealthBelow(X)% (once per deployment).

Movement via Abilities/Spells: No default "Move" action. Movement initiated only through abilities or Spells.

Special Creature Archetypes:

Support Units / Specialists: Active non-Attack abilities queued by player. Subject to Deployment Time.

Automated Units: Automatic passive triggers each turn. Target predetermined.

Creature Attacks: Actively queued by the player.

Spells: Active actions played directly by the player. Can initiate movement or other effects.

Determinism: Game effects are deterministic; no percentage-based chances for failure.

Card Management:

Drawing Cards: Standard mechanic.

Draw Manipulation: Effects modify cards drawn per turn.

Discard Pile: A zone where used Spells and destroyed Creatures are placed.

Discarding: Some effects may require or allow players to discard cards directly from their own hand. Discarded cards typically go to the Discard Pile. Discarding cards can sometimes be used as an additional cost or resource for playing specific powerful cards or activating certain abilities.

Banish: A mechanic that removes a card from the game entirely. Banished cards do not go to the Discard Pile and cannot be returned to play, hand, or deck by any means.

Card Lifecycle & Reshuffling:

When a Spell card is played and resolves, it is placed into its owner's Discard Pile.

When a Creature is destroyed (reduced to 0 HP or by a "destroy" effect), its card is placed into its owner's Discard Pile.

When a player attempts to draw a card from an empty Draw Deck, their Discard Pile is shuffled to become their new Draw Deck.

Stat Reset: When creature cards are reshuffled from the Discard Pile back into the Draw Deck, any modifications (stat changes, potentially persistent status effects acquired in the previous deployment) are reset to the card's original base values.

Requisites: Conditions required to play/activate certain cards (e.g., sacrifice creature, discard X cards, X creatures in play, total friendly stat > Y).

III. Core Gameplay Loops & Mechanics

Information Warfare (Intelligence vs. Counter-Intelligence):

Intelligence: Reveal hidden pending info.

Counter-Intelligence: Conceal actions/info.

Board Manipulation & Control:

Logistics (Movement): Abilities/Spells involving moving/swapping creatures. Includes Post-Action Repositioning and Positional Relay effects.

Movement Restriction: Heavy status, specific abilities, Slot Effects.

Slot Effects / Environmental Hazards: Status effects on slots. Only one per slot (new replaces old). Can be hidden. Various triggers (On Move In/Out, Continuous, Reactive, One-Time). Can block actions (e.g., Movement Jammer).

Hazard Removal / Decontamination: Cleansing slot effects.

Location (Weather): Global modifiers.

Flooding: Using numerous weak creatures for board presence/obstruction.

Creature Enhancement & Protection:

Setup: Increasing stats.

Survivability: Heal, Armor, redirection.

Immunity/Protection: Granular immunity.

Cleansing: Removing negative status from creatures/slots.

Disruption & Control:

Status Effects Application: Applying status effects (see Section IV).

Red Tape: Delaying actions, lowering priority.

Disable Archetype: Disabling functions (Suppressed).

Counter / Interference: Stopping/altering queued actions.

Attack/Effect Redirection: Changing targets.

Recursion & Recovery: Abilities focused on bringing back cards or units.

Revive: An ability or Spell effect that takes a specified creature card from the Discard Pile and returns it to the battlefield. The revived creature enters with a set amount of HP (e.g., half max HP, full HP - determined by the card effect) and is subject to Deployment Time.

Return to Hand (from Battlefield): An ability or Spell effect that removes a creature from the battlefield and places its card into its owner's hand.

Return to Hand (from Discard): An ability or Spell effect that takes a specified card (Creature or Spell) from the Discard Pile and places it into its owner's hand.

Combat, Damage & Targeting:

Direct Damage: Reducing HP.

Chain/Spread Damage: Multi-target effects.

Armor: Temporary HP, depleted first. Recoil hits Armor first.

Life Drain / Siphoning: A mechanism (typically a passive ability) where dealing damage heals the source. Example: "When this creature deals damage with its Attack, its controller heals HP equal to 50% of the damage dealt." Interaction with Armor: Life Drain typically calculates based on damage dealt to HP or Armor.

Targeting Nuances: Specific patterns or restrictions.

Action Queue & Turn Flow Mechanics:

Queue Manipulation: Interacting with the hidden pending action queue.

Priority Enhancement: Granting higher priority.

Global Priority Modifiers: Affecting all actions (e.g., Coordinated Advance, System Scramble).

Normal Tie-Breaking: (Equal Priority/Speed) 1. Lowest current Health resolves first. 2. Lowest Attack resolves first. 3. Random.

Reversed Tie-Breaking: (System Scramble) 1. Highest current Health resolves first. 2. Highest Attack resolves first. 3. Random.

Priority Reduction: Lowering action priority.

IV. Detailed Mechanics & Specific Systems

Traps: Hidden effects (Reveal trigger, Manipulation trigger, Slot-based).

Status Effects (Detailed Definitions):

Addicted, Blessed, Bleeding/Corroding, Bored, Caffeinated, Compromised, Concealed, Cursed, Delayed, Deployment Time, Depressed, Doomed, Heavy, Ostracized, Revealed, Suppressed, Targeted, Tired. (Definitions unchanged).

V. Game Design Philosophy & Balancing

Balancing Focus: Key design priority.

Numerical Balance: Conservative numbers, careful stacking.

Balancing Levers: Opportunity Cost (Card choice, slot occupation, sacrifice, discarding), Space Limitations, Domino Effects.

Development Goals & Player Experience:

Reward: Strategic Planning, Creative Deckbuilding & Synergies, Adaptation to opponent actions and game state, opportunities for Comebacks.

Avoid: Overly complex calculations required mid-turn (Complicated Math), situations that feel inherently unwinnable due to luck rather than strategy (Unfair Situations). Focus on clear cause-and-effect for actions and consequences.

Core Design Tenet - Double-Edged Sword:

Major positives have drawbacks/risks. Major negatives have upsides/compensation. All cards have effects, ideally drawbacks.

Examples: "OnMove: +1 Attack" + "OnAttacked: Controller takes 1 damage"; "Doomed" + Attack bonus; "Reckless Assault" (Attack bonus + recoil damage).

VI. Illustrative Content Examples

Example Status Groupings: Information, Debuffs/Control, Buffs/Utility, Initial State.

Example Card Concept 1: AI Psychologist (As before)

Example Card Concept 2: Cult Ritualist (As before)

Example Passive Ability Concepts:

"All-Terrain Protocol" / "Ninja Step": Ignores negative slot effects on movement.

"Defiance Protocol" / "Resolute": First time reduced to 0 HP from max HP, survive with 1 HP (once per deployment).

"Critical Response" / "Adrenal Surge": (Trigger: OnHealthBelow50%) Heal 2 HP (once per deployment).

"Reckless Assault" / "Overcharge": Attacks +1 damage. Takes 1 recoil damage after attacking (hits Armor first, can be lethal).

"Kinetic Backlash": Takes recoil damage = 50% of damage dealt after attacking (hits Armor first, can be lethal).

"Vampiric Strike" / "Essence Tap": When this creature deals damage with its Attack, its controller heals HP equal to 50% (rounded up) of the damage dealt.