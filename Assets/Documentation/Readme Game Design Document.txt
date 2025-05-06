DOCUMENT USAGE GUIDELINES:

1.  Preservation of Existing Content: All existing thematic elements (card names, ability names, flavor text, factions, specific examples) are stable and must be preserved unless the user explicitly requests a change to a specific element.
2.  Mechanical Alignment: Mechanical changes must align with existing flavor.
3.  No Simplification/Condensation: No part of this document (concepts, mechanics, rules, status definitions, examples, principles) should be simplified, shortened, condensed, or significantly rephrased unless explicitly instructed by the user. Maintain existing detail, nuance, and phrasing in all other sections when implementing user requests.
4.  Example Card List Integrity: The "Illustrative Card Examples List" (a separate document) is fixed. The number of cards on that list cannot be modified (no additions or unsolicited removals) unless explicitly requested by the user. If an existing card example needs mechanical adjustment to fit a new rule, its core concept and flavor must be preserved.

Game Design Document

I. Core Concept & Vision

Premise: A card game focused on outsmarting opponents via strategic planning, information control, and tactical positioning on a constrained battlefield.

Genre: Roguelite Deckbuilder. Players build and modify their deck during a run, making card choices and adapting to challenges crucial.

Central Conflict: The core tension revolves around Intelligence versus Counter-Intelligence. Thematically, this is mirrored by a multifaceted conflict:

Established pyramidal powers (Statecraft, Myth, Corporate) strive to impose and maintain their specific paradigms of order and control, often by manipulating information, enforcing dogma, and preserving hierarchical structures.

Decentralized, emergent forces (Digital Society, Zeitgeist, Mind) challenge these established norms, thriving on fluidity, the free flow of (and sometimes weaponized) information, and the unpredictable power of collective or individual consciousness.

Navigating and exploiting this dynamic are clandestine networks and transformative systems (Shadow Ops., Tech, Applied Sci-Fi), which wield sophisticated tools of intelligence, counter-intelligence, disruption, and innovation. These entities may serve various masters, subvert existing powers, or pursue their own distinct agendas within this complex web of influence.

Primary Goal: Outsmart opponents by:

Gathering info on opponent's hidden plans.

Denying info to the opponent.

Controlling the global 'Location' effect (currently prototyped as 'Weather,' but will encompass various environmental or zone-wide states that fulfill the same role).

Creating/manipulating status effects on creatures and slots.

Strategically moving creatures (friendly or enemy) for advantage.

Key Interaction: Tactical Positioning

Battlefield: Each player controls one row of 5 creature slots, facing each other symmetrically.

Slot Occupation: Max one creature per slot. Placement is key.

Adjacency: Non-edge slots have 2 adjacent friendly slots (left/right) and 1 opposing enemy slot. Edge slots have 1 adjacent friendly and 1 opposing enemy slot.

Movement & Blocking: Placement dictates movement paths, targets (adjacency/position), triggers passive effects, and controls the board. Creatures block their slot; other creatures cannot move into or through it unless an ability specifically allows crossing, swapping, or pushing/pulling. Crucially, creatures cannot move freely; all movement requires specific Spells or creature abilities. A creature changing sides (e.g., due to Recruitment/Conversion effects as defined in Section IV.D) moves to an available slot on the new controller's side. It cannot be voluntarily moved into an opponent's slot by its current controller. Friendly creatures cannot move into enemy slots unless explicitly recruited or converted by an enemy effect, at which point they become effectively enemy creatures under the opponent's control.

Key Mechanic: Fog of War

Default State: Opponent's pending actions this turn (movements, attack targets, ability targets) are hidden in the action queue until resolution.

Intelligence: Actions that reveal opponent's hidden information. Vital for strategy.

Counter-Intelligence: Actions that exploit or deepen the fog of war.

Resolution: Actions become visible to both players once they resolve.

Revealed Info: Information gained via Intelligence typically lasts only until the end of the current turn, unless concealed again by Counter-Intelligence.

II. Foundational Game Systems & Rules

A. Faction System
All cards are categorized into distinct factions. The primary factions include: Shadow Ops., Statecraft, Myth, Corporate, Digital Society, Zeitgeist, Mind, Tech, and Applied Sci-Fi.
Deckbuilding allows and encourages combinations of factions within a single deck, similar to color pairs or shards in Magic: The Gathering. This system promotes diverse deck archetypes built around combined faction strategies and identities.
(Placeholder - Idea for Future Development): Faction-based interactions exist as a soft, granular version of type matchups, providing subtle advantages or disadvantages rather than hard counters. This can manifest as creatures receiving modified outcomes when targeted by actions originating from specific factions, reflecting the thematic relationships and conflicts between them. These benefits or drawbacks are intended to be minor, such as a 1-point damage reduction when an attack is resisted, a slight increase or decrease in the duration of a status effect, or immunity to specific, less impactful status effects (e.g., a minor debuff), rather than creating hard counters or overwhelming advantages.

