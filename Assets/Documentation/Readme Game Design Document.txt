Game Design Document

I. Core Concept & Vision

Premise: A card game focused on outsmarting opponents via strategic planning, information control, and tactical positioning on a constrained battlefield.

Genre: Roguelite Deckbuilder. Players build and modify their deck during a run. Consequently, card choices and adapting to challenges are crucial.

Central Conflict: The core tension revolves around Intelligence versus Counter-Intelligence. Thematically, this is mirrored by a multifaceted conflict:

Established pyramidal powers (Statecraft, Myth, Corporate) strive to impose and maintain their specific paradigms of order and control. They often achieve this by manipulating information, enforcing dogma, and preserving hierarchical structures.

In contrast, decentralized, emergent forces (Digital Society, Zeitgeist, Mind) challenge these established norms. These forces thrive on fluidity, the free flow of (and sometimes weaponized) information, and the unpredictable power of collective or individual consciousness.

Navigating and exploiting this dynamic are clandestine networks and transformative systems (Shadow Ops., Tech, Applied Sci-Fi). These entities wield sophisticated tools of intelligence, counter-intelligence, disruption, and innovation. They may serve various masters, subvert existing powers, or pursue their own distinct agendas within this complex web of influence.

Primary Goal: Outsmart opponents by:

Gathering info on opponent's hidden plans.

Denying info to the opponent.

Controlling the global 'Location' effect (currently prototyped as 'Weather,' but will encompass various environmental or zone-wide states that fulfill the same role).

Creating/manipulating status effects on creatures and slots.

Strategically moving creatures (friendly or enemy) for advantage.

Key Interaction: Tactical Positioning

Battlefield: Each player controls one row of 5 creature slots, facing each other symmetrically.

Slot Occupation: A maximum of one creature can occupy each slot. Placement is therefore key.

Adjacency: Non-edge slots have two adjacent friendly slots (left and right) and one opposing enemy slot. Edge slots have one adjacent friendly slot and one opposing enemy slot.

Movement & Blocking: Placement dictates movement paths and potential targets (based on adjacency or position). It also triggers passive effects and generally controls the board. Creatures inherently block the slot they occupy; other creatures cannot move into or through an occupied slot unless an ability specifically allows for crossing, swapping, or pushing/pulling. Crucially, creatures cannot move freely. All movement requires specific Spells or creature abilities. If a creature changes sides (for instance, due to Recruitment or Conversion effects as defined in Section IV.D), it moves to an available slot on the new controller's side of the battlefield. A creature cannot be voluntarily moved into an opponent's slot by its current controller. Friendly creatures cannot move into enemy slots unless explicitly recruited or converted by an enemy effect. At that point, they effectively become enemy creatures under the opponent's control.

Key Mechanic: Fog of War

The "Fog of War" in this game revolves around the fact that players do not have a real-time preview of the specific actions their opponents are queuing during their planning phase. Opponents cannot see what Spells are being prepared or which abilities are being activated until those actions resolve and are recorded. Information about an opponent's specific plans for the current turn is primarily gleaned *after the fact* through the Turn History, or by using specialized Intelligence abilities that might offer limited insights under specific conditions.

End of Turn Action Revelation & Turn History: At the End of each Turn, after all queued actions for that turn have resolved, they are recorded in the **Turn History**. This history, envisioned with an "old newspaper" flavor for the UI, serves as a log.
    *   Standard resolved actions become fully visible to both players in the Turn History.
    *   Resolved actions that were `Concealed Actions` (i.e., originated from a creature with the "Undercover" ability or the `Concealed` status effect, and were not negated by `Revealed` status or made visible by `Compromised` status for a specific player) will appear as obscured entries (e.g., '???') in the Turn History for the opponent. The player who performed the `Concealed Action` sees it normally.

Intelligence & Investigating the Past: Players can use "Intelligence" actions. A primary use of these is to target specific '???' entries in the opponent's Turn History from previous turns or earlier in the current turn.
    *   Successfully revealing a '???' entry via an Intelligence action makes that past action's details permanently visible to the investigating player.
    *   This act of revelation is an action itself that is queued. When it resolves, if the original concealed action had any "On Being Revealed by Intelligence" trigger effects (like Counter-Intelligence Traps or Strategic Disclosures, detailed in Section IV.A and IV.C), those effects are immediately formulated as new actions and added to the current Action Queue, resolving in sequence after the Intelligence reveal action.

Counter-Intelligence: These are actions or effects that might further obscure information, or more commonly, represent the risks of prying too deep (e.g., traps triggered by Intelligence reveals). The `Compromised` status (see Section IV.B) is also a key Intelligence/Counter-Intelligence tool. Pre-set Interference effects (Section III.D.4) also form a core part of counter-intelligence strategies.

The `Revealed` Status Effect: This status (defined in Section IV.B) actively counters concealment. Applying it to a creature that is otherwise "Undercover" or `Concealed` will cause its actions to no longer be concealed. This application can itself trigger any "On Being Revealed" effects (defined in Section IV.C).

The `Compromised` Status Effect: This status (defined in Section IV.B) makes an opponent's otherwise concealed creature's actions (past, present, and future) visible to the player who applied `Compromised`, without initially triggering "On Being Revealed" effects. The handler can then choose to use a separate Intelligence action to trigger those effects if desired.

II. Foundational Game Systems & Rules

A. Faction System
All cards are categorized into distinct factions. The primary factions include: Shadow Ops., Statecraft, Myth, Corporate, Digital Society, Zeitgeist, Mind, Tech, and Applied Sci-Fi.
Deckbuilding allows and encourages combinations of factions within a single deck, similar to how color pairs or shards function in Magic: The Gathering. This system promotes diverse deck archetypes, which are built around combined faction strategies and identities.
(Placeholder - Idea for Future Development): Faction-based interactions exist as a soft, granular version of type matchups. These interactions provide subtle advantages or disadvantages rather than hard counters. This can manifest in various ways. For instance, creatures might receive modified outcomes when targeted by actions that originate from specific factions, thereby reflecting the thematic relationships and conflicts between these groups. The design intends for these benefits or drawbacks to be minor. Examples include a 1-point damage reduction when an attack is resisted, a slight increase or decrease in the duration of an applied status effect, or immunity to specific, less impactful status effects (such as a minor debuff). The goal is to avoid creating hard counters or overwhelming advantages through these faction-based interactions.

