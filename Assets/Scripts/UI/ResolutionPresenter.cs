using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Enums;

/// <summary>
/// Plays visual feedback for every resolved action on a screen overlay: attacks, spells, damage,
/// healing, armor, buffs, statuses, deaths and life lost by players. Each feedback "beat" holds the
/// resolution for a moment, so the turn shows its effects one after another (see TurnManager).
/// </summary>
public class ResolutionPresenter : MonoBehaviour, IResolutionPresenter {
    // How long each kind of beat holds the resolution before the next action runs
    private const float AttackBeat = 0.45f;
    private const float SpellBeat = 1.0f;
    private const float SummonBeat = 0.4f;
    private const float SpellLeavesHandBeat = 0.5f;
    private const float HitBeat = 0.55f;
    private const float StatusBeat = 0.65f;
    private const float DeathBeat = 0.65f;
    private const float PlayerBeat = 0.75f;

    // Labels that land on the same target in quick succession are stacked instead of overlapping
    private const float StackWindow = 0.35f;
    private const float StackSpacing = 40f;

    private static readonly Color DamageColor = new Color(1f, 0.32f, 0.25f);
    private static readonly Color HealColor = new Color(0.4f, 0.92f, 0.5f);
    private static readonly Color ArmorColor = new Color(0.5f, 0.78f, 1f);
    private static readonly Color BuffColor = new Color(1f, 0.68f, 0.22f);
    private static readonly Color DebuffColor = new Color(0.75f, 0.55f, 1f);
    private static readonly Color StatusColor = new Color(1f, 0.9f, 0.35f);

    private RectTransform overlay;
    private IGameMediator mediator;
    private IGameReferences references;
    private IGameManager gameManager;
    private float busyUntil;

    // Last known stats per creature, to show what a stat or armor change actually was
    private readonly Dictionary<string, CreatureSnapshot> snapshots = new Dictionary<string, CreatureSnapshot>();
    private readonly Dictionary<object, (float time, int count)> labelStacks = new Dictionary<object, (float, int)>();

    private struct CreatureSnapshot {
        public int Attack, MaxHealth, Speed, Armor;

        public CreatureSnapshot(ICreature creature) {
            Attack = creature.Attack;
            MaxHealth = creature.MaxHealth;
            Speed = creature.Speed;
            Armor = creature.CurrentArmorPool;
        }
    }

    public static ResolutionPresenter Create(Transform parent) {
        var presenterObject = new GameObject("ResolutionPresenter", typeof(RectTransform));
        presenterObject.transform.SetParent(parent, false);

        var canvas = presenterObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // Above the board, below the tooltip

        var scaler = presenterObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var presenter = presenterObject.AddComponent<ResolutionPresenter>();
        presenter.overlay = (RectTransform)presenterObject.transform;
        return presenter;
    }

    public void Initialize(IGameMediator gameMediator, IGameReferences gameReferences, IGameManager manager) {
        mediator = gameMediator;
        references = gameReferences;
        gameManager = manager;

        gameManager.ActionsQueue.OnActionExecuting.AddListener(OnActionExecuting);
        mediator.AddCreatureSummonedListener(OnCreatureSummoned);
        mediator.AddCreatureDamagedListener(OnCreatureDamaged);
        mediator.AddCreatureHealedListener(OnCreatureHealed);
        mediator.AddCreatureArmorChangedListener(OnCreatureArmorChanged);
        mediator.AddCreatureStatsChangedListener(OnCreatureStatsChanged);
        mediator.AddCreatureStatusChangedListener(OnCreatureStatusChanged);
        mediator.AddCreatureDiedListener(OnCreatureDied);
        mediator.AddPlayerDamagedListener(OnPlayerDamaged);
        mediator.AddPlayerHealedListener(OnPlayerHealed);
    }