B. Detailed Faction Themes & Relationships
Shadow Ops.: Embodies: Chaos (in its disruptive potential), Unknown (its hidden nature), Control (its manipulative methods), Covert (its operational mode), Institution (as an organized, albeit clandestine, entity). Operates within a centralized pyramidal structure, viewing its assets with stark utilitarianism – instruments to be employed and, once their purpose is served or they become a liability, to be expunged with clinical precision, leaving no trace. Its loyalties can be complex, sometimes serving, sometimes undermining the established Order for its own interests.
Statecraft: Embodies: Order (its aim to govern), Structure (through its hierarchies and laws), Known (its public-facing pronouncements), Control (through legislation and enforcement), Institution (as the formal apparatus of governance). It is inherently a centralized pyramidal structure built upon a community-based ethos (however skewed or enforced), designed for top-down command and the preservation of the Status Quo. It relies on Tradition and maintains wary, transactional relationships with other pyramidal powers.
Myth: Embodies: Tradition (keeper of appropriated beliefs), Dogma (presenting a facade of Harmony), Influence (shaping culture and morality through a community-based narrative). Typically operates as a centralized pyramidal structure, fostering a culture of willing sacrifice. It proffers solace from existential dreads it subtly cultivates or even personifies, demanding devotion as the sole bulwark against the very shadows it defines, thereby binding its followers to its will.
Corporate: Embodies: Order (within its structures, aimed at profit), Structure (hierarchical organization), Control (over resources and markets, wielding significant raw Power), Influence (economically and politically). It is a quintessential centralized pyramidal structure driven by an unyielding, individualistic hunger. Its grand narrative of self-made triumph serves as a meticulously crafted lure, a superficial veneer of meritocracy designed to enlist aspirants into its machinery, transforming their ambitions into fuel for its ceaseless expansion. Within its sphere, the currents churn with predatory instinct as entities devour or subsume rivals in a relentless pursuit of dominance.
Digital Society: Embodies: Chaos (its emergent, unpredictable nature, amplified by the overwhelming flood of information whose sheer size and constant shift make it unmanageable and inherently uncontrollable), Flow/Fluidity (rapidly evolving trends), Innovation/Novelty (constantly generating new expression), Autonomy (resisting direct Control), Emergence (as a collective phenomenon). It thrives as a decentralized flat structure, a vast, interconnected community of ephemeral thought and expression, sharing a deep, synergistic, and fraternal relationship with Zeitgeist.
Zeitgeist: Embodies: Flow/Fluidity (the shifting spirit of an era), Innovation/Novelty (driving new cultural trends), Unknown (its emergent direction), Influence (shaping public opinion), Chaos (disrupting established norms), Emergence (reflecting a community's collective consciousness and subconscious). It operates as a decentralized flat structure, synergizing strongly with Digital Society.
Mind: Embodies: Critical Discernment (the faculty of rigorous thought, deconstructing illusions and exposing manipulation), Autonomy (individual consciousness resisting Control), Influence/Manipulation (shaping thoughts subtly), Unknown (depths of the subconscious). Represents the power of individual and networked thought, operating as a decentralized flat structure. It is a potent counter to the dogma of pyramidal Myth and the seductive illusions of Corporate, sharing a natural intellectual kinship with Applied Sci-Fi.
Tech: Embodies: Innovation/Novelty (rapid technological advancement), Structure/System (its power derives from vast, intricately interwoven systems, a lattice of dependencies capable of extraordinary output, yet this very interconnectedness renders it acutely susceptible to cascading failures or targeted disruption), Control (offering tools for information control), Flow/Fluidity (constant evolution), Power (immense impact when leveraged, yet this power can sometimes surge beyond the grasp of its creators, an unpredictable current of its own).
Applied Sci-Fi: Embodies: Innovation/Novelty (its domain is the realized frontier of human ambition, where once-speculative marvels—advanced material sciences, engineered biospheres, the very reshaping of worlds—are wielded as tangible instruments of will), Empirical Scrutiny (the rigorous application of the scientific method), Knowledge (its core strength lies in deciphering the universe's mechanisms and mastering their application, built upon a foundation of audacious theory that has already dared to chart the contours of existence, from the quantum weave to the cosmic tapestry), Order (in its methodologies). It shares a natural kinship with Mind, as both are dedicated to the rigorous deconstruction of the apparent and the relentless pursuit of underlying truths—one through introspective clarity, the other through empirical dominion over the material. Its profound insights are often coveted for exploitation by Statecraft and Corporate.

C. Action Economy & Tempo
No resource cost (mana/energy) to play cards from hand. Play limited by hand size and timing.
Tempo managed primarily by Deployment Time (X) (see Section IV.B for full definition). This represents the time it takes for a creature to become fully operational after entering play, though this duration can be manipulated by certain game effects like Spells, creature abilities, or Locations.

D. Action Types & Triggering
1. Creature Stats: Attack, Health, Speed (influences action order with Priority). Stats can be modified.
2. Creature Abilities (Passive): Always passive. Trigger automatically on specific game events (e.g., OnDamage, OnAttack, OnFriendlyCreatureMove, OnRevealed). Targeting for effects is predetermined by card text (e.g., "target the attacker," "adjacent creatures," "the creature that triggered this") or random; targeting is never chosen by the player at the time the passive ability triggers. All decisions regarding targets for passive effects are implicitly defined by the card's rules or resolved randomly if specified. (See Section IV.C Trigger Events for detailed list).
3. Special Creature Archetypes:
Support/Specialists: May lack a standard Attack. Might have an Active Ability (e.g., deal damage, heal, apply status) that the player queues during their planning phase each turn, selecting any required targets at that time of queuing, per the ability's rules. Subject to Deployment Time and queue mechanics.
Automated Units: May lack an Attack. Have powerful passive abilities triggering automatically each turn (e.g., OnTurnStart: deal 1 damage randomly; OnTurnEnd: grant 1 Armor adjacently). Target is predetermined by the ability.
4. Creature Attacks: Actively queued by the player during planning. Target selection might be restricted by rules (e.g., only target slot in front, cannot target Concealed). Targets are chosen when the attack is queued.
5. Spells: Active actions. Played from hand (e.g., by dragging and dropping onto a target), activated by player, often with chosen targets selected at the time of play. Effects resolve immediately or are queued. Can cause movement, status, damage, healing, etc.
6. Determinism: Game effects are deterministic. No percentage-based randomness for success/failure (except explicit random targeting). Outcomes are predictable based on game state and interactions.

E. Card Management
1. Drawing Cards (Scheduled Draw):
Players draw a base number of cards (e.g., 1) only at the Start of their Turn. This is the only time cards naturally enter hand from the deck.
The number drawn = Base Draw + Sum of all positive/negative draw modifiers accumulated during the previous turn.
No effect allows drawing cards immediately from the deck mid-turn.
2. Draw Manipulation (Next Turn Draw Modification): Affects future draws.
Card advantage effects: "Schedule X additional card draws for your next turn." Increments a counter for the next turn's draw.
Card disadvantage effects: "Draw X fewer cards next turn" / "Scheduled draw reduced by X." Decrements the counter (min 0 cards).
Modifiers accumulate additively during a turn; resolved together for the next turn's Start of Turn draw calculation.
3. Discard Pile: Zone for used Spells and destroyed Creatures.
4. Discarding from Hand:
Action: Player selects card(s) in hand -> moves them immediately to Discard Pile.
Use: Primarily as an additional cost ("As an additional cost..., discard 3 cards") or part of an effect ("Deal 3 damage..., then discard 1 card").
Distinction: Different from Draw Manipulation. Discarding from Hand is immediate, affecting current hand. Modifying next turn's draw is delayed, affecting future resources.
5. Banish: Removes a card (from hand, board, discard, deck) from the game permanently. Banished cards are outside all zones, irretrievable (no Revive, Return to Hand, etc.).
6. Card Lifecycle & Reshuffling:
Spell played -> Resolves -> Goes to Discard Pile.
Creature destroyed (0 HP or "destroy" effect) -> Goes from battlefield to Discard Pile.
Reshuffle Trigger: Activates only when required to draw at Start of Turn, but Draw Deck has fewer cards than scheduled.
Reshuffle Process:
Attempt to draw scheduled cards. Draw any remaining from Draw Deck (could be 0).
If Draw Deck is empty and draw requirement unmet: Shuffle entire Discard Pile -> becomes new Draw Deck.
Immediately continue drawing from new Draw Deck until total drawn matches the scheduled number for that Start of Turn phase.
7. Creature Reset: When creature cards reshuffle from Discard to Deck, temporary mods (current HP changes, temp stats, some statuses) reset to printed base values.
8. Requisites: Some powerful cards require conditions met before play/activation (e.g., sacrifice creature, discard X cards, have X creatures in play, total friendly Attack > Y).
9. Opponent Hand Size Checks: Due to the simultaneous nature of player turns and action resolution, any game effects or abilities that check the number of cards in an opponent's hand (e.g., for conditional effects, targeting, or information gathering) will perform this check at the End of the current Turn, after all other actions for the turn have resolved. This ensures a stable and definitive hand state for such checks.

F. Action Queue & Turn Flow Mechanics
1. Queue Manipulation: Actions interacting with opponent's hidden queue (reveal, modify parameters, destroy actions, reorder opponent's view).
2. Priority Enhancement: Granting actions higher Priority value -> resolve earlier, regardless of Speed. (Similar to Pokémon priority). Can be innate on creature actions ("Priority Attack: +1 Priority").
3. Global Priority Modifiers: Effects impacting all actions' Priority for a duration.
Example ("Coordinated Advance"): Friendly actions this turn & next 3 turns get +1 Priority.
Example ("Temporal Distortion"): For X turns, the normal action resolution order based on Speed is inverted (similar to Pokémon's Trick Room). Actions with higher Priority still resolve before actions with lower Priority, in their normal Speed order (or subsequent tie-breakers if Speed is tied for these Priority actions). However, among actions of the same Priority level (including actions with no inherent Priority modifiers), creatures with lower Speed will act before creatures with higher Speed.
4. Normal Tie-Breaking: If actions tie in Priority & creature Speed:
Lowest current (Health + Armor) resolves first.
If (Health + Armor) tied, lowest base Attack resolves first.
If Attack tied, resolve randomly (with potential minor influence from the Karma System if implemented and active to mitigate extreme luck streaks).
5. Tie-Breaking During Temporal Distortion: If actions that are subject to the inverted Speed order (i.e., they have the same Priority level) also tie in Speed:
Lowest current (Health + Armor) resolves first.
If (Health + Armor) tied, lowest base Attack resolves first.
If Attack tied, resolve randomly (with potential minor influence from the Karma System if implemented and active to mitigate extreme luck streaks).
6. Priority Reduction: Actions or effects that assign a negative modifier to an action's base Priority (e.g., Priority -1). This causes the affected action to resolve later in the turn's queue, after actions with higher (or the default 0) Priority. If the reduction is significant, the action might resolve even after actions from slower creatures that have a higher effective Priority.

III. Core Gameplay Loops & Mechanics

A. Information Warfare
Constant interaction between revealing opponent's hidden plans (Intelligence) and concealing yours or setting traps (Counter-Intelligence).

B. Board Manipulation & Control
1. Logistics (Movement): Use abilities/Spells to reposition creatures (self, allies, enemies) or swap positions. Optimizes attacks, blocks, adjacency; disrupts opponents. Includes tactics like Post-Action Repositioning ("Hit and Run"), Positional Relays (buffs on moving into vacated slot).
2. Movement Restriction: Use Heavy status, specific abilities, or Slot Effects to lock down enemies or protect allies.
3. Slot Effects / Environmental Hazards: Persistent or triggered effects on battlefield slots. Rule: Only one effect per slot; new replaces old. Can be visible or hidden traps. Triggers vary (On Move In/Out, Continuous Turn Start/End, Reactive, One-Time). Can block actions (e.g., "Movement Jammer").
4. Hazard Removal / Decontamination: Actions/abilities to cleanse negative Slot Effects, resetting them or replacing with neutral/beneficial effects.
5. Location (Weather): Cards imposing global modifiers on the whole battlefield (duration or until changed). Can restrict (no certain card types), buff (+1 Armor/turn), trigger effects (heal all), enable strategies.
6. Flooding: Overwhelm board with many weak/token creatures ("junk") to obstruct movement, dilute single-target effects, enable quantity-based strategies.
7. Recruitment / Conversion: Taking control of enemy creatures to use them against their former owner or deny the opponent a resource. (See Section IV.D for details).

C. Creature Enhancement & Protection
1. Setup: Actions/abilities increasing core stats (Attack, Health, Speed) of self or allies.
2. Survivability: Actions like Heal (restore HP), Add Armor (temp HP buffer), abilities redirecting attacks (from weak to durable, or back at attacker).
3. Immunity/Protection: Granular immunity negating specific negative effects (e.g., 'Mental Debuffs', non-friendly movement, direct damage types, AoE damage, Intelligence reveal). Not blanket invulnerability.
4. Cleansing: Actively removing negative status effects from friendlies or slots (via active ability, passive trigger, timed effect, recurring effect).

D. Disruption & Control
1. Status Effects Application: Core mechanic of applying defined status effects (Section IV.B) to enemies/slots to hinder, expose, or control.
2. Red Tape: Effects delaying opponent actions, lowering Priority (resolve later), adding Deployment Time turns. Slows tempo.
3. Disable Archetype: Strategies disabling key enemy functions (granularly: passive only, Attack only; or significantly: Suppressed - blocks most actions but allows ability-initiated Move).
4. Counter / Interference: Abilities interacting with opponent's hidden queue to stop a specific queued action. Might remove it or replace it (potentially with a detrimental action).
5. Attack/Effect Redirection: Abilities changing an opponent's queued action target (to self, ally, random valid, or back to an enemy/source).
6. Recruitment / Conversion / Mind Control: Effects that take control of an opponent's creature, either temporarily or permanently, disrupting their board and plans. (See Section IV.D for details).

E. Recursion & Recovery
1. Revive: Ability/Spell selects creature card from Discard -> returns to empty battlefield slot. Enters with specified HP (e.g., half max, full), subject to Deployment Time.
2. Return to Hand (from Battlefield): Ability/Spell removes creature (friendly/enemy) from slot -> puts card into owner's hand. Allows redeploy or protection.
3. Return to Hand (from Discard): Ability/Spell selects card (Creature/Spell) from Discard -> puts into player's hand immediately. Available to play again.

F. Combat, Damage & Targeting
1. Direct Damage: Reducing target's Health.
2. Chain/Spread Damage: Hits multiple distinct targets (sequentially or simultaneously).
3. Armor: Temporary HP pool on creatures. Damage hits Armor first, then Health. Can be removed by specific effects. Recoil damage also hits Armor first.
4. Life Drain / Siphoning: Dealing damage also heals the source/controller (often based on damage dealt to HP or Armor). Typically a passive ability.
5. Targeting Nuances: Many effects have specific restrictions/patterns (e.g., only slot in front, any slot except front, geometric patterns like 'V'/' +', random among valid pool, condition-based like only Concealed/Depressed/high Attack targets).

IV. Detailed Mechanics & Specific Systems

A. Traps
Hidden triggers activated by opponent interaction.
Type 1: Passive ability/effect triggers when revealed by enemy Intelligence (e.g., "Trap Card: When revealed..., deal 1 damage to enemy player").
Type 2: Queued action with hidden rider. If opponent manipulates that specific action (Counter, Delay, Reorder), rider triggers negative effect on opponent (e.g., "If this action is countered, opponent discards 1 card").
Slot-based traps via hidden Slot Effects are possible.
All trap effects trigger automatically based on their conditions; no player choice is made at the time of triggering.

B. Status Effects (Detailed Definitions)
Addicted (X turns): At the beginning of its controller's turn, if this creature did not have its stats directly altered (increase or decrease) by any effect during the previous turn, it gains Suppressed 1. This check occurs each turn for the duration X. Theme: Dependency on continuous stat alterations; withdrawal occurs if stat alterations cease.
Blessed: Persists until consumed. Negates the next negative status effect that would be applied, then Blessed is removed. Does not stack. Theme: Single-use ward, divine protection.
Bleeding / Corroding (X turns): At the End of its controller's Turn, this creature takes 1 damage. Lasts X turns. Stacks duration. Damage does trigger "OnDamage" passive abilities. Theme: Damage over time, wounds, decay.
Bored (X turns): At the beginning of its controller's turn, if not targeted last turn, skips action/passives this turn. Heals 1 HP End of Turn. Lasts X turns. Theme: Apathy, complacency.
Caffeinated (X turns): Actions +1 Priority. +1 Attack. Takes 1 damage End of Turn. Lasts X turns. Cannot be cleansed. Stacks duration. Cleanses Tired. Theme: Stimulant, hyperactive, burn out.
Compromised: Persists until triggered once. When targeted by enemy "Intelligence", triggers a negative effect for its controller (defined by source). Removed after triggering. Can be applied secretly. Theme: Leaky information, double agent, honeypot.
Concealed: Persists until next action resolves or removed/overridden. Planned action hidden/obscured in opponent's queue preview. Overrides Revealed. Theme: Secrets, hidden intent, misdirection.
Cursed (X turns): End of controller's turn, apply a random negative status from a predefined pool. Lasts X turns. Stacks duration. Theme: Bad luck, persistent misfortune.
Delayed (X turns): Next X turns, actions have base Priority -1. Stacks additively. Duration resets/extends. Theme: Sluggishness, lag.
Deployment Time (X turns): Applied on entry. For X turns, cannot perform actions, passives don't trigger (exceptions possible). Decreases by 1/turn. This duration can be reduced by specific Spells, creature abilities, or Location effects.
Depressed (X turns): Attack stat halved (rounded). Lasts X turns. Stacks duration. 3+ total turns -> remove all Depressed, apply Doomed. Theme: Morale loss, reduced effectiveness.
Doomed (X turns): Destroyed after X turns. Gains +1 Attack start of each turn. Cannot gain Depressed. Cannot be cleansed. Cannot stack duration. Theme: Marked for death, final surge.
Heavy (X turns): Cannot perform Move actions, cannot be moved. Gains 1 Armor End of Turn. Lasts X turns. Stacks duration. Theme: Anchored, immovable.
Ostracized (X turns): Cannot be targeted by actions/abilities. Cannot gain new status effects. Existing effects remain. Can still act. Lasts X turns. Stacks duration. Theme: Isolation, untouchable, phased out.
Revealed (X turns): Planned action visible to opponent in queue preview. Decreases by 1/turn. Stacks duration. Overridden by Concealed. Theme: Information leak, surveillance.
Suppressed (X turns): Cannot perform actions (except Move). Passives don't trigger. Lasts X turns. Stacks duration. Theme: Major disablement, suppression.
Targeted (X intensity): Takes X additional damage from all sources. Stacks intensity. Duration refreshes/extends. Theme: Defenses breached, weak point.
Tired (X stacks): Adds stacks. 1 stack -> Suppressed 1 in 2 turns. 2 stacks -> Suppressed 1 next turn. 3+ stacks -> Suppressed 1 immediately this turn (removes queued actions except Move). Cleansed by Caffeinated. Theme: Gradual exhaustion to shutdown.

C. Trigger Events
This section defines common game events that can trigger passive abilities or other effects. Many triggers provide contextual information (e.g., source of damage, specific status applied) that can be used by the ability's logic.
1. Combat & Creature State Changes:
* OnDamaged(damageSource, damageAmount, damageType): Triggers when this creature (or entity with this trigger) takes damage.
* damageSource: The entity or effect (e.g., creature, spell, status) that caused the damage.
* damageAmount: The numerical value of Health and/or Armor damage taken.
* damageType: Category of damage (e.g., 'Attack', 'Spell', 'AbilityEffect', 'StatusEffect', 'Recoil', 'LocationEffect', 'SlotEffect').
* OnDealDamage(damagedTarget, damageAmount, damageType): Triggers when this creature deals damage.
* damagedTarget: The entity that received the damage.
* damageAmount: The numerical value of Health and/or Armor damage dealt.
* damageType: As above.
* OnHeal(healingSource, healAmount): Triggers when this creature is healed.
* healingSource: The entity or effect that caused the healing.
* healAmount: The numerical value of Health restored.
* OnAttack(attackTarget): Triggers when this creature initiates/declares an Attack action. This typically resolves before damage calculation and before OnAttacked on the target.
* attackTarget: The creature or slot targeted by the attack.
* OnAttacked(attackingCreature): Triggers when this creature is targeted by an Attack action. This typically resolves before damage calculation.
* attackingCreature: The creature performing the attack.
* OnKill(killedCreature): Triggers when this creature's action (e.g., Attack, ability) directly results in another creature being destroyed.
* killedCreature: The creature that was destroyed by this creature.
* OnDied(deathSource): Triggers when this creature is destroyed (Health reduced to 0 or less, or by a "destroy" effect) and sent to the Discard Pile.
* deathSource: The entity, effect, or game rule (e.g., 'CombatDamage', 'SpellEffect', 'StatusDoomed') that caused the death.
* OnArmorGained(amountGained): Triggers when this creature gains Armor.
* amountGained: The quantity of Armor added.
* OnArmorBroken: Triggers when this creature's Armor is reduced from a positive value to 0 by damage.
* OnStatChange(statChanged, oldValue, newValue): Triggers when one of this creature's core stats (Attack, Health, Speed) is modified.
* statChanged: The specific stat (e.g., 'Attack', 'Health', 'Speed').
* oldValue: The value of the stat before the change.
* newValue: The value of the stat after the change.
2. Turn Structure & Action Flow:
* OnTurnStart: Triggers at the beginning of this creature's controller's turn, typically before the Scheduled Draw phase.
* OnTurnEnd: Triggers at the end of this creature's controller's turn, after all queued actions for the turn have resolved.
* OnActionQueued(actionDetails): Triggers when an action involving this creature (either as the source or a target) is added to the action queue.
* actionDetails: Information about the queued action (e.g., type, source, target).
* OnActionResolved(actionDetails): Triggers when an action involving this creature (as source or target) resolves from the queue.
* actionDetails: Information about the resolved action.
* OnDeploymentTimeComplete: Triggers specifically when this creature's Deployment Time counter reaches zero and it becomes fully operational.
3. Movement & Positioning:
* OnMove(originSlot, destinationSlot): Triggers when this creature successfully completes a move action.
* originSlot: The slot this creature moved from.
* destinationSlot: The slot this creature moved to.
* OnEnterSlot(enteredSlot, method): Triggers when this creature enters a slot.
* enteredSlot: The slot the creature has entered.
* method: How the creature entered (e.g., 'Played', 'Moved', 'Swapped', 'Pulled', 'Pushed', 'Revived', 'Recruited', 'Converted', 'ReturnedToOwner').
* OnLeaveSlot(leftSlot, method): Triggers when this creature leaves a slot.
* leftSlot: The slot the creature has left.
* method: How the creature left (e.g., 'Moved', 'Swapped', 'ReturnedToHand', 'Destroyed', 'Recruited', 'Converted', 'ReturnedToOwner').
* OnEnterBattlefield: Triggers when this creature card enters the battlefield from any zone (Hand, Discard Pile via Revive). Occurs after Deployment Time is applied, but before OnDeploymentTimeComplete. (Note: Generally does not re-trigger if a temporarily recruited creature returns to its owner, unless specified by an effect).
* OnLeaveBattlefield(destinationZone): Triggers when this creature is removed from the battlefield.
* destinationZone: The zone the creature card is moving to (e.g., 'Hand', 'DiscardPile', 'Banish', 'Owner'sHand' if returned from temporary recruitment when board is full).
4. Status Effects & Information Warfare:
* OnStatusEffectApplied(statusEffect, effectSource): Triggers when any status effect is applied to this creature.
* statusEffect: The specific status (e.g., Blessed, Depressed, including its duration/intensity if applicable).
* effectSource: The entity or card that applied the status.
* OnStatusEffectRemoved(statusEffect, reason): Triggers when any status effect is removed from this creature.
* statusEffect: The specific status that was removed.
* reason: Why it was removed (e.g., 'Expired', 'Cleansed', 'Consumed').
* OnGainSpecificStatus: A more specific trigger for when a particular status is gained (e.g., OnGainConcealed, OnGainBlessed).
* effectSource: The entity or card that applied the status.
* OnLoseSpecificStatus: A more specific trigger for when a particular status is lost/removed (e.g., OnLoseRevealed, OnLoseCaffeinated).
* reason: Why it was removed.
* OnRevealedByIntelligence(revealingSource): Triggers when this creature (or its pending action) is successfully revealed by an opponent's Intelligence effect.
* revealingSource: The enemy card or effect that caused the reveal.
* OnCompromisedTrigger(triggeringSource): Triggers when this creature's Compromised status is activated by an opponent's Intelligence effect.
* triggeringSource: The enemy Intelligence effect that triggered Compromised.
* OnBecomeTargetable: Triggers when this creature transitions from an untargetable state (e.g., Ostracized ends) to a targetable state.
* OnBecomeUntargetable: Triggers when this creature transitions from a targetable state to an untargetable state (e.g., Ostracized applied).
5. Card & Resource Management (Primarily Player/Controller Level):
* OnCardDrawn(drawnCard): Player-level trigger. Triggers when the player controlling this ability draws one or more cards.
* drawnCard: The card(s) that were drawn.
* OnCardDiscardedFromHand(discardedCard): Player-level trigger. Triggers when the player discards one or more cards from their hand.
* discardedCard: The card(s) that were discarded.
* OnCardPlayed(playedCard): Player-level trigger. Triggers when the player plays a card from their hand.
* playedCard: The Spell or Creature card that was played.
* OnReturnToHand: Triggers when this specific creature card is returned to its owner's hand from the battlefield or Discard Pile.
* OnRevive: Triggers when this specific creature card is returned to the battlefield from the Discard Pile.
* OnDeckReshuffle: Player-level trigger. Triggers when the player's draw deck is reshuffled.
6. Environment & Global State:
* OnLocationChange(oldLocation, newLocation): Global trigger, can affect all creatures or players. Triggers when the active Location (Weather) changes.
* oldLocation: The previous Location effect.
* newLocation: The new Location effect now active.
* OnTrapActivated(trapDetails, activatingEntity): Triggers when a trap (card-based or slot-based) is sprung.
* trapDetails: Information about the trap that was triggered.
* activatingEntity: The creature, action, or effect that triggered the trap.
* OnSlotEffectApplied(slot, slotEffect): Triggers if this creature is on a slot when a new Slot Effect is applied to it.
* slot: The slot this creature occupies.
* slotEffect: The Slot Effect that was applied.
* OnSlotEffectRemoved(slot, slotEffect): Triggers if this creature is on a slot when a Slot Effect is removed from it.
* slot: The slot this creature occupies.
* slotEffect: The Slot Effect that was removed.
* OnOpponentPlaysSpell(playedSpell, spellTarget): Player-level trigger. Triggers when the opponent plays a Spell card.
* playedSpell: The Spell card played by the opponent.
* spellTarget: The target of the opponent's spell, if any.
* OnEnemyCreatureGainConcealed(creature, source): Player-level or specific creature trigger. Triggers when an enemy creature gains Concealed.
* creature: The enemy creature that gained Concealed.
* source: The source of the Concealed status.
* OnBeingTargetedByEnemyIntelligence(targetingSource): Triggers when this creature is specifically targeted by an enemy's Intelligence effect.
* targetingSource: The enemy creature or spell that is the source of the Intelligence effect.
* OnFriendlyTurnStart: (Can be considered a more specific version of OnTurnStart for abilities concerned with friendly context, though OnTurnStart usually implies current controller's turn). Triggers at the start of the turn of the player who controls this creature.

D. Recruitment / Conversion Effects
This section details mechanics for taking control of an opponent's creature.
1.  Definition: Recruitment/Conversion effects allow a player to gain control of an opponent's creature. This is typically achieved through Spells or specific creature abilities. All targeting for these effects is determined when the Spell is played or the creature's active ability is queued.
2.  Targeting: These effects target an enemy creature, chosen at the time the Spell is played or the ability is queued.
3.  Control Change: The targeted creature comes under the control of the player who initiated the effect. The original owner of the card does not change.
    *   If the creature is destroyed while under a new controller, it goes to its original owner's Discard Pile.
    *   If returned to hand, it goes to its original owner's hand.
4.  Positioning & Movement:
    *   The recruited/converted creature moves from its current slot on the opponent's side to an empty friendly slot on the recruiting player's side.
    *   The specific card effect will state if it can only target if an empty slot is available, or if it can target a slot occupied by a friendly creature (which might be destroyed, returned to hand, or swapped as per the card's text). Default assumption: an empty friendly slot is required.
5.  Duration:
    *   Temporary Recruitment: Control lasts for a specified number of turns (e.g., "Gain control of target enemy creature for 2 of your turns").
        *   At the end of the duration (typically at the End of Turn of the current controller), the creature attempts to return to an empty slot on its original owner's side of the battlefield.
        *   If an empty slot is available on the original owner's side, it moves there. It triggers `OnEnterSlot` (method: 'ReturnedToOwner').
        *   If no empty slot is available, it is returned to its original owner's hand. Triggers `OnLeaveBattlefield` (destinationZone: 'Owner'sHand') and relevant `OnReturnToHand` for the creature.
        *   If its original owner's hand is full, the creature is sent to its original owner's Discard Pile.
        *   The creature does not re-trigger 'OnEnterBattlefield' effects when returning to its original owner unless specified by an effect. Deployment Time is not reapplied.
    *   Permanent Conversion: Control is permanent until the creature leaves the battlefield or is subsequently recruited/converted by another player (including the original owner).
6.  State Preservation: Unless otherwise specified by the recruiting/converting card effect:
    *   The creature retains its current Health, Armor, and all existing status effects (positive and negative).
    *   Its base stats (Attack, Health, Speed as printed on the card) remain unchanged.
    *   Its current Deployment Time (if any) continues to count down under the new controller.
7.  Action Queue & Fog of War:
    *   When a creature is Recruited/Converted, any actions queued by its previous controller for the current turn are immediately cancelled.
    *   The new controller may queue actions for it starting from their next turn's planning phase (or as otherwise specified by the recruiting effect).
    *   Its visibility status (Revealed/Concealed) is maintained unless the recruiting effect states otherwise.

V. Game Design Philosophy & Balancing

A. Game Tone
Intentionally cynical, funny, lighthearted. Achieved via consistent satire/exaggeration in card concepts, mechanics, presentation (mocking modern absurdities, bureaucracy, internet culture, myth tropes). Flavor text, names, visuals reinforce this.

B. Balancing Focus
Paramount goal, requires playtesting, data analysis, iteration.
1. Numerical Balance: Use conservative numbers initially for stat mods, damage, healing, Armor, durations. Carefully consider stacking (additive/multiplicative) to prevent broken scaling.
2. Priority Modifier Rarity: Cards granting direct Priority enhancements (e.g., "+1 Priority to an action") are intended to be less common or have higher associated costs/drawbacks compared to cards that alter base Speed stats. This reflects the significant, turn-order-defining impact of Priority.
3. Deployment Time Manipulation: Abilities that reduce or bypass Deployment Time ("agilize deployment") should be rare and carefully balanced due to their significant tempo advantage.
4. Status Effect Design Principle: Prioritize using the existing defined status effects (Section IV.B). Avoid creating new ones unless the mechanic is impossible otherwise. Promotes coherence, reduces complexity, encourages synergy discovery.
5. Creature Ability Design Principle: Prioritize reusing established core mechanics (damage, status, move, heal, armor, stats) and defined trigger types (Section IV.C). Discourage novel underlying mechanics if replicable with existing tools. However, trigger conditions themselves are highly flexible and can be freely modified, combined, and invented (e.g., 'OnFriendlyOfTypeX moves onto affected slot', 'OnTaking Spell Damage while Blessed', 'WhileAdjacentTo exactly one enemy').
6. Faction Synergy & Strategic Versatility Principle:
While individual factions offer powerful internal synergies, relying exclusively on a single faction can lead to inherent strategic limitations or "blind spots"—similar to how a mono-color deck in Magic: The Gathering might excel in its core strategy but lack answers to specific threats (e.g., Elves having potent creature strategies but limited means to deal with flying opponents). Optimal deck performance, therefore, often involves a thoughtful trade-off: incorporating cards or sub-systems from other factions to gain access to mechanics, tools, or answers that complement the primary faction's strengths and cover its weaknesses. This encourages creative deckbuilding aimed at achieving a more robust and versatile strategic toolbox.
7. Underdog Affinity & Comeback Potential: The game aims to foster an environment where comebacks are possible and strategic play can overcome a disadvantage. Some mechanics are subtly tuned to support this, creating an "underdog leaning" feel without explicitly punishing success. For example, the tie-breaking resolution order (lowest current Health + Armor, then lowest base Attack) can occasionally give a slight edge to a creature or player currently in a weaker board state, promoting tighter matches.
8. Embracing and Leveraging Chaos for Strategic Depth:
The game encourages players to navigate and even embrace the inherent chaos of its complex interactions. Winning often involves asking the most difficult strategic questions to the opponent, creating board states or action queues that are challenging for them to resolve optimally, and capitalizing on any resulting missteps or suboptimal plays. This mirrors the strategic depth found in games like chess or competitive Pokémon, where anticipating and outmaneuvering the opponent through complex scenarios is key.
9. Rewarding Emergent and 'Game-Breaking' Synergies:
The design philosophy embraces the potential for players to discover powerful, unconventional strategies that might feel like "breaking the game." These moments are intended to be "aha!" experiences, rewarding deep system knowledge, creative deckbuilding, and a sense of outsmarting established patterns.
Conceptually similar to "reanimator" archetypes in other card games (e.g., discarding high-cost creatures to revive them cheaply) or unexpected, potent combos like "Dead Branch + Corruption" in Slay the Spire.
These strategies, while potentially very effective, should be:
a. Rare: Often requiring specific, multi-card combinations, unique circumstances, or significant setup.
b. Balanced: Not universally dominant, but offering a high-risk/high-reward alternative path. Their existence should not invalidate other core strategies.
c. Quirky & Thematic: Ideally aligning with the game's cynical and humorous tone, making their discovery and execution enjoyable. Some of this quirkiness may stem from players recognizing and combining elements based on their understanding of the meta-references embedded within the game (e.g., phrases from The Simpsons, internet memes, cultural touchstones like Fight Club), leading to unexpectedly synergistic or thematically amusing outcomes when these referenced elements are used together.
d. A Reward for Creativity: Their existence is a nod to player ingenuity and encourages exploration of the game's systems.
10. Recruitment/Conversion Power Level: Effects that allow a player to permanently gain control of an opponent's creature (Conversion) are inherently very powerful and should be rare, high-cost, or come with significant conditions/drawbacks. Temporary recruitment effects can be more common but must be balanced around their duration and potential impact. The ability to use an opponent's own resources against them is a major strategic swing.
11. Balancing Levers:
a. Opportunity Cost: Strategic choices like deck slot allocation, board space management, and manipulating scheduled draws influence tempo and future planning.
b. Additional Resource Costs: Many actions or powerful effects will require the expenditure of specific resources, such as sacrificing creatures, discarding cards from hand, or other defined costs, further influencing decision-making.
c. Space Limitations: Hard 5 creature slots per side constrain deployment. Hand size implicitly managed by draw vs play/discard rate.
d. Domino Effects: Intentionally embrace complex chain reactions. Balance involves managing predictability/impact of cascades; requires player risk/reward assessment.
12. Development Goals & Player Experience:
a. Reward: Strategic Planning, Creative Deckbuilding, Adaptation, Comeback potential.
b. Avoid: Complicated Math, Unfair Situations (extreme luck variance feeling unwinnable). Focus on clear cause/effect. Scheduled draw aids predictable tempo.
13. Prototype Karma System (Future Consideration):
A "Karma System" is being considered as a subtle, background mechanic to mitigate extreme "bad luck" streaks due to inherent game randomness. This system would notionally track statistically improbable negative outcomes for a player.
If such a streak is detected, future low-impact random resolutions (e.g., certain random target selections where multiple valid targets exist, or truly random tie-breaks not covered by other rules) might receive an infinitesimally small, temporary bias in that player's favor.
The goal is to gently nudge probabilities towards a perceived fairness over a longer game, not to directly influence outcomes or become a strategic element.
This effect would be:
a. Extremely minor and often imperceptible.
b. Designed to be non-exploitable.
c. Quickly self-correcting or decaying once a "favorable" random outcome occurs for the player.
d. Aimed at improving player experience by reducing "feel-bad" moments from severe statistical outliers, rather than impacting core strategic decisions or win conditions.
e. Its implementation would require rigorous testing and be carefully balanced to ensure it remains a background "smoothing" effect and doesn't introduce new imbalances.
14. Core Design Tenet - Double-Edged Sword:
Significant positive effects should have a drawback, risk, or cost.
Significant negative effects/costs should offer potential upside, niche, or benefit.
Most cards should have primary effect + secondary drawback/mitigator (even minor).
Examples remain valid: "OnMove: +1 Attack" / "OnAttacked: Controller takes 1 damage"; Doomed (+Attack before death); Reckless Assault (+Damage for recoil).

VI. Illustrative Content Examples

A. Example Status Groupings
Information (Revealed, Concealed, Compromised), Debuffs/Control (Bored, Delayed, Heavy, Suppressed, Cursed, Ostracized, Addicted, Targeted, Doomed, Depressed, Tired, Bleeding/Corroding), Buffs/Utility (Blessed, Caffeinated), Initial State (Deployment Time).

B. Example Passive Ability Concepts
Ninja Step (ignores slot effects), Resolute (survive lethal once), Adrenal Surge (heals low HP once), Overcharge/Reckless Assault (bonus damage + recoil), Kinetic Backlash (recoil based on damage dealt), Essence Tap/Vampiric Strike (life drain).

C. Example Card Concepts
(Refer to the separate document: "Illustrative Card Examples List" for specific card details. This list is maintained as per the Document Usage Guidelines.)