B. Detailed Faction Themes & Relationships
Shadow Ops.: Embodies: Chaos (in its disruptive potential), Unknown (its hidden nature), Control (its manipulative methods), Covert (its operational mode), Institution (as an organized, albeit clandestine, entity). Operates within a centralized pyramidal structure, viewing its assets with stark utilitarianism. These assets are instruments to be employed and, once their purpose is served or they become a liability, to be expunged with clinical precision, leaving no trace. Its loyalties can be complex, sometimes serving, sometimes undermining the established Order for its own interests.
Statecraft: Embodies: Order (its aim to govern), Structure (through its hierarchies and laws), Known (its public-facing pronouncements), Control (through legislation and enforcement), Institution (as the formal apparatus of governance). It is inherently a centralized pyramidal structure built upon a community-based ethos (however skewed or enforced). This structure is designed for top-down command and the preservation of the Status Quo. It relies on Tradition and maintains wary, transactional relationships with other pyramidal powers.
Myth: Embodies: Tradition (keeper of appropriated beliefs), Dogma (presenting a facade of Harmony), Influence (shaping culture and morality through a community-based narrative). Typically operates as a centralized pyramidal structure, fostering a culture of willing sacrifice. It proffers solace from existential dreads it subtly cultivates or even personifies. In return, it demands devotion as the sole bulwark against the very shadows it defines, thereby binding its followers to its will.
Corporate: Embodies: Order (within its structures, aimed at profit), Structure (hierarchical organization), Control (over resources and markets, wielding significant raw Power), Influence (economically and politically). It is a quintessential centralized pyramidal structure driven by an unyielding, individualistic hunger. Its grand narrative of self-made triumph serves as a meticulously crafted lure. This narrative is a superficial veneer of meritocracy designed to enlist aspirants into its machinery, transforming their ambitions into fuel for its ceaseless expansion. Within its sphere, the currents churn with predatory instinct as entities devour or subsume rivals in a relentless pursuit of dominance.
Digital Society: Embodies: Chaos (its emergent, unpredictable nature, amplified by the overwhelming flood of information whose sheer size and constant shift make it unmanageable and inherently uncontrollable), Flow/Fluidity (rapidly evolving trends), Innovation/Novelty (constantly generating new expression), Autonomy (resisting direct Control), Emergence (as a collective phenomenon). It thrives as a decentralized flat structure. It is a vast, interconnected community of ephemeral thought and expression, sharing a deep, synergistic, and fraternal relationship with Zeitgeist.
Zeitgeist: Embodies: Flow/Fluidity (the shifting spirit of an era), Innovation/Novelty (driving new cultural trends), Unknown (its emergent direction), Influence (shaping public opinion), Chaos (disrupting established norms), Emergence (reflecting a community's collective consciousness and subconscious). It operates as a decentralized flat structure, synergizing strongly with Digital Society.
Mind: Embodies: Critical Discernment (the faculty of rigorous thought, deconstructing illusions and exposing manipulation), Autonomy (individual consciousness resisting Control), Influence/Manipulation (shaping thoughts subtly), Unknown (depths of the subconscious). Represents the power of individual and networked thought, operating as a decentralized flat structure. It is a potent counter to the dogma of pyramidal Myth and the seductive illusions of Corporate. Furthermore, it shares a natural intellectual kinship with Applied Sci-Fi.
Tech: Embodies: Innovation/Novelty (rapid technological advancement), Structure/System (its power derives from vast, intricately interwoven systems, a lattice of dependencies capable of extraordinary output; yet, this very interconnectedness renders it acutely susceptible to cascading failures or targeted disruption), Control (offering tools for information control), Flow/Fluidity (constant evolution), Power (immense impact when leveraged, yet this power can sometimes surge beyond the grasp of its creators, becoming an unpredictable current of its own).
Applied Sci-Fi: Embodies: Innovation/Novelty (its domain is the realized frontier of human ambition, where once-speculative marvels—advanced material sciences, engineered biospheres, the very reshaping of worlds—are wielded as tangible instruments of will), Empirical Scrutiny (the rigorous application of the scientific method), Knowledge (its core strength lies in deciphering the universe's mechanisms and mastering their application, built upon a foundation of audacious theory that has already dared to chart the contours of existence, from the quantum weave to the cosmic tapestry), Order (in its methodologies). It shares a natural kinship with Mind. Both are dedicated to the rigorous deconstruction of the apparent and the relentless pursuit of underlying truths—one through introspective clarity, the other through empirical dominion over the material. Its profound insights are often coveted for exploitation by Statecraft and Corporate.

C. Action Economy & Tempo
No resource cost (mana/energy) is required to play cards from hand. Instead, play is limited by hand size and timing considerations.
Tempo is managed primarily by a mechanic known as Deployment Time (X). This mechanic, which is fully defined in Section IV.B, represents the time it takes for a creature to become fully operational after it enters play. However, it's important to note that this duration is not always fixed; certain game effects, such as Spells, creature abilities, or active Locations, can manipulate it.

D. Action Types & Triggering
1. Creature Stats: Attack, Health, and Speed (which influences action order in conjunction with Priority). Stats can be modified.
2. Creature Abilities (Passive): These abilities are inherently passive. **Crucially, whenever their specified game event conditions are met (as defined in Section IV.C Trigger Events), they ALWAYS trigger automatically. This triggering requires no player decision or intervention at the moment it occurs.** This automatic triggering happens unless the creature is under an effect (such as Suppressed, as defined in Section IV.B) that explicitly prevents its passive abilities from functioning. When a passive ability triggers, its effect is formulated as an action. This action is then added to the Action Queue to be resolved in sequence, as detailed in Section II.G (Universal Action Queuing). Targeting for the effects of these queued actions is predetermined by the card's text (e.g., "target the attacker," "adjacent creatures," "the creature that triggered this") or is resolved randomly if specified by the card. Players do not choose targets for passive abilities when they trigger.
3. Special Creature Archetypes:
Support/Specialists: These may lack a standard Attack. They might possess an Active Ability (e.g., deal damage, heal, apply status) that the player queues during their planning phase each turn. The player selects any required targets at the time of queuing, according to the ability's rules. These abilities are subject to Deployment Time and queue mechanics.
Automated Units: These may lack an Attack. They typically have powerful passive abilities that trigger automatically each turn (e.g., "OnTurnStart: deal 1 damage randomly"; "OnTurnEnd: grant 1 Armor adjacently"). The target for such abilities is predetermined by the ability itself.
4. Creature Attacks: Players actively queue these during their planning phase. **These attacks are added as actions to the Action Queue.** Target selection for attacks might be restricted by rules (e.g., can only target the slot directly in front, cannot target a creature whose actions are Concealed). Targets are chosen at the time the attack is queued.
5. Spells: These are active actions. The player plays them from hand (e.g., by dragging and dropping onto a target, if applicable). When a Spell is played, any costs (including Requisites like discarding cards from hand as an additional cost) are paid, and any targets are chosen by the player at that time. The Spell's effect is then formulated as an action and added to the action queue. This action resolves according to standard queue mechanics (Priority, Speed, etc.). Spells can cause various game state changes, including movement, status effects, damage, healing, or card draw modification.
6. Determinism: Game effects are deterministic. There is no percentage-based randomness for success or failure (except in cases of explicitly random targeting). Outcomes are predictable based on the current game state and the interactions of active effects.

E. Card Management
1. Drawing Cards (Scheduled Draw):
Players draw a base number of cards (e.g., 1) only at the Start of their Turn. This is the sole occasion when cards naturally enter a player's hand from their deck.
The number of cards drawn is calculated as: Base Draw + Sum of all positive/negative draw modifiers accumulated during the previous turn.
No effect allows a player to draw cards immediately from their deck mid-turn.
2. Draw Manipulation (Next Turn Draw Modification): This affects future card draws.
Card advantage effects state: "Schedule X additional card draws for your next turn." This increments a counter that applies to the next turn's draw.
Card disadvantage effects state: "Draw X fewer cards next turn" or "Scheduled draw reduced by X." This decrements the counter (to a minimum of 0 cards drawn).
These modifiers accumulate additively during a turn. They are resolved together for the calculation of the next turn's Start of Turn draw.
3. Discard Pile: This is the zone for used Spells and destroyed Creatures.
4. Discarding from Hand:
    This refers to the act of a player moving cards from their hand to their Discard Pile. All actions related to playing cards, including the payment of discard costs, are performed during the player's planning phase. The resulting card effects are then added to the action queue. The action queue subsequently resolves step-by-step, without further player intervention.
    a.  As an Additional Cost: The only way cards are discarded from a player's hand is as an additional cost to play another card. When a player decides to play a card that has such a cost (e.g., "As an additional cost to play this card, discard 2 cards"), they select the specific card(s) to be discarded from their hand during their planning phase, at that very moment. These selected cards are moved to the Discard Pile immediately as part of fulfilling the cost. The card being played, along with its effect, is then added to the action queue.
    b.  Discard as a Resolving Effect - Prohibition and Alternative: Game effects (originating from Spells, creature abilities, traps, etc.) that resolve from the action queue *cannot* cause a player to discard cards directly from their hand. Consider an effect traditionally designed to force an opponent (or oneself) to discard a card from hand as part of its resolution (e.g., an effect that might read "Target player discards 1 card"). In this game, such an effect must instead be implemented differently: it becomes an effect that reduces the number of cards that player will draw on their next turn. This is further detailed in Section II.E.2 (Draw Manipulation) with phrasing like "Target player draws 1 fewer card next turn." This design ensures that all hand manipulation related to card effects focuses on future resources (next turn's draw) rather than immediate hand reduction during the action resolution phase. This, in turn, maintains the uninterrupted flow of action resolution.
    c.  Distinction from Draw Manipulation: Discarding from hand as an additional cost is an immediate action taken by the player during their planning phase. It affects the current hand size by player choice to enable another action. In contrast, Draw Manipulation, which includes effects that reduce the next turn's draw (now serving as the mechanical replacement for forced discard effects), is a delayed consequence that impacts the future resources available to a player.
5. Banish: This action removes a card (from hand, board, discard pile, or deck) from the game permanently. Banished cards are considered outside all game zones and are irretrievable (i.e., they cannot be affected by Revive, Return to Hand, or similar effects).
6. Card Lifecycle & Reshuffling:
A Spell is played -> its effect resolves -> the Spell card goes to the Discard Pile.
A Creature is destroyed (due to reaching 0 HP or by a "destroy" effect) -> the Creature card goes from the battlefield to the Discard Pile.
Reshuffle Trigger: This activates only when a player is required to draw cards at the Start of their Turn, but their Draw Deck contains fewer cards than the number scheduled to be drawn.
Reshuffle Process:
    The player attempts to draw the scheduled number of cards. Any cards remaining in the Draw Deck are drawn first (this could be 0 cards).
    If the Draw Deck becomes empty and the draw requirement has not yet been met, the player's entire Discard Pile is shuffled. This shuffled pile becomes their new Draw Deck.
    Immediately following the reshuffle, the player continues drawing cards from this new Draw Deck until the total number of cards drawn matches the number scheduled for that Start of Turn phase.
7. Creature Reset: When creature cards are reshuffled from the Discard Pile back into the Deck, any temporary modifications (such as current HP changes, temporary stat alterations, and some status effects) are reset to their printed base values.
8. Requisites: Some powerful cards have conditions that must be met before they can be played or their abilities activated. Examples include: sacrificing a creature, discarding X cards, having X creatures in play, or having a total friendly Attack greater than Y.
9. Opponent Hand Size Checks: Due to the simultaneous nature of player turns and action resolution, any game effects or abilities that need to check the number of cards in an opponent's hand (e.g., for conditional effects, targeting, or information gathering) will perform this check at the End of the current Turn. This check occurs after all other actions for the turn have resolved, ensuring a stable and definitive hand state for such evaluations.

F. Action Queue & Turn Flow Mechanics
**Turn Structure Overview:**
A turn consists of the following general phases:
1.  Start of Turn Phase (Scheduled Draw, OnTurnStart triggers resolve from queue).
2.  Planning Phase (Players queue their Spells, Creature Attacks, and Active Abilities for the turn).
3.  **Pre-Resolution Interference Phase:** Before the main Action Queue resolves, the game checks for any active "Set-up" Interference effects (Nullify, Reorder, Redirect - see Section III.D.4). If an action in the pending queue meets the trigger conditions of such an effect, that action is modified (e.g., Nullified, its Priority changed, its target changed) directly within the pending queue. This phase ensures these specific modifications occur before standard resolution.
4.  Action Resolution Phase (The (potentially modified) Action Queue resolves action by action based on Priority, Speed, and tie-breakers. Passive abilities triggering during resolution add their effects to this queue).
5.  End of Turn Phase (Turn History updated, OnTurnEnd triggers resolve from queue, Hand Size checks for effects occur).

1. Queue Manipulation (General): Beyond the specific Pre-Resolution Interference effects, other actions might interact with queue visibility (e.g., Intelligence revealing details of pending actions) or apply effects that modify how opponents perceive or plan around the queue. Direct destruction or unsolicited removal of actions from the queue by card effects during the Action Resolution Phase is not a feature.
2. Priority Enhancement: Granting actions a higher Priority value causes them to resolve earlier, regardless of creature Speed. This is similar to priority mechanics in Pokémon. Priority enhancement can be an innate quality of a creature's action (e.g., "Priority Attack: +1 Priority"). Pre-Resolution Interference effects can also modify Priority.
3.  Global Priority Modifiers: These are effects that impact the Priority of all actions for a specified duration.
    Example ("Coordinated Advance"): Friendly actions this turn and for the next 3 turns get +1 Priority.
    Example ("Temporal Distortion"): For X turns, the normal action resolution order based on Speed is inverted.
    *   **Priority Resolution during Temporal Distortion:** Actions are first grouped and resolved by Priority level (actions in higher Priority groups resolve before actions in lower Priority groups).
    *   **Speed Resolution during Temporal Distortion:** Within each Priority level, actions from creatures with lower Speed will act before creatures with higher Speed. If Speed is tied for these actions, the resolution order is determined by the **Standard Tie-Breaking Protocol** (defined in Section II.F.4).

4.  **Standard Tie-Breaking Protocol:** This protocol is used when actions are tied in both Priority and creature Speed (during normal resolution) or when actions within the same Priority level have tied Speed during Temporal Distortion. The resolution order is as follows:
    *   The action associated with the creature having the lowest current (Health + Armor) total resolves first.
    *   If the (Health + Armor) total is also tied, the action associated with the creature having the lowest base Attack resolves first.
    *   If Attack is also tied, the resolution order is determined randomly (with potential minor influence from the Karma System, if implemented and active, to mitigate extreme luck streaks).

5.  Tie-Breaking During Temporal Distortion: If actions subject to the inverted Speed order (i.e., they have the same Priority level and Temporal Distortion is active) also have tied Speed values, the resolution order is determined by the **Standard Tie-Breaking Protocol** (defined in Section II.F.4).

6.  Priority Reduction: Actions or effects can assign a negative modifier to an action's base Priority (e.g., Priority -1). This causes the affected action to resolve later in the turn's queue, after actions with higher (or the default 0) Priority. Pre-Resolution Interference effects can also modify Priority. If the reduction is significant, the action might resolve even after actions from slower creatures that possess a higher effective Priority.

G. Universal Action Queuing (New Section)
A fundamental principle of gameplay is that no card effect resolves instantaneously upon the card being played or its ability conditions being met. This principle means that every effect generated by a Spell or a creature ability (whether active or passive) becomes an action for the queue. Similarly, every player-initiated creature attack is treated as such an action. Furthermore, every triggered trap effect, which includes Strategic Disclosure effects as detailed in Section IV.A.1, is also formulated as an action and placed into the Action Queue. These actions are then resolved in sequence during the Action Resolution phase of the turn, according to the rules outlined in Section II.F (Action Queue & Turn Flow Mechanics).
It's important to distinguish these card-generated queued actions from other player-initiated processes. For example, paying costs, such as discarding a card from hand as an additional cost (defined in II.E.4.a), is handled as described in that section. The mechanical process of drawing cards at the start of a turn (Scheduled Draw, per II.E.1) also follows its specific rules. Likewise, moving cards between zones as a direct consequence of a game rule – for instance, a creature being destroyed due to 0 Health and thus sent to the Discard Pile – is managed as detailed elsewhere. These types of processes are not "queued actions" generated by cards in the specific sense meant by this Universal Action Queuing principle.

III. Core Gameplay Loops & Mechanics

A. Information Warfare
This involves constant interaction between revealing an opponent's hidden plans (Intelligence) and concealing your own plans or setting traps (Counter-Intelligence). This also includes using Intelligence actions to investigate obscured '???' entries in the Turn History. Such investigations can potentially reveal past enemy plans or trigger residual effects, such as Counter-Intelligence traps or Strategic Disclosure benefits (both detailed in Section IV.A). Setting up Interference effects (Section III.D.4) is also a key part of this loop.

B. Board Manipulation & Control
1. Logistics (Movement): Using abilities or Spells to reposition creatures (self, allies, or enemies) or to swap their positions. This optimizes attacks, blocks, and adjacency benefits, while also disrupting opponents' formations. Tactics include Post-Action Repositioning ("Hit and Run") and Positional Relays (buffs triggered by moving into a vacated slot).
2. Movement Restriction: Using the Heavy status, specific abilities, or Slot Effects to lock down enemy creatures or protect allied creatures.
3.  Slot Effects / Environmental Hazards: These are persistent or triggered effects on battlefield slots.
    *   **Exclusivity & Replacement:** Only one such effect can be active per slot. If a new Slot Effect is applied to a slot that already has one, the **new Slot Effect replaces the old one**, regardless of compatibility. (For example, if a slot has "Sanctified Ground" and an effect attempts to apply "Desecrated Ground," the "Sanctified Ground" is removed and "Desecrated Ground" becomes active.)
    *   **Nature:** Slot Effects can be visible or hidden traps.
    *   **Triggers:** Their triggers vary (e.g., On Move In/Out, Continuous Turn Start/End, Reactive, One-Time).
    *   **Functionality:** They can also block actions (e.g., a "Movement Jammer" slot effect).
    *   **Damage to Slots & Occupants:** When a slot is the target of damage, or when damage is redirected to a slot, that damage is applied to any creature currently occupying that slot. If the slot is empty at the moment of damage resolution, or if the creature in the slot is destroyed by this damage and there is excess damage, any such damage (initial or excess) is dealt to the player controlling that slot.
4. Hazard Removal / Decontamination: Actions or abilities designed to cleanse negative Slot Effects. This might reset them to a neutral state or replace them with neutral or beneficial effects.
5. Location (Weather): Cards that impose global modifiers on the entire battlefield. These effects can last for a set duration or until changed by another Location card. Locations can restrict actions (e.g., preventing certain card types from being played), provide buffs (e.g., +1 Armor per turn to all creatures), trigger global effects (e.g., heal all creatures), or enable specific strategies.
6. Flooding: Overwhelming the board with many weak or token creatures ("junk"). This tactic aims to obstruct movement, dilute the effectiveness of single-target effects, and enable strategies based on creature quantity.
7. Recruitment / Conversion: Taking control of enemy creatures. This allows a player to use those creatures against their former owner or simply to deny the opponent a resource. Details are provided in Section IV.D.

C. Creature Enhancement & Protection
1. Setup: Actions or abilities that increase the core stats (Attack, Health, Speed) of a player's own creatures or allied creatures.
2. Survivability: Actions like Heal (restoring HP), Add Armor (providing a temporary HP buffer), or abilities that redirect attacks (e.g., from a weak creature to a durable one, or back at the attacker – see also Section III.D.4 for triggered redirection).
3. Immunity/Protection: Granular immunity that negates specific negative effects. Examples include immunity to 'Mental Debuffs', non-friendly movement, specific direct damage types, AoE damage, or Intelligence reveal. This is not blanket invulnerability.
4. Cleansing: Actively removing negative status effects from friendly creatures or from slots. This can be achieved via an active ability, a passive trigger, a timed effect, or a recurring effect.

D. Disruption & Control
1. Status Effects Application: A core mechanic involving the application of defined status effects (see Section IV.B for definitions) to enemy creatures or slots. The goal is to hinder, expose, or control them.
2. Red Tape: Effects that delay an opponent's actions, lower their Priority (causing them to resolve later – see also III.D.4 for triggered reordering/priority changes), or add turns to a creature's Deployment Time. These tactics slow the opponent's tempo.
3. Disable Archetype: Strategies focused on disabling key enemy functions. This can be granular (affecting only passive abilities or only Attacks) or significant (such as the Suppressed status, which blocks most actions but still allows ability-initiated Move actions).
4.  **Interference (Triggered Queue Modification):** Certain abilities or effects allow players to set up "Interference" conditions in advance, akin to booby traps for the Action Queue. These are not played in direct response but are pre-set. During the **Pre-Resolution Interference Phase** (see Section II.F Turn Structure), the game checks all pending actions in the queue against any active, set-up Interference effects. If an action meets the trigger conditions specified by an Interference effect, that action is modified *before* the normal Action Resolution Phase begins. This ensures these modifications happen deterministically and avoid race conditions. All forms of direct modification to an opponent's pending actions in the queue (Nullify, Reorder, Redirect) operate exclusively through this pre-set Interference system.
    *   **UI Notification:** When an Interference effect modifies an action (Nullifies, Reorders, or Redirects it), the game's UI should clearly indicate to both players that the modification occurred and display the name or source of the Interference effect that caused it. This provides context for the changed execution.
    *   Types of Interference include:
        *   **Nullify:** A set-up effect triggers, causing a specific opponent's pending action to be marked as "Nullified." When this action's turn comes during the Action Resolution Phase, it resolves with no game effect.
        *   **Reorder (Priority/Position Modification):** A set-up effect triggers, altering a pending action's Priority (e.g., increasing or decreasing it) or potentially other properties that influence its resolution order relative to other actions. This mechanically changes its place in the upcoming resolution sequence.
        *   **Redirect:** A set-up effect triggers, changing the target of a specific opponent's pending action to a new valid target, as defined by the Interference effect.
    The trigger conditions for these Interference effects are defined on the cards that create them (e.g., "Set-up: The next time an opponent queues a Spell action targeting your 'Slot 1' creature, that Spell action is Nullified.").

5.  Attack/Effect Redirection: All redirection of an opponent's queued actions is handled exclusively by pre-set "Redirect" Interference effects, as detailed in Section III.D.4. There are no game mechanics that allow a player's resolving action to dynamically redirect another opponent's action that is still pending in the queue during the Action Resolution Phase.
6. Recruitment / Conversion / Mind Control: Effects that allow a player to take control of an opponent's creature, either temporarily or permanently. This disrupts the opponent's board state and plans. Full details are in Section IV.D.

E. Recursion & Recovery
1. Revive: An ability or Spell that selects a creature card from a player's Discard Pile and returns it to an empty battlefield slot. The creature enters play with a specified amount of HP (e.g., half its maximum HP, or full HP) and is subject to Deployment Time.
2. Return to Hand (from Battlefield): An ability or Spell that removes a creature (friendly or enemy) from its slot and puts the card into its owner's hand. This allows for redeployment or protection of the creature.
3. Return to Hand (from Discard): An ability or Spell that selects a card (Creature or Spell) from a player's Discard Pile and puts it directly into that player's hand. The card is then available to be played again.

F. Combat, Damage & Targeting
0.  **Active Engagement for Combat Damage:** Creatures only deal combat damage as a result of performing an Attack action that they have actively queued and which targets an enemy creature or slot. There is no inherent "retaliation" damage dealt by a creature merely for being the target of an attack, unless a specific passive ability explicitly grants such a reaction (e.g., "Thorns: OnAttacked, deal 1 damage to the attacker").
1.  Direct Damage: Reducing a target's Health.
2.  Chain/Spread Damage: Damage that hits multiple distinct targets, either sequentially or simultaneously.
3.  Armor: A temporary HP pool on creatures. Damage is applied to Armor first, then to Health. Armor can be removed by specific effects. Recoil damage also hits Armor first.
4.  Life Drain / Siphoning: Dealing damage also heals the source of the damage or its controller. This healing is often based on the amount of damage dealt to Health or Armor. This is typically a passive ability.
5.  Targeting Nuances:
    *   **Targeting Types:** Effects and actions in the game utilize two primary forms of targeting:
        *   **Creature-Targeting:** The effect or action targets a specific creature. If the targeted creature moves to a different slot before the action resolves, the action will still affect that same creature in its new location (akin to a "homing" effect).
            *   **If Targeted Creature Dies:** If a creature targeted by a Creature-Targeting effect is destroyed and moved to the Discard Pile *before* the effect resolves, the effect will **redirect to target the slot** the destroyed creature last occupied.
                1.  The effect resolves on that specific slot.
                2.  Any creature currently in that slot when the effect resolves will be affected by this slot-targeted effect (e.g., take damage if the effect is damaging).
                3.  If the slot is empty when the effect resolves, any damage component of the effect is applied directly to the player who controls that slot. Other non-damage effects might be negated or apply to the slot itself if appropriate for the effect's nature and there's no creature.
        *   **Slot-Targeting:** The effect or action targets a specific battlefield slot. If the creature occupying that slot moves before the action resolves, the action will affect whatever creature is currently in the targeted slot at the moment of resolution. If the slot is empty, the action may have no effect or may affect the slot itself (e.g., applying a Slot Effect).
    *   **Damage to Slots & Occupants (General Rule):** When a slot is the target of damage (e.g., through Slot-Targeting or redirection as per the Creature-Targeting death contingency):
        *   That damage is first applied to any creature currently occupying that slot.
        *   If the slot is empty at the moment of damage resolution, the damage is dealt to the player controlling that slot.
        *   If a creature in the slot is destroyed by this damage, any excess damage is also dealt to the player controlling that slot.
    *   **Targeting Restrictions & Patterns:** Many effects have specific restrictions or patterns for targeting beyond the general type. Examples include: only the slot directly in front; any slot except the front one; geometric patterns like 'V' or '+'; random selection among a valid pool of targets; or condition-based targeting, such as only creatures whose actions are Concealed, Depressed creatures, or creatures with high Attack. The card text will specify these.

IV. Detailed Mechanics & Specific Systems

A. Traps & Strategic Disclosures
These are effects tied to specific actions or creatures that trigger under particular conditions, often related to information warfare. When a trap's or strategic disclosure's trigger condition is met (as defined by the `OnRevealedByIntelligence` trigger in Section IV.C), its effect is formulated as an action. This action is then added to the *current turn's* Action Queue for resolution, following standard Priority and timing rules.

**Triggering Conditions for "On Being Revealed" Effects (Traps/Strategic Disclosures):**
The primary ways these effects trigger are defined by the `OnRevealedByIntelligence` trigger (see Section IV.C). In summary:
1.  **Intelligence Action on Turn History:** An opponent uses an Intelligence action that successfully reveals a '???' entry in the Turn History, where the original concealed action had such a trigger. The trap/disclosure effect is queued immediately after the Intelligence reveal action resolves.
2.  **Application of `Revealed` Status:** An opponent applies the `Revealed` status effect to a creature that is "Undercover" or has the `Concealed` status effect. If that creature or its typical actions have an "On Being Revealed" trigger, it activates at this point. The trap/disclosure effect is queued.

Type 1: Counter-Intelligence Trap ("On Being Revealed"): A passive ability or effect on a card that triggers a detrimental effect for the **opponent who caused the reveal** (either through an Intelligence action on a '???' entry or by applying the `Revealed` status).
    *   Example: "Trap: OnRevealedByIntelligence (by opponent): Add an action to the queue: that opponent draws 1 fewer card next turn." (This uses the trigger from Section IV.C)
    *   These effects trigger automatically when their specific reveal conditions are met; the resulting detrimental effect is always queued.

Type 2: Counter-Intelligence Trap (Rider on a Queued Action): This involves a hidden rider attached to a *currently queued* action. If that specific queued action is modified by an opponent's Interference effect (e.g., Nullified, its target changed by Redirect, or its Priority significantly altered by Reorder, as described in Section III.D.4) during the Pre-Resolution Interference Phase, the rider triggers. The rider's effect (typically a negative effect on the opponent who owns the Interference effect) is then formulated as an action and added to the Action Queue.
    *   Example: "Spell X [Primary Effect]. Rider: If Spell X is Nullified by an opponent's effect, add an action to the queue: the opponent who caused the Nullification takes 2 damage."

Slot-based traps, which are deployed via hidden Slot Effects, also follow this principle: when triggered, their effect is formulated as an action and added to the Action Queue.

A.1. Strategic Disclosure Effects ("On Being Revealed" - Beneficial for Investigator/Revealer)
Distinct from detrimental traps, 'Strategic Disclosure' effects are beneficial outcomes. These benefits trigger for the **player who successfully causes the reveal of an opponent's concealed information** (either by using an Intelligence action on a '???' entry in the Turn History or by applying the `Revealed` status effect to an opponent's "Undercover" / `Concealed` creature, as per the `OnRevealedByIntelligence` trigger in Section IV.C).

*   Flavor: This represents the **investigating player gaining a tangible advantage. This advantage comes from exposing an opponent's undercover operation or sensitive information.** The "scandal" or revelation consequently creates a backlash or an opportunity that the revealing player can exploit. Your example: "the revelation of non-such weapons of mass destruction in Iraq, it ended up ending the war and creating backlash for the ones involved" – the "backlash" here translates to a game benefit for the player who forced the revelation.
*   Mechanism: The mechanism operates as follows: A player's action (an Intelligence action on a '???' or applying the `Revealed` status) successfully reveals an opponent's concealed information. This revealed information must correspond to an action or creature possessing a 'Strategic Disclosure' characteristic (this characteristic is an inherent part of the card or action that was originally concealed). When these conditions are met via the `OnRevealedByIntelligence` trigger, the specified beneficial effect triggers for the **player who performed the revealing action.** This beneficial outcome is then formulated as an action. Examples of such outcomes include: "The player revealing this information draws 1 additional card next turn," or "The player revealing this information applies +1 Attack to one of their creatures." Finally, this formulated action is added to the *current turn's* Action Queue for that investigating/revealing player.
*   Purpose: These effects serve as an additional incentive for players to actively invest in Intelligence capabilities or tools that negate concealment. They make the act of uncovering secrets potentially more rewarding than just gaining information. This adds another layer to the risk/reward dynamics involved when playing with and against concealed strategies. For the player employing concealment, this introduces an added risk. Specifically, not only can their hidden operations be exposed, but their opponent might also directly benefit from the discovery itself.
*   Triggering: All Strategic Disclosure effects trigger automatically when their conditions (based on `OnRevealedByIntelligence`) are met; the resulting beneficial effect for the investigating/revealing player is always queued.

B. Status Effects (Detailed Definitions)
Addicted (X turns): At the beginning of its controller's turn, if this creature did not have its stats directly altered (increase or decrease) by any effect during the previous turn, it gains Suppressed 1. This check occurs each turn for the duration X. Theme: Dependency on continuous stat alterations; withdrawal occurs if stat alterations cease.
Blessed: Persists until consumed. Negates the next negative status effect that would be applied, then Blessed is removed. Does not stack. Theme: Single-use ward, divine protection.
Bleeding / Corroding (X turns): At the End of its controller's Turn, this creature takes 1 damage. Lasts X turns. Stacks duration. Damage does trigger "OnDamage" passive abilities. Theme: Damage over time, wounds, decay.
Bored (X turns): At the beginning of its controller's turn, if not targeted last turn, skips action/passives this turn. Heals 1 HP End of Turn. Lasts X turns. Theme: Apathy, complacency.
Caffeinated (X turns): Actions +1 Priority. +1 Attack. Takes 1 damage End of Turn. Lasts X turns. Cannot be cleansed. Stacks duration. Cleanses Tired. Theme: Stimulant, hyperactive, burn out.

**Concealed (X turns):** Status Effect. For X turns, all actions queued by this creature are `Concealed Actions`.
    *   **`Concealed Actions` and Fog of War:** `Concealed Actions` are not visible to the opponent in any pre-resolution preview.
    *   **`Concealed Actions` in Turn History:** When a `Concealed Action` resolves, its entry appears as '???' in the opponent's Turn History. The controller sees it normally.
    *   This status can be negated by the `Revealed` status effect. If a creature has both, `Revealed` takes precedence.
    *   Creatures with a native "Undercover" passive ability inherently produce `Concealed Actions` without needing this status, but are affected by `Revealed` status similarly.
    *   Theme: Secrets, hidden operations, misdirection.

**Compromised (Persists until cleansed or creature leaves play):** Status Effect. Flavor: An agent captured and converted into a double agent.
    *   **Targeting:** Typically applied to an opponent's creature that is currently "Undercover" or has the `Concealed` status effect.
    *   **Effect on Visibility (for the Handler):** For the player who applied `Compromised` (the "handler"):
        *   All past '???' entries in the Turn History from this creature become fully visible to the handler.
        *   All future actions queued by this creature, even if they would normally be `Concealed Actions` (due to "Undercover" ability or ongoing `Concealed` status), are fully visible to the handler in their Turn History after resolution (and potentially in any limited previews if such a mechanic exists for specific Intelligence tools targeting pending actions).
    *   **Effect on Visibility (for others):** The original controller of the `Compromised` creature, and any other opponents, still see its `Concealed Actions` as '???' in the Turn History, as per normal concealment rules.
    *   **"On Being Revealed" Triggers:** Critically, the passive information leakage to the handler due to the `Compromised` status does *not* automatically trigger any `OnRevealedByIntelligence` effects associated with the creature or its actions.
    *   **Optional Intelligence Action:** The handler can *still choose* to use a separate Intelligence action targeting a (now visible to them) past concealed action of the `Compromised` creature in the Turn History. If they do so, any associated `OnRevealedByIntelligence` Traps or Strategic Disclosures *will* trigger as per their normal conditions, with effects queued for the handler.
    *   Theme: Double agent, leaky information, controlled opposition.

Cursed (X turns): End of controller's turn, apply a random negative status from a predefined pool. Lasts X turns. Stacks duration. Theme: Bad luck, persistent misfortune.
Delayed (X turns): Next X turns, actions have base Priority -1. Stacks additively. Duration resets/extends. Theme: Sluggishness, lag.
Deployment Time (X turns): Applied on entry. For X turns, cannot perform actions, passives don't trigger (exceptions possible). Decreases by 1/turn. This duration can be reduced by specific Spells, creature abilities, or Location effects.
Depressed (X turns): Attack stat halved (rounded). Lasts X turns. Stacks duration. 3+ total turns -> remove all Depressed, apply Doomed. Theme: Morale loss, reduced effectiveness.
Doomed (X turns): Destroyed after X turns. Gains +1 Attack start of each turn. Cannot gain Depressed. Cannot be cleansed. Cannot stack duration. Theme: Marked for death, final surge.
Heavy (X turns): Cannot perform Move actions, cannot be moved. Gains 1 Armor End of Turn. Lasts X turns. Stacks duration. Theme: Anchored, immovable.
Ostracized (X turns): Cannot be targeted by actions/abilities. Cannot gain new status effects. Existing effects remain. Can still act. Lasts X turns. Stacks duration. Theme: Isolation, untouchable, phased out.

**Revealed (X turns):** Status Effect. For X turns, this status actively counters concealment for the affected creature.
    *   **Effect:** If a creature with an "Undercover" ability or the `Concealed` status effect gains the `Revealed` status, its actions are *not* `Concealed Actions` for the duration of `Revealed`. They are treated as normal actions regarding visibility in the Turn History.
    *   **Triggering "On Being Revealed" Effects:** If applying the `Revealed` status causes a creature to stop producing `Concealed Actions` (i.e., it was "Undercover" or `Concealed`), and that creature or its actions have any `OnRevealedByIntelligence` triggers (Traps or Strategic Disclosures as defined in Section IV.C), those triggers activate *at the moment the `Revealed` status is applied*. The resulting trap/disclosure effects are queued, affecting the player who applied the `Revealed` status (detrimentally for traps, beneficially for disclosures).
    *   `Revealed` status overrides the `Concealed` status and the "Undercover" ability's inherent concealment.
    *   Theme: Exposed, under surveillance, public knowledge.

Suppressed (X turns): Cannot perform actions (except Move). Passives don't trigger. Lasts X turns. Stacks duration. Theme: Major disablement, suppression.
Targeted (X intensity): Takes X additional damage from all sources. Stacks intensity. Duration refreshes/extends. Theme: Defenses breached, weak point.
Tired (X stacks): Adds stacks. 1 stack -> Suppressed 1 in 2 turns. 2 stacks -> Suppressed 1 next turn. 3+ stacks -> Suppressed 1 immediately this turn (removes queued actions except Move). Cleansed by Caffeinated. Theme: Gradual exhaustion to shutdown.

C. Trigger Events
DOCUMENT USAGE GUIDELINES:

Comprehensive List: This document contains the definitive list of standardized game events that can serve as triggers for abilities and game effects.

Stability of Definitions: The names and core definitions (including primary parameters) of these trigger events are stable and should not be altered, removed, or significantly rephrased without explicit instruction. This ensures consistent interpretation across all game mechanics.

Adding New Triggers: New trigger events should only be added if a fundamentally new type of game interaction occurs that cannot be adequately represented by existing triggers, even with flexible ability conditions.

Parameter Expansion: Parameters for existing triggers may be expanded if new mechanics require additional contextual information, but the original parameters must be maintained.

Distinction from Ability Conditions: This list defines what event occurs (e.g., OnDamaged). The specific conditions under which a card's ability reacts to this event (e.g., 'OnDamaged by a Spell while Concealed') are defined on individual cards and are highly flexible, as per the main Game Design Document (Section V.B.5 - Creature Ability Design Principle).

Trigger Events List

This section defines common game events that can trigger passive abilities or other effects. Many triggers provide contextual information (e.g., source of damage, specific status applied) that can be used by the ability's logic.

Combat & Creature State Changes:

OnDamaged(damageSource, damageAmount, damageType): Triggers when this creature (or entity with this trigger) takes damage.
    damageSource: The entity or effect (e.g., creature, spell, status) that caused the damage.
    damageAmount: The numerical value of Health and/or Armor damage taken.
    damageType: Category of damage (e.g., 'Attack', 'Spell', 'AbilityEffect', 'StatusEffect', 'Recoil', 'LocationEffect', 'SlotEffect').

OnDealDamage(damagedTarget, damageAmount, damageType): Triggers when this creature deals damage.
    damagedTarget: The entity that received the damage.
    damageAmount: The numerical value of Health and/or Armor damage dealt.
    damageType: As above.

OnHeal(healingSource, healAmount): Triggers when this creature is healed.
    healingSource: The entity or effect that caused the healing.
    healAmount: The numerical value of Health restored.

OnAttack(attackTarget): Triggers when this creature initiates/declares an Attack action. This typically resolves before damage calculation and before OnAttacked on the target.
    attackTarget: The creature or slot targeted by the attack.

OnAttacked(attackingCreature): Triggers when this creature is targeted by an Attack action. This typically resolves before damage calculation.
    attackingCreature: The creature performing the attack.

OnKill(killedCreature): Triggers when this creature's action (e.g., Attack, ability) directly results in another creature being destroyed.
    killedCreature: The creature that was destroyed by this creature.

OnAboutToDie(cause, source, details): Triggers when this creature is marked for imminent destruction (e.g., after lethal damage has been calculated but before Health is officially set to 0/negative, or when a "destroy" effect targeting it is about to resolve and move it to the Discard Pile). This occurs before OnDied.
    cause: The reason for impending death (e.g., 'LethalDamage', 'DestroyEffect', 'StatusEffectDoomed', 'GameRule').
    source: The entity or effect (e.g., creature, spell, status) that will cause the death.
    details: Optional. Contextual information. If cause is 'LethalDamage', details could include { damageAmount, damageType } representing the lethal portion of damage. If cause is 'DestroyEffect', details could include the specific spell or ability.

OnDied(deathSource): Triggers when this creature is confirmed to be destroyed (Health is 0 or less after all modifications and potential preventions from OnAboutToDie effects, or by an unprevented "destroy" effect) and is moved to the Discard Pile. Abilities with this trigger on the creature resolve while the card is in the Discard Pile.
    deathSource: The entity, effect, or game rule (e.g., 'CombatDamage', 'SpellEffect', 'StatusDoomed', 'AbilityEffect') that ultimately caused the death.

OnArmorGained(amountGained): Triggers when this creature gains Armor.
    amountGained: The quantity of Armor added.

OnArmorBroken: Triggers when this creature's Armor is reduced from a positive value to 0 by damage.

OnStatChange(statChanged, oldValue, newValue): Triggers when one of this creature's core stats (Attack, Health, Speed) is modified.
    statChanged: The specific stat (e.g., 'Attack', 'Health', 'Speed').
    oldValue: The value of the stat before the change.
    newValue: The value of the stat after the change.

Turn Structure & Action Flow:

OnTurnStart: Triggers at the beginning of this creature's controller's turn, typically before the Scheduled Draw phase.

OnTurnEnd: Triggers at the end of this creature's controller's turn, after all queued actions for the turn have resolved.

OnActionQueued(actionDetails): Triggers when an action involving this creature (either as the source or a target) is added to the action queue.
    actionDetails: Information about the queued action (e.g., type, source, target).

OnActionResolved(actionDetails): Triggers when an action involving this creature (as source or target) resolves from the queue.
    actionDetails: Information about the resolved action.

OnDeploymentTimeComplete: Triggers specifically when this creature's Deployment Time counter reaches zero and it becomes fully operational.

Movement & Positioning:

OnMove(originSlot, destinationSlot): Triggers when this creature successfully completes a move action.
    originSlot: The slot this creature moved from.
    destinationSlot: The slot this creature moved to.

OnEnterSlot(enteredSlot, method): Triggers when this creature enters a slot.
    enteredSlot: The slot the creature has entered.
    method: How the creature entered (e.g., 'Played', 'Moved', 'Swapped', 'Pulled', 'Pushed', 'Revived', 'Recruited', 'Converted', 'ReturnedToOwner').

OnLeaveSlot(leftSlot, method): Triggers when this creature leaves a slot.
    leftSlot: The slot the creature has left.
    method: How the creature left (e.g., 'Moved', 'Swapped', 'ReturnedToHand', 'Destroyed', 'Recruited', 'Converted', 'ReturnedToOwner').

OnEnterBattlefield: Triggers when this creature card enters the battlefield from any zone (Hand, Discard Pile via Revive). Occurs after Deployment Time is applied, but before OnDeploymentTimeComplete. (Note: Generally does not re-trigger if a temporarily recruited creature returns to its owner, unless specified by an effect).

OnLeaveBattlefield(destinationZone): Triggers when this creature is removed from the battlefield.
    destinationZone: The zone the creature card is moving to (e.g., 'Hand', 'DiscardPile', 'Banish', 'Owner'sHand' if returned from temporary recruitment when board is full).

Status Effects & Information Warfare:

OnStatusEffectApplied(statusEffect, effectSource): Triggers when any status effect is applied to this creature.
    statusEffect: The specific status (e.g., Blessed, Depressed, including its duration/intensity if applicable).
    effectSource: The entity or card that applied the status.

OnStatusEffectRemoved(statusEffect, reason): Triggers when any status effect is removed from this creature.
    statusEffect: The specific status that was removed.
    reason: Why it was removed (e.g., 'Expired', 'Cleansed', 'Consumed').

OnGainSpecificStatus: A more specific trigger for when a particular status is gained (e.g., OnGainConcealed, OnGainBlessed).
    effectSource: The entity or card that applied the status.

OnLoseSpecificStatus: A more specific trigger for when a particular status is lost/removed (e.g., OnLoseRevealed, OnLoseCaffeinated).
    reason: Why it was removed.

OnRevealedByIntelligence(revealingSource, revealedActionDetails): Triggers when an opponent's action causes the reveal of this creature's concealed nature or its past Concealed Action. This can occur when:
    1) An opponent's Intelligence action successfully reveals a '???' entry (representing this creature's past Concealed Action) from the Turn History.
    2) An opponent applies the 'Revealed' status effect to this creature, if this creature was "Undercover" or possessed the 'Concealed' status effect.
    revealingSource: The enemy card, effect, or Intelligence action that caused the reveal.
    revealedActionDetails: Optional. If the reveal was of a past '???' action from the Turn History, this provides details about that specific past action.