    private void OnDestroy() {
        if (mediator == null) return;

        gameManager?.ActionsQueue?.OnActionExecuting.RemoveListener(OnActionExecuting);
        mediator.RemoveCreatureSummonedListener(OnCreatureSummoned);
        mediator.RemoveCreatureDamagedListener(OnCreatureDamaged);
        mediator.RemoveCreatureHealedListener(OnCreatureHealed);
        mediator.RemoveCreatureArmorChangedListener(OnCreatureArmorChanged);
        mediator.RemoveCreatureStatsChangedListener(OnCreatureStatsChanged);
        mediator.RemoveCreatureStatusChangedListener(OnCreatureStatusChanged);
        mediator.RemoveCreatureDiedListener(OnCreatureDied);
        mediator.RemovePlayerDamagedListener(OnPlayerDamaged);
        mediator.RemovePlayerHealedListener(OnPlayerHealed);
        DOTween.Kill(this);
    }

    public IEnumerator WaitUntilIdle() {
        while (Time.time < busyUntil) yield return null;
    }

    private void Hold(float seconds) => busyUntil = Mathf.Max(busyUntil, Time.time + seconds);

    #region Action beats
    private void OnActionExecuting(IGameAction action) {
        switch (action) {
            case BattlefieldCombatAction attack:
                PlayAttack(attack);
                break;
            case PlaySpellAction spell:
                PlaySpellReveal(spell.GetSpell(), spell.GetOwner());
                break;
            case SummonCreatureAction _:
                Hold(SummonBeat); // BattlefieldUI plays the summon pop itself
                break;
            case PlayCardAction play when play.GetCard() is Spell:
                Hold(SpellLeavesHandBeat); // HandUI flies the card out of the hand; one spell at a time
                break;
        }
    }

    private void PlayAttack(BattlefieldCombatAction attack) {
        var attacker = attack.GetAttacker();
        var attackerCard = FindCard(attacker);
        var targetSlot = attack.GetTargetSlot();
        if (attackerCard == null || targetSlot == null || attacker.IsDead) return;
        if (!TryGetOverlayPoint(attackerCard.transform, out var from) || !TryGetOverlayPoint(targetSlot.transform, out var to)) return;

        attackerCard.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f).SetTarget(this);

        var swords = CreateLabel(EffectIcons.Tag(EffectIcons.Attack), Color.white, 64);
        swords.rectTransform.anchoredPosition = from;
        DOTween.Sequence().SetTarget(this)
            .Append(DOTween.To(() => swords.rectTransform.anchoredPosition, p => swords.rectTransform.anchoredPosition = p, to, 0.28f).SetEase(Ease.InQuad))
            .Join(swords.transform.DOScale(1.4f, 0.28f))
            .Append(DOTween.To(() => swords.alpha, a => swords.alpha = a, 0f, 0.15f))
            .OnComplete(() => Destroy(swords.gameObject));

