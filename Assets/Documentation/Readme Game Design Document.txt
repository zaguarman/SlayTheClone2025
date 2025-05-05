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

Drawing Cards (Scheduled Draw): Players draw a base number of cards (e.g., 1) only at the start of their turn. No effect allows immediate drawing during a turn.

Draw Manipulation (Next Turn Draw Modification): Effects modify the number of cards drawn at the start of the next turn (e.g., "Schedule X additional draws", "Draw X fewer cards next turn").

Discard Pile: Zone for used Spells and destroyed Creatures.

Discarding from Hand: Choosing cards currently in hand and moving them immediately to the Discard Pile, typically as an additional cost or effect. Separate from modifying next turn's draw.

Banish: Removes card from the game permanently.

Card Lifecycle & Reshuffling:

Played Spells & destroyed Creatures go to their owner's Discard Pile.

Reshuffle Trigger & Process: If, during the Start of Turn draw phase, a player needs to draw one or more cards but their Draw Deck is empty, the following occurs:

Any remaining cards are drawn from the empty deck (which will be zero).

The player's entire Discard Pile is shuffled thoroughly to become their new Draw Deck.

The player then continues drawing cards from this newly formed Draw Deck until they have drawn the total number of cards scheduled for that turn's draw phase.

Creature stats are reset to their base values when their cards are part of the Discard Pile being reshuffled into the Draw Deck.

Requisites: Conditions for play/activation (e.g., sacrifice, discard from hand, board state).

III. Core Gameplay Loops & Mechanics

Information Warfare (Intelligence vs. Counter-Intelligence): Reveal vs. conceal pending actions/info.

Board Manipulation & Control:

Logistics (Movement): Abilities/Spells for moving/swapping.

Movement Restriction: Heavy status, abilities, Slot Effects.

Slot Effects / Environmental Hazards: Single effect per slot (new replaces old). Various triggers. Can block actions.

Hazard Removal / Decontamination: Cleansing slot effects.

Location (Weather): Global modifiers.

Flooding: Using numerous weak creatures.

Creature Enhancement & Protection: Setup, Survivability, Immunity/Protection, Cleansing.

Disruption & Control: Status Effects Application, Red Tape, Disable Archetype (Suppressed), Counter/Interference, Attack/Effect Redirection.

Recursion & Recovery: Revive, Return to Hand (from Battlefield or Discard).

Combat, Damage & Targeting: Direct Damage, Chain/Spread Damage, Armor, Life Drain/Siphoning, Targeting Nuances.

Action Queue & Turn Flow Mechanics: Queue Manipulation, Priority Enhancement, Global Priority Modifiers, Normal Tie-Breaking (Lowest HP -> Lowest Atk -> Random), Reversed Tie-Breaking (Highest HP -> Highest Atk -> Random), Priority Reduction.

IV. Detailed Mechanics & Specific Systems

Traps: Hidden effects (Reveal trigger, Manipulation trigger, Slot-based).

Status Effects (Detailed Definitions): Addicted, Blessed, Bleeding/Corroding, Bored, Caffeinated, Compromised, Concealed, Cursed, Delayed, Deployment Time, Depressed, Doomed, Heavy, Ostracized, Revealed, Suppressed, Targeted, Tired. (Full definitions as previously established).

V. Game Design Philosophy & Balancing

Game Tone: The game aims for a cynical, funny, and lighthearted tone, achieved through satire and exaggeration, often reflecting modern absurdities, bureaucracy, internet culture, and twisted mythic tropes. Flavor text, card names, and visuals contribute to this voice.

Flavor Preservation: During the iterative design process documented here, existing thematic elements (card names, ability names, flavor text, faction associations) should be considered stable and preserved unless a user explicitly requests a change to these elements. Mechanical changes should aim to integrate with existing flavor where possible.

Text Detail Preservation: No part of this document, including descriptions of mechanics, rules, status effects, examples, or design principles, should be simplified, shortened, condensed, or rephrased unless explicitly instructed to do so by the user. Modifications should involve additions, specific changes, or deletions as requested, maintaining the existing level of detail and phrasing in all other aspects.

Balancing Focus: Key priority. Rigorous playtesting and iteration needed.

Numerical Balance: Conservative numbers, careful stacking.

Status Effect Design Principle: Prioritize using existing status effects. Avoid creating new ones unless necessary. Promotes coherence, reduces complexity, encourages synergy.

Creature Ability Design Principle: Prioritize reusing existing core ability mechanics and trigger types. Avoid novel mechanics if effects can be achieved through combination, status effects, or varied triggers. Trigger conditions can be freely modified and combined.

Balancing Levers:

Opportunity Cost: Card choice, Slot Occupation, Sacrifice, Discarding from hand. The Scheduled Draw mechanic and Next Turn Draw Modification are key tempo and planning levers.

Space Limitations: Finite creature slots, hand size limits.

Domino Effects: Intentionally designed chain reactions.

Development Goals & Player Experience:

Reward: Strategic Planning, Creative Deckbuilding, Adaptation, Comebacks.

Avoid: Complicated Math, Unfair Situations. Focus on clear cause-and-effect.

Core Design Tenet - Double-Edged Sword: Positives have drawbacks; negatives have upsides.

VI. Illustrative Content Examples

Example Status Groupings: Information, Debuffs/Control, Buffs/Utility, Initial State.

Example Passive Ability Concepts: Ninja Step, Resolute, Adrenal Surge, Overcharge, Kinetic Backlash, Essence Tap.

Example Card Concepts: (Names, Factions, Flavor Text preserved as per rule in Section V)

Card Name: Plausible Deniability Protocol

Type: Spell / Faction: Shadow Operations

Effect: Target friendly creature gains Concealed 1. If that creature is destroyed by an opponent's action this turn or next turn, return it to your hand instead of the Discard Pile.

Flavor Text: "The Secretary disavows any knowledge..."

Card Name: Honeypot Agent

Type: Creature / Faction: Shadow Operations

Stats: 1/4/2 / DT: 2

Ability (Passive): Permanently Compromised. If Compromised triggers via opponent effect: Apply Suppressed 1 to the triggering enemy creature.

Flavor Text: "They thought they were getting secrets..."

Card Name: Oracle of Delphi's Ambiguous Warning (Revised)

Type: Spell / Faction: Myths & Mysteries

Effect: Look at top 3 deck cards. Choose 1 for top of deck, 1 for Discard Pile, Banish the last. Apply Cursed 1 randomly.

Flavor Text: "The threads of fate show... choices!"

Card Name: Influencer Apology Video

Type: Spell / Faction: Zeitgeist & Archetypes

Effect: Target friendly creature gains Blessed. Opponent schedules +1 draw next turn. Apply Revealed 1 to target creature.

Flavor Text: "I'm taking accountability... Link in bio!"

Card Name: Keyboard Warrior

Type: Creature / Faction: Zeitgeist & Archetypes

Stats: 2/1/3 / DT: 0

Ability (Passive): OnAttack: Gains Concealed 1. End of Turn: If did not attack, gains Bored 1.

Flavor Text: "U MAD BRO? XD"

Card Name: The Grind™ Mindset

Type: Spell / Faction: Zeitgeist & Archetypes

Effect: Target friendly creature gains Caffeinated 3. Apply Depressed 1 to ALL other friendly creatures.

Flavor Text: "Sleep is for the weak! CRUSH IT!"