OnBecomeTargetable: Triggers when this creature transitions from an untargetable state (e.g., Ostracized ends) to a targetable state.

OnBecomeUntargetable: Triggers when this creature transitions from a targetable state to an untargetable state (e.g., Ostracized applied).

Card & Resource Management (Primarily Player/Controller Level):

OnCardDrawn(drawnCard): Player-level trigger. Triggers when the player controlling this ability draws one or more cards.
    drawnCard: The card(s) that were drawn.

OnCardDiscardedFromHand(discardedCard): Player-level trigger. Triggers when the player discards one or more cards from their hand (as an additional cost).
    discardedCard: The card(s) that were discarded.

OnCardPlayed(playedCard): Player-level trigger. Triggers when the player plays a card from their hand.
    playedCard: The Spell or Creature card that was played.

OnReturnToHand: Triggers when this specific creature card is returned to its owner's hand from the battlefield or Discard Pile.

OnRevive: Triggers when this specific creature card is returned to the battlefield from the Discard Pile.

OnDeckReshuffle: Player-level trigger. Triggers when the player's draw deck is reshuffled.

Environment & Global State:

OnLocationChange(oldLocation, newLocation): Global trigger, can affect all creatures or players. Triggers when the active Location (Weather) changes.
    oldLocation: The previous Location effect.
    newLocation: The new Location effect now active.