        Hold(AttackBeat);
    }

    private void PlaySpellReveal(Spell spell, IPlayer caster) {
        var data = spell?.SourceData;
        if (data == null) return;

        var reveal = CardFactory.CreateCardController(spell, data, caster, overlay, mediator, references, gameManager);
        if (reveal == null) return;

        var group = reveal.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;
        var rect = (RectTransform)reveal.transform;
        rect.anchoredPosition = new Vector2(0f, 60f);
        rect.localScale = Vector3.one * 1.2f;

        DOTween.Sequence().SetTarget(this)
            .Append(DOTween.To(() => group.alpha, a => group.alpha = a, 1f, 0.15f))
            .Join(rect.DOScale(1.7f, 0.25f).SetEase(Ease.OutBack))
            .AppendInterval(0.45f)
            .Append(DOTween.To(() => group.alpha, a => group.alpha = a, 0f, 0.25f))
            .Join(rect.DOScale(2f, 0.25f))
            .OnComplete(() => Destroy(reveal.gameObject));

        Hold(SpellBeat);
    }
    #endregion

    #region Creature beats
    private void OnCreatureSummoned(ICreature creature, IPlayer owner) {
        snapshots[creature.TargetId] = new CreatureSnapshot(creature);
    }

    private void OnCreatureDamaged(ICreature creature, int damage) {
        var card = FindCard(creature);
        if (card == null) return;

        FlashCard(card, DamageColor);
        card.transform.DOPunchPosition(new Vector3(10f, 0f, 0f), 0.3f, 12, 0.6f).SetTarget(this);
        ShowOnCard(card, $"-{damage}{EffectIcons.Tag(EffectIcons.Damage)}", DamageColor);
        Hold(HitBeat);
    }

    private void OnCreatureHealed(ICreature creature, int amount) {
        var card = FindCard(creature);
        if (card == null) return;

        FlashCard(card, HealColor);
        ShowOnCard(card, $"+{amount}{EffectIcons.Tag(EffectIcons.Heal)}", HealColor);
        Hold(HitBeat);
    }

    private void OnCreatureArmorChanged(ICreature creature, int newArmor) {
        var previous = GetSnapshot(creature);
        int change = newArmor - previous.Armor;
        previous.Armor = newArmor;
        snapshots[creature.TargetId] = previous;

        var card = FindCard(creature);
        if (card == null || change == 0) return;

        string sign = change > 0 ? "+" : "";
        ShowOnCard(card, $"{sign}{change}{EffectIcons.Tag(EffectIcons.Armor)}", ArmorColor);
        if (change > 0) FlashCard(card, ArmorColor);
        Hold(HitBeat);
    }

    private void OnCreatureStatsChanged(ICreature creature) {
        var previous = GetSnapshot(creature);
        var current = new CreatureSnapshot(creature) { Armor = previous.Armor };
        snapshots[creature.TargetId] = current;

        var card = FindCard(creature);
        if (card == null) return;

        bool shown = ShowStatChange(card, current.Attack - previous.Attack, EffectIcons.Sword)
            | ShowStatChange(card, current.MaxHealth - previous.MaxHealth, EffectIcons.Heart)
            | ShowStatChange(card, current.Speed - previous.Speed, EffectIcons.Speed);
        if (shown) Hold(HitBeat);
    }

    private bool ShowStatChange(CardController card, int change, string icon) {
        if (change == 0) return false;

        var color = change > 0 ? BuffColor : DebuffColor;
        string arrow = EffectIcons.Tag(change > 0 ? EffectIcons.Buff : EffectIcons.Debuff);
        ShowOnCard(card, $"{arrow}{(change > 0 ? "+" : "")}{change}{EffectIcons.Tag(icon)}", color);
        FlashCard(card, color);
        return true;
    }

    private void OnCreatureStatusChanged(ICreature creature, StatusEffectType status, bool applied) {
        var card = FindCard(creature);
        string icon = EffectIcons.ForStatus(status);
        if (!applied || card == null || icon == null) return;

        FlashCard(card, StatusColor);
        PopIcon(card.transform, icon, 120f);
        ShowOnCard(card, $"{status}!", StatusColor, 34f);
        Hold(StatusBeat);
    }

    private void OnCreatureDied(ICreature creature) {
        snapshots.Remove(creature.TargetId);
        var card = FindCard(creature);
        if (card == null) return;

        PopIcon(card.transform, EffectIcons.Death, 130f);
        Hold(DeathBeat);
    }
    #endregion

    #region Player beats
    private void OnPlayerDamaged(IPlayer player, int damage) =>
        ShowPlayerChange(player, $"-{damage}{EffectIcons.Tag(EffectIcons.Heart)}", DamageColor);

    private void OnPlayerHealed(IPlayer player, int amount) =>
        ShowPlayerChange(player, $"+{amount}{EffectIcons.Tag(EffectIcons.Heal)}", HealColor);

    private void ShowPlayerChange(IPlayer player, string text, Color color) {
        var healthText = GetHealthText(player);
        if (healthText == null) return;

        var originalColor = healthText.color;
        healthText.transform.DOPunchScale(Vector3.one * 0.35f, 0.4f, 8, 0.6f).SetTarget(this);
        DOTween.Sequence().SetTarget(this)
            .Append(DOTween.To(() => healthText.color, c => healthText.color = c, color, 0.1f))
            .AppendInterval(0.3f)
            .Append(DOTween.To(() => healthText.color, c => healthText.color = c, originalColor, 0.3f));

        if (TryGetOverlayPoint(healthText.transform, out var point)) {
            FloatLabel(healthText, point + new Vector2(140f, 0f), text, color, 58f);
        }
        Hold(PlayerBeat);
    }

    private TextMeshProUGUI GetHealthText(IPlayer player) {
        var playerUI = player.IsPlayer1 ? references.GetPlayer1UI() : references.GetPlayer2UI();
        return playerUI != null ? playerUI.GetComponentInChildren<TextMeshProUGUI>() : null;
    }
    #endregion

    #region Visual helpers
    private CardController FindCard(ICreature creature) {
        if (creature == null) return null;
        return references.GetPlayer1BattlefieldUI()?.GetCardController(creature)
            ?? references.GetPlayer2BattlefieldUI()?.GetCardController(creature);
    }

    private CreatureSnapshot GetSnapshot(ICreature creature) {
        if (!snapshots.TryGetValue(creature.TargetId, out var snapshot)) {
            snapshot = new CreatureSnapshot(creature);
            snapshots[creature.TargetId] = snapshot;
        }
        return snapshot;
    }

    // Converts a board object's position (world-space canvas) into a point on this overlay
    private bool TryGetOverlayPoint(Transform target, out Vector2 point) {
        point = default;
        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return false;

        var rootCanvas = canvas.rootCanvas;
        var eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, target.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screenPoint, null, out point);
    }

    private void ShowOnCard(CardController card, string text, Color color, float size = 46f) {
        if (TryGetOverlayPoint(card.transform, out var point)) {
            FloatLabel(card, point, text, color, size);
        }
    }

    // A label that pops in over the target, drifts up and fades out
    private void FloatLabel(object stackKey, Vector2 at, string text, Color color, float size) {
        var label = CreateLabel(text, color, size);
        var rect = label.rectTransform;
        rect.anchoredPosition = at + NextStackOffset(stackKey);
        label.transform.localScale = Vector3.one * 0.4f;

        DOTween.Sequence().SetTarget(this)
            .Insert(0f, label.transform.DOScale(1.15f, 0.15f).SetEase(Ease.OutBack))
            .Insert(0.15f, label.transform.DOScale(1f, 0.1f))
            .Insert(0f, DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, rect.anchoredPosition + new Vector2(0f, 70f), 0.9f).SetEase(Ease.OutCubic))
            .Insert(0.6f, DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.3f))
            .OnComplete(() => Destroy(label.gameObject));
    }

    // A big icon that bursts over the target and fades
    private void PopIcon(Transform target, string icon, float size) {
        if (!TryGetOverlayPoint(target, out var point)) return;

        var label = CreateLabel(EffectIcons.Tag(icon), Color.white, size);
        label.rectTransform.anchoredPosition = point;
        label.transform.localScale = Vector3.one * 0.3f;

        DOTween.Sequence().SetTarget(this)
            .Append(label.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack))
            .AppendInterval(0.25f)
            .Append(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.25f))
            .Join(label.transform.DOScale(1.5f, 0.25f))
            .OnComplete(() => Destroy(label.gameObject));
    }

    private Vector2 NextStackOffset(object key) {
        int count = labelStacks.TryGetValue(key, out var stack) && Time.time - stack.time < StackWindow ? stack.count + 1 : 0;
        labelStacks[key] = (Time.time, count);
        return new Vector2(0f, -StackSpacing * count);
    }

    private TextMeshProUGUI CreateLabel(string text, Color color, float size) {
        var labelObject = new GameObject("Feedback", typeof(RectTransform));
        labelObject.transform.SetParent(overlay, false);

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = color;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(20, 16, 24, 255);
        label.rectTransform.sizeDelta = new Vector2(400f, 140f);
        return label;
    }

    // A colored overlay on the card that fades out
    private void FlashCard(CardController card, Color color) {
        var flashObject = new GameObject("Flash", typeof(RectTransform));
        flashObject.transform.SetParent(card.transform, false);
        var rect = (RectTransform)flashObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var flash = flashObject.AddComponent<Image>();
        flash.raycastTarget = false;
        flash.color = new Color(color.r, color.g, color.b, 0.6f);
        DOTween.To(() => flash.color.a, a => flash.color = new Color(color.r, color.g, color.b, a), 0f, 0.4f)
            .SetTarget(this)
            .OnComplete(() => Destroy(flashObject));
    }
    #endregion
}