OnTrapActivated(trapDetails, activatingEntity): Triggers when a trap (card-based or slot-based) is sprung. (This is a generic trap trigger; specific Interference effects in III.D.4 and Counter-Intelligence Traps in IV.A are more specialized interactions with the queue or reveals).
    trapDetails: Information about the trap that was triggered.
    activatingEntity: The creature, action, or effect that triggered the trap.

OnSlotEffectApplied(slot, slotEffect): Triggers if this creature is on a slot when a new Slot Effect is applied to it.
    slot: The slot this creature occupies.
    slotEffect: The Slot Effect that was applied.

OnSlotEffectRemoved(slot, slotEffect): Triggers if this creature is on a slot when a Slot Effect is removed from it.
    slot: The slot this creature occupies.
    slotEffect: The Slot Effect that was removed.

OnOpponentPlaysSpell(playedSpell, spellTarget): Player-level trigger. Triggers when the opponent plays a Spell card.
    playedSpell: The Spell card played by the opponent.
    spellTarget: The target of the opponent's spell, if any.

OnEnemyCreatureGainConcealed(creature, source): Player-level or specific creature trigger. Triggers when an enemy creature gains the Concealed status effect or has an inherent "Undercover" ability making its actions concealed.
    creature: The enemy creature that became concealed.
    source: The source of the Concealed status or the "Undercover" ability.

OnBeingTargetedByEnemyIntelligence(targetingSource): Triggers when this creature is specifically targeted by an enemy's Intelligence effect (e.g., an effect attempting to reveal a pending action or a '???' in the Turn History associated with this creature).
    targetingSource: The enemy creature or spell that is the source of the Intelligence effect.

OnFriendlyTurnStart: (Can be considered a more specific version of OnTurnStart for abilities concerned with friendly context, though OnTurnStart usually implies current controller's turn). Triggers at the start of the turn of the player who controls this creature.

D. Recruitment / Conversion Effects
This section details mechanics for taking control of an opponent's creature.
1.  Definition: Recruitment/Conversion effects allow a player to gain control of an opponent's creature. This is typically achieved through Spells or specific creature abilities. All targeting for these effects is determined when the Spell is played or the creature's active ability is queued.
2.  Targeting: These effects target an enemy creature. The target is chosen at the time the Spell is played or the ability is queued.
3.  Control Change: The targeted creature comes under the control of the player who initiated the effect. The original owner of the card does not change.
    *   If the creature is destroyed while under a new controller, it goes to its original owner's Discard Pile.
    *   If returned to hand, it goes to its original owner's hand.
4.  Positioning & Movement:
    *   The recruited/converted creature moves from its current slot on the opponent's side to an empty friendly slot on the recruiting player's side.
    *   The specific card effect will state if it can only target if an empty slot is available, or if it can target a slot occupied by a friendly creature. In the latter case, the friendly creature might be destroyed, returned to hand, or swapped, as per the card's text. The default assumption is that an empty friendly slot is required.
5.  Duration:
    *   Temporary Recruitment: Control lasts for a specified number of turns (e.g., "Gain control of target enemy creature for 2 of your turns").
        *   At the end of the specified duration (typically at the End of Turn of the current controller), the creature attempts to return to an empty slot on its original owner's side of the battlefield.
        *   If an empty slot is available on the original owner's side, the creature moves there. This triggers `OnEnterSlot` (method: 'ReturnedToOwner').
        *   If no empty slot is available on the original owner's side, the creature is returned to its original owner's hand. This triggers `OnLeaveBattlefield` (destinationZone: 'Owner'sHand') and relevant `OnReturnToHand` triggers for the creature.
        *   If its original owner's hand is full at this point, the creature is sent to its original owner's Discard Pile.
        *   The creature does not re-trigger 'OnEnterBattlefield' effects when returning to its original owner unless an effect specifically states otherwise. Deployment Time is not reapplied.
    *   Permanent Conversion: Control is permanent until the creature leaves the battlefield or is subsequently recruited/converted by another player (which can include the original owner).
6.  State Preservation: Unless otherwise specified by the recruiting/converting card effect:
    *   The creature retains its current Health, Armor, and all existing status effects (both positive and negative).
    *   Its base stats (Attack, Health, Speed as printed on the card) remain unchanged.
    *   Its current Deployment Time (if any) continues to count down under the new controller.
7.  Action Queue & Fog of War:
    *   **Cancellation of Previous Controller's Queued Actions:** When a creature is Recruited/Converted, any actions queued by its *previous controller* for the current turn are **immediately cancelled** and removed from the Action Queue. The new controller does not inherit these actions, and they do not resolve.
    *   **New Controller's Actions:** The new controller may queue new actions for the recruited/converted creature starting from their *next* planning phase, or as otherwise specified by the recruiting effect (e.g., an effect might state "Recruit target creature. It may act this turn."). This provides the new controller an opportunity to integrate the creature into their strategy or react to its state.
    *   **"Time Bomb" Archetype:** The "time bomb" strategy, where a player gives a creature detrimental attributes and then passes it to an opponent, is achieved through persistent means such as:
        *   Negative status effects (e.g., Bleeding, Doomed, Cursed) that remain on the creature.
        *   Inherent passive abilities on the creature that are detrimental to its controller (e.g., "OnTurnEnd: Your controller takes 1 damage").
        *   Effects that trigger upon the creature entering a new slot (e.g., "OnEnterSlot: Apply Suppressed 1 to adjacent friendly creatures").
        The cancellation of previously queued actions means the "time bomb" relies on the creature's inherent state and abilities, not on lingering commands from its old master.
    *   **Concealment Status After Control Change:** If the recruited/converted creature has the `Concealed` status effect or an inherent "Undercover" ability:
        *   These attributes are maintained on the creature.
        *   However, the concealment now benefits the *new controller*. Actions queued for this creature by its new controller will appear as `Concealed Actions` (i.e., "hidden/obscured" or '???') to the new controller's opponents (including the creature's original owner). The new controller sees their own queued actions for this creature normally.
        *   Essentially, the creature's clandestine nature now serves its current master. Any `Revealed` or `Compromised` status effects also continue to apply relative to the creature's new controller and their opponents.
    *   **Important Note on Action Persistence:** All actions queued during a turn must resolve within that turn. Actions are never carried over from one turn to the next in the Action Queue.

V. Game Design Philosophy & Balancing

A. Game Tone
The game tone is intentionally cynical, funny, and lighthearted. This is achieved through consistent satire and exaggeration in card concepts, mechanics, and presentation. This includes mocking modern absurdities, bureaucracy, internet culture, and myth tropes. Flavor text, card names, and visuals all reinforce this tone.

B. Balancing Focus
Balance is a paramount goal. It requires ongoing playtesting, data analysis, and iteration.
1. Numerical Balance: Initially, use conservative numbers for stat modifications, damage, healing, Armor values, and effect durations. Carefully consider how stacking effects (both additive and multiplicative) work to prevent broken scaling.
2. Priority Modifier Rarity: Cards that grant direct Priority enhancements (e.g., "+1 Priority to an action") are intended to be less common or to have higher associated costs/drawbacks compared to cards that alter base Speed stats. This rarity reflects the significant, turn-order-defining impact of Priority.
3. Deployment Time Manipulation: Abilities that reduce or bypass Deployment Time ("agilize deployment") should be rare and carefully balanced. This is due to the significant tempo advantage such abilities provide.
4. Status Effect Design Principle: Prioritize using the existing, defined status effects (listed in Section IV.B). Avoid creating new status effects unless the desired mechanic is impossible to achieve otherwise. This promotes coherence, reduces complexity, and encourages players to discover synergies.
5. Creature Ability Design Principle: Prioritize reusing established core mechanics (such as damage, status application, movement, healing, armor, and stat changes) and defined trigger types (listed in Section IV.C). Discourage the creation of novel underlying mechanics if the desired effect can be replicated with existing tools. However, the trigger conditions themselves (including those for set-up Interference effects) are highly flexible. They can be freely modified, combined, and invented (e.g., 'OnFriendlyOfTypeX moves onto affected slot', 'OnTaking Spell Damage while Blessed', 'WhileAdjacentTo exactly one enemy', 'If opponent queues an Attack action with Attack > 5...').
6. Faction Synergy & Strategic Versatility Principle:
While individual factions offer powerful internal synergies, relying exclusively on a single faction can lead to inherent strategic limitations or "blind spots." This is similar to how a mono-color deck in Magic: The Gathering might excel in its core strategy but lack answers to specific threats (e.g., Elves having potent creature strategies but limited means to deal with flying opponents). Optimal deck performance, therefore, often involves a thoughtful trade-off. This trade-off means incorporating cards or sub-systems from other factions to gain access to mechanics, tools, or answers that complement the primary faction's strengths and cover its weaknesses. This approach encourages creative deckbuilding aimed at achieving a more robust and versatile strategic toolbox.
7. Underdog Affinity & Comeback Potential: The game aims to foster an environment where comebacks are possible and strategic play can overcome a disadvantage. Some mechanics are subtly tuned to support this, creating an "underdog leaning" feel without explicitly punishing success. For example, the tie-breaking resolution order (lowest current Health + Armor, then lowest base Attack) can occasionally give a slight edge to a creature or player currently in a weaker board state, thereby promoting tighter matches.
8. Embracing and Leveraging Chaos for Strategic Depth:
The game encourages players to navigate and even embrace the inherent chaos of its complex interactions. Winning often involves asking the most difficult strategic questions to the opponent. This means creating board states or action queues that are challenging for them to resolve optimally, and then capitalizing on any resulting missteps or suboptimal plays. This mirrors the strategic depth found in games like chess or competitive Pokémon, where anticipating and outmaneuvering the opponent through complex scenarios is key.
9. Rewarding Emergent and 'Game-Breaking' Synergies:
The design philosophy embraces the potential for players to discover powerful, unconventional strategies that might feel like "breaking the game." These moments are intended to be "aha!" experiences. They reward deep system knowledge, creative deckbuilding, and a sense of outsmarting established patterns.
Conceptually, these are similar to "reanimator" archetypes in other card games (e.g., discarding high-cost creatures to revive them cheaply) or unexpected, potent combos like "Dead Branch + Corruption" in Slay the Spire.
These strategies, while potentially very effective, should adhere to the following:
a. Rare: They should often require specific, multi-card combinations, unique circumstances, or significant setup.
b. Balanced: They should not be universally dominant, but rather offer a high-risk/high-reward alternative path. Their existence should not invalidate other core strategies.
c. Quirky & Thematic: Ideally, they should align with the game's cynical and humorous tone, making their discovery and execution enjoyable. Some of this quirkiness may stem from players recognizing and combining elements based on their understanding of the meta-references embedded within the game (e.g., phrases from The Simpsons, internet memes, cultural touchstones like Fight Club). This can lead to unexpectedly synergistic or thematically amusing outcomes when these referenced elements are used together.
d. A Reward for Creativity: Their existence is a nod to player ingenuity and encourages exploration of the game's systems.
10. Recruitment/Conversion Power Level: Effects that allow a player to permanently gain control of an opponent's creature (Conversion) are inherently very powerful. Such effects should be rare, high-cost, or come with significant conditions or drawbacks. Temporary recruitment effects can be more common but must be balanced around their duration and potential impact. The ability to use an opponent's own resources against them represents a major strategic swing.
11. Balancing Levers:
a. Opportunity Cost: Strategic choices, such as deck slot allocation, board space management, and manipulating scheduled draws, influence tempo and future planning.
b. Additional Resource Costs: Many actions or powerful effects will require the expenditure of specific resources. These can include sacrificing creatures, discarding cards from hand, or other defined costs, further influencing decision-making.
c. Space Limitations: The hard limit of 5 creature slots per side constrains deployment. Hand size is implicitly managed by the rate of drawing versus playing or discarding cards.
d. Domino Effects: The game intentionally embraces complex chain reactions. Balance involves managing the predictability and impact of these cascades, requiring players to assess risk versus reward.
12. Development Goals & Player Experience:
a. Reward: Strategic Planning, Creative Deckbuilding, Adaptation, Comeback potential.
b. Avoid: Complicated Math, Unfair Situations (where extreme luck variance feels unwinnable). The focus is on clear cause and effect. The scheduled draw mechanic aids in creating a predictable tempo.
13. Prototype Karma System (Future Consideration):
A "Karma System" is being considered as a subtle, background mechanic. Its purpose would be to mitigate extreme "bad luck" streaks that might arise from inherent game randomness. This system would notionally track statistically improbable negative outcomes for a player.
If such a streak is detected, future low-impact random resolutions (e.g., certain random target selections where multiple valid targets exist, or truly random tie-breaks not covered by other rules) might receive an infinitesimally small, temporary bias in that player's favor.
The goal of this system is to gently nudge probabilities towards a perceived fairness over a longer game, not to directly influence outcomes or become a strategic element for players to consider.
This effect would be:
a. Extremely minor and often imperceptible.
b. Designed to be non-exploitable.
c. Quickly self-correcting or decaying once a "favorable" random outcome occurs for the player.
d. Aimed at improving player experience by reducing "feel-bad" moments that result from severe statistical outliers, rather than impacting core strategic decisions or win conditions.
e. Its implementation would require rigorous testing and be carefully balanced to ensure it remains a background "smoothing" effect and doesn't introduce new imbalances.
14. Core Design Tenet - Double-Edged Sword:
Significant positive effects should generally have an associated drawback, risk, or cost.
Conversely, significant negative effects or costs should ideally offer a potential upside, a niche use, or some form of benefit.
Most cards should have a primary effect plus a secondary drawback or mitigator, even if it's a minor one.
Existing examples remain valid: "OnMove: +1 Attack" / "OnAttacked: Controller takes 1 damage"; Doomed (+Attack before death); Reckless Assault (+Damage for recoil).

VI. Illustrative Content Examples

A. Example Status Groupings
Information Warfare States (Undercover [ability], `Concealed` [status], `Compromised` [status], `Revealed` [status]), Debuffs/Control (Bored, Delayed, Heavy, Suppressed, Cursed, Ostracized, Addicted, Targeted, Doomed, Depressed, Tired, Bleeding/Corroding), Buffs/Utility (Blessed, Caffeinated), Initial State (Deployment Time).

B. Example Passive Ability Concepts
Ninja Step (ignores slot effects), Resolute (survive lethal once), Adrenal Surge (heals low HP once), Overcharge/Reckless Assault (bonus damage + recoil), Kinetic Backlash (recoil based on damage dealt), Essence Tap/Vampiric Strike (life drain), **Undercover** (All actions this creature queues are `Concealed Actions`). **Set-up Interference:** (e.g., "Pre-emptive Scramble: Set-up. If an opponent queues 3 or more Spell actions this turn, the opponent's Spell action with the highest base Priority (or random if tied) has its Priority reduced by 2 during the Pre-Resolution Interference Phase.").

C. Example Card Concepts
(Refer to the separate document: "Illustrative Card Examples List" for specific card details. This list is maintained as per the Document Usage Guidelines.)