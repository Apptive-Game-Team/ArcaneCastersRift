using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneCasters.DataSystem;
using ArcaneCasters.GameSystem;
using ArcaneCasters.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using Util.ChangeListener;

namespace ArcaneCasters.UISystem {
    // Field journal HUD in the theevilent palette (navy, white, orange). Everything sits at the
    // screen edges and stays small so Mira and the fight stay visible.
    [DefaultExecutionOrder(200)]
    public sealed class ArcaneHud : MonoBehaviour, IChangeListener<ArcaneCaster> {
        public ArcaneSession Session;
        public ArcaneInput Controls;

        private static readonly Color Navy = new Color32(13, 34, 56, 238), NavyLight = new Color32(28, 58, 90, 255), NavyLine = new Color32(52, 86, 122, 255),
            Orange = new Color32(255, 154, 28, 255), Muted = new Color32(157, 180, 200, 255), Health = new Color32(240, 96, 60, 255), ManaBlue = new Color32(79, 168, 255, 255);
        private static readonly string[] SlotKeys = { "좌클릭", "Q", "E", "1", "Space" };
        private static readonly string[] IconNames = { "bolt", "summon", "lightning", "frost", "roots", "ward", "dash" };

        private RectTransform canvas;
        private Sprite white, triangle, vignetteSprite;
        private Text levelText, healthText, objectiveText, distanceText, toastText, bossName, promptLabel, potionCount;
        private Image healthFill, manaFill, bossFill, goalArrow, vignette, deathShade;
        private RectTransform toastPanel, prompt, bossPanel;
        private GameObject deathPanel;
        private readonly SkillSlot[] slots = new SkillSlot[5];
        private float toastUntil, hurtAt = -10;

        private GameObject dialoguePanel, dim;
        private Image leftPortrait, rightPortrait;
        private Text speakerText, speechText, pageText;
        private RectTransform speakerPlate, speechRect;
        private readonly Queue<DialogueLine> pending = new Queue<DialogueLine>();
        private Action afterDialogue;
        private int pageIndex, pageCount;

        private GameObject journal, pause;
        private Text journalPoints, detailName, detailRole, detailBody, upgradeLabel;
        private Image detailIcon;
        private readonly List<JournalRow> rows = new List<JournalRow>();
        private readonly Text[] equipLabels = new Text[3];
        private ArcaneSpell selected = ArcaneSpell.Bolt;

        private sealed class SkillSlot { public Image frame, icon, cooldown; public Text key, timer; }
        private sealed class JournalRow { public ArcaneSpell spell; public Image back, marker, icon; public Text name, rank; }

        private IEnumerator Start() {
            yield return null;
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            triangle = MakeTriangle();
            vignetteSprite = MakeVignette();
            var root = new GameObject("Field journal HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            canvas = root.GetComponent<RectTransform>();

            vignette = Img(canvas, "Hurt vignette", Vector2.zero, Vector2.zero, new Color(1, .2f, .1f, 0), Stretch());
            vignette.sprite = vignetteSprite;
            BuildVitals();
            BuildObjective();
            BuildBoss();
            BuildSkillBar();
            BuildToast();
            BuildPrompt();
            BuildDeath();
            BuildDialogue();
            BuildJournal();
            BuildPause();

            Session.OnToast += ShowToast;
            Session.OnDialogue += ShowDialogue;
            Session.OnFloatText += FloatText;
            Session.OnPlayerHurt += Hurt;
            Session.OnPlayerDown += ShowDeath;
            Session.OnPlayerRespawn += HideDeath;
            Session.Player.Status.AddListener(this);
            OnChanged(Session.Player);
            Controls.ToggleBook = ToggleJournal;
            Controls.TogglePause = TogglePause;
            Controls.AdvanceDialogue = AdvanceDialogue;
            UpdateSlots(Session.Player);
        }

        // ---------- builders ----------
        private void BuildVitals() {
            var panel = Panel(canvas, "Vitals", new Vector2(16, 14), new Vector2(286, 76), Navy, TopLeft);
            var window = Panel(panel, "Portrait window", new Vector2(8, 8), new Vector2(60, 60), NavyLight, TopLeft);
            window.gameObject.AddComponent<RectMask2D>();
            var face = Img(window, "Portrait", new Vector2(-56, -31), new Vector2(150, 139), Color.white, TopLeft);
            face.sprite = Session.Assets.Portrait("mira");
            face.preserveAspect = true;
            Label(panel, ArcaneSession.Mira, new Vector2(80, 8), new Vector2(120, 22), 17, Orange, TopLeft);
            levelText = Label(panel, "", new Vector2(190, 10), new Vector2(84, 20), 13, Muted, TopLeft, TextAnchor.UpperRight);
            healthFill = Bar(panel, new Vector2(80, 34), new Vector2(194, 14), Health);
            healthText = Label(healthFill.transform.parent, "", new Vector2(0, -1), new Vector2(194, 16), 11, Color.white, TopLeft, TextAnchor.MiddleCenter);
            manaFill = Bar(panel, new Vector2(80, 54), new Vector2(194, 8), ManaBlue);
        }

        private void BuildObjective() {
            var panel = Panel(canvas, "Objective", new Vector2(-336, 14), new Vector2(320, 66), Navy, TopRight);
            Label(panel, "현장 기록", new Vector2(14, 8), new Vector2(200, 18), 12, Orange, TopLeft);
            objectiveText = Label(panel, "", new Vector2(14, 28), new Vector2(240, 30), 17, Color.white, TopLeft);
            distanceText = Label(panel, "", new Vector2(236, 8), new Vector2(70, 18), 12, Muted, TopLeft, TextAnchor.UpperRight);
            goalArrow = Img(panel, "Goal arrow", new Vector2(272, 30), new Vector2(26, 26), Orange, TopLeft);
            goalArrow.sprite = triangle;
            goalArrow.rectTransform.pivot = new Vector2(.5f, .5f);
            goalArrow.rectTransform.anchoredPosition = new Vector2(285, -43);
        }

        private void BuildBoss() {
            bossPanel = Panel(canvas, "Boss", new Vector2(-220, 92), new Vector2(440, 40), Navy, TopCenter);
            bossName = Label(bossPanel, "", new Vector2(0, 3), new Vector2(440, 18), 14, Color.white, TopLeft, TextAnchor.MiddleCenter);
            bossFill = Bar(bossPanel, new Vector2(16, 24), new Vector2(408, 9), Health);
            bossPanel.gameObject.SetActive(false);
        }

        private void BuildSkillBar() {
            const float size = 50, gap = 6;
            float total = size * 5 + gap * 4 + 16 + (44 + gap) * 2;
            float x = -total / 2;
            for (int i = 0; i < 5; i++) {
                slots[i] = Slot(new Vector2(x + i * (size + gap), -66), size, SlotKeys[i]);
            }
            float extra = x + 5 * (size + gap) + 10;
            var potion = Slot(new Vector2(extra, -60), 44, "R");
            potion.icon.sprite = Session.Assets.Icon("potion");
            potion.icon.enabled = true;
            potionCount = potion.timer;
            potionCount.alignment = TextAnchor.LowerRight;
            potionCount.fontSize = 14;
            var heal = potion.frame.gameObject.AddComponent<Button>();
            heal.targetGraphic = potion.frame;
            heal.onClick.AddListener(() => Session.Player.Heal());
            var book = Slot(new Vector2(extra + 44 + gap, -60), 44, "K");
            book.icon.sprite = Session.Assets.Icon("journal");
            book.icon.enabled = true;
            book.timer.text = "";
            var open = book.frame.gameObject.AddComponent<Button>();
            open.targetGraphic = book.frame;
            open.onClick.AddListener(ToggleJournal);
        }

        private SkillSlot Slot(Vector2 position, float size, string key) {
            var frame = Panel(canvas, "Slot " + key, position, new Vector2(size, size), Navy, BottomCenter);
            frame.GetComponent<Image>().raycastTarget = true;
            Outline(frame, NavyLine);
            var icon = Img(frame, "Icon", new Vector2(5, 5), new Vector2(size - 10, size - 10), Color.white, TopLeft);
            icon.preserveAspect = true;
            var cooldown = Img(frame, "Cooldown", Vector2.zero, new Vector2(size, size), new Color(0, 0, 0, .62f), TopLeft);
            cooldown.sprite = white;
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0;
            var timer = Label(frame, "", Vector2.zero, new Vector2(size - 4, size - 2), 15, Color.white, TopLeft, TextAnchor.MiddleCenter);
            var badge = Panel(frame, "Key", new Vector2(-2, -8), new Vector2(key.Length > 2 ? 38 : 18, 15), Orange, TopLeft);
            var keyText = Label(badge, key, Vector2.zero, badge.sizeDelta, 10, new Color32(13, 34, 56, 255), TopLeft, TextAnchor.MiddleCenter);
            return new SkillSlot { frame = frame.GetComponent<Image>(), icon = icon, cooldown = cooldown, key = keyText, timer = timer };
        }

        private void BuildPrompt() {
            prompt = Panel(canvas, "Interaction prompt", Vector2.zero, new Vector2(200, 32), Navy, Center);
            prompt.pivot = new Vector2(.5f, 0);
            Outline(prompt, Orange);
            var key = Panel(prompt, "Key", new Vector2(6, 5), new Vector2(22, 22), Orange, TopLeft);
            Label(key, "F", Vector2.zero, new Vector2(22, 22), 15, new Color32(13, 34, 56, 255), TopLeft, TextAnchor.MiddleCenter);
            promptLabel = Label(prompt, "", new Vector2(34, 0), new Vector2(160, 32), 15, Color.white, TopLeft, TextAnchor.MiddleLeft);
            var tail = Img(prompt, "Tail", Vector2.zero, new Vector2(14, 9), Orange, BottomCenter);
            tail.sprite = triangle;
            tail.rectTransform.pivot = new Vector2(.5f, .5f);
            tail.rectTransform.anchoredPosition = new Vector2(0, -4.5f);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            prompt.gameObject.SetActive(false);
        }

        private void BuildToast() {
            toastPanel = Panel(canvas, "Toast", new Vector2(-300, 94), new Vector2(600, 34), Navy, TopCenter);
            toastText = Label(toastPanel, "", Vector2.zero, new Vector2(600, 34), 15, Color.white, TopLeft, TextAnchor.MiddleCenter);
            toastPanel.gameObject.SetActive(false);
        }

        private void BuildDeath() {
            deathShade = Img(canvas, "Down shade", Vector2.zero, Vector2.zero, new Color(.02f, .04f, .08f, .62f), Stretch());
            deathPanel = deathShade.gameObject;
            Label(deathShade.rectTransform, "쓰러졌다", new Vector2(-200, -40), new Vector2(400, 50), 34, Color.white, Center, TextAnchor.MiddleCenter);
            Label(deathShade.rectTransform, "야영지에서 다시 시작한다 · 연구 기록은 무사하다", new Vector2(-260, 14), new Vector2(520, 30), 16, Muted, Center, TextAnchor.MiddleCenter);
            deathPanel.SetActive(false);
        }

        private void BuildDialogue() {
            dim = Img(canvas, "Dialogue dim", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .28f), Stretch()).gameObject;
            dim.GetComponent<Image>().raycastTarget = true;
            dim.SetActive(false);
            var panel = Panel(canvas, "Dialogue", new Vector2(-470, -176), new Vector2(940, 158), Navy, BottomCenter);
            panel.GetComponent<Image>().raycastTarget = true;
            Outline(panel, NavyLine);
            dialoguePanel = panel.gameObject;
            leftPortrait = Img(panel, "Left portrait", new Vector2(-10, -298), new Vector2(300, 300), Color.white, TopLeft);
            leftPortrait.preserveAspect = true;
            rightPortrait = Img(panel, "Right portrait", new Vector2(650, -298), new Vector2(300, 300), Color.white, TopLeft);
            rightPortrait.preserveAspect = true;
            speakerPlate = Panel(panel, "Speaker", new Vector2(300, -16), new Vector2(120, 32), Orange, TopLeft);
            speakerText = Label(speakerPlate, "", Vector2.zero, new Vector2(120, 32), 18, new Color32(13, 34, 56, 255), TopLeft, TextAnchor.MiddleCenter);
            speechText = Label(panel, "", new Vector2(300, 30), new Vector2(360, 100), 19, Color.white, TopLeft);
            speechText.lineSpacing = 1.15f;
            speechRect = speechText.rectTransform;
            pageText = Label(panel, "", new Vector2(300, 126), new Vector2(360, 20), 13, Orange, TopLeft, TextAnchor.LowerRight);
            var advance = panel.gameObject.AddComponent<Button>();
            advance.targetGraphic = panel.GetComponent<Image>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(AdvanceDialogue);
            dialoguePanel.SetActive(false);
        }

        private void BuildJournal() {
            var shade = Img(canvas, "Journal shade", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .4f), Stretch());
            shade.raycastTarget = true;
            journal = shade.gameObject;
            var panel = Panel(shade.rectTransform, "Spell journal", new Vector2(-430, -260), new Vector2(860, 520), Navy, Center);
            panel.GetComponent<Image>().raycastTarget = true;
            Outline(panel, NavyLine);
            Label(panel, "마법 연구", new Vector2(28, 20), new Vector2(300, 34), 26, Color.white, TopLeft);
            journalPoints = Label(panel, "", new Vector2(190, 28), new Vector2(300, 24), 16, Orange, TopLeft);
            Button(panel, "닫기  K", new Vector2(736, 20), new Vector2(100, 34), ToggleJournal, NavyLight);
            for (int i = 0; i < 7; i++) {
                var spell = (ArcaneSpell)i;
                var row = Panel(panel, "Row " + i, new Vector2(24, 70 + i * 62), new Vector2(260, 56), NavyLight, TopLeft);
                var back = row.GetComponent<Image>();
                back.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = back;
                button.onClick.AddListener(() => { selected = spell; Session.Audio.Click(); RefreshJournal(); });
                var marker = Img(row, "Selected", Vector2.zero, new Vector2(4, 56), Orange, TopLeft);
                var icon = Img(row, "Icon", new Vector2(12, 8), new Vector2(40, 40), Color.white, TopLeft);
                icon.sprite = Session.Assets.Icon(IconNames[i]);
                icon.preserveAspect = true;
                var name = Label(row, Session.Player.Book.Definition(spell).name, new Vector2(62, 8), new Vector2(180, 22), 16, Color.white, TopLeft);
                var rank = Label(row, "", new Vector2(62, 31), new Vector2(180, 18), 12, Orange, TopLeft);
                rows.Add(new JournalRow { spell = spell, back = back, marker = marker, icon = icon, name = name, rank = rank });
            }
            detailIcon = Img(panel, "Detail icon", new Vector2(312, 74), new Vector2(84, 84), Color.white, TopLeft);
            detailIcon.preserveAspect = true;
            detailName = Label(panel, "", new Vector2(412, 82), new Vector2(420, 32), 24, Color.white, TopLeft);
            detailRole = Label(panel, "", new Vector2(412, 120), new Vector2(420, 22), 14, Muted, TopLeft);
            detailBody = Label(panel, "", new Vector2(312, 176), new Vector2(520, 170), 16, Color.white, TopLeft);
            detailBody.lineSpacing = 1.2f;
            var upgrade = Button(panel, "", new Vector2(312, 370), new Vector2(280, 44), Upgrade, Orange);
            upgradeLabel = upgrade.GetComponentInChildren<Text>();
            upgradeLabel.color = new Color32(13, 34, 56, 255);
            Label(panel, "장착 칸", new Vector2(312, 428), new Vector2(200, 20), 13, Muted, TopLeft);
            string[] slotNames = { "Q", "E · 우클릭", "1" };
            for (int i = 0; i < 3; i++) {
                int slot = i;
                var button = Button(panel, "", new Vector2(312 + i * 178, 452), new Vector2(168, 44), () => Equip(slot), NavyLight);
                equipLabels[i] = button.GetComponentInChildren<Text>();
                equipLabels[i].fontSize = 14;
                equipLabels[i].supportRichText = true;
                equipLabels[i].text = slotNames[i];
            }
            journal.SetActive(false);
        }

        private void BuildPause() {
            var shade = Img(canvas, "Pause shade", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .45f), Stretch());
            shade.raycastTarget = true;
            pause = shade.gameObject;
            var panel = Panel(shade.rectTransform, "Pause", new Vector2(-200, -200), new Vector2(400, 400), Navy, Center);
            Outline(panel, NavyLine);
            Label(panel, "일시 정지", new Vector2(0, 22), new Vector2(400, 36), 26, Color.white, TopLeft, TextAnchor.MiddleCenter);
            Button(panel, "계속하기", new Vector2(50, 76), new Vector2(300, 42), TogglePause, Orange).GetComponentInChildren<Text>().color = new Color32(13, 34, 56, 255);
            Button(panel, "저장하고 종료", new Vector2(50, 128), new Vector2(300, 42), () => { Session.Save(); Application.Quit(); }, NavyLight);
            Label(panel, "WASD  이동\n마우스  조준 · 좌클릭 마력탄\nQ · E(우클릭) · 1  장착한 마법\nSpace  회피      F  조사 · 대화\nR  회복약      K  마법 연구      Esc  일시 정지",
                new Vector2(36, 194), new Vector2(330, 180), 14, Muted, TopLeft).lineSpacing = 1.35f;
            pause.SetActive(false);
        }

        // ---------- per frame ----------
        private void Update() {
            if (!canvas || !Session.Player) return;
            var player = Session.Player;
            objectiveText.text = Session.Objective;
            bool hasGoal = Session.HasGoal;
            goalArrow.enabled = distanceText.enabled = hasGoal;
            if (hasGoal) {
                Vector2 toGoal = Session.Goal - (Vector2)player.transform.position;
                distanceText.text = toGoal.magnitude.ToString("0") + "m";
                goalArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(toGoal.y, toGoal.x) * Mathf.Rad2Deg - 90);
            }
            UpdateSlots(player);
            UpdatePrompt();
            UpdateBoss();
            bool toastVisible = Time.unscaledTime < toastUntil && !dialoguePanel.activeSelf;
            toastPanel.gameObject.SetActive(toastVisible);
            toastPanel.anchoredPosition = new Vector2(-toastPanel.sizeDelta.x / 2, bossPanel.gameObject.activeSelf ? -140 : -94);
            float hurt = Mathf.Clamp01(1 - (Time.unscaledTime - hurtAt) / .45f);
            vignette.color = new Color(1, .18f, .1f, hurt * .38f);
            RefreshUiMode();
        }

        private void RefreshUiMode() {
            Session.UiMode = dialoguePanel.activeSelf ? "dialogue" : journal.activeSelf ? "journal" : pause.activeSelf ? "pause" : "play";
        }

        private void UpdateSlots(ArcaneCaster player) {
            var book = player.Book;
            ArcaneSpell[] spells = { ArcaneSpell.Bolt, book.Slot(0), book.Slot(1), book.Slot(2), ArcaneSpell.Dash };
            for (int i = 0; i < 5; i++) {
                var slot = slots[i];
                var spell = spells[i];
                bool empty = spell == ArcaneSpell.None;
                slot.icon.enabled = !empty;
                if (empty) { slot.cooldown.fillAmount = 0; slot.timer.text = "—"; slot.timer.color = Muted; continue; }
                slot.icon.sprite = Session.Assets.Icon(IconNames[(int)spell]);
                var stats = book.Stats(spell);
                float remaining = player.Ready[(int)spell] - Time.time;
                slot.cooldown.fillAmount = remaining > 0 && stats.cooldown > 0 ? remaining / stats.cooldown : 0;
                slot.timer.text = remaining > .05f && stats.cooldown >= 1 ? remaining.ToString(remaining < 10 ? "0.0" : "0") : "";
                slot.timer.color = Color.white;
                slot.icon.color = player.Mana < stats.cost ? new Color(.5f, .6f, .8f, .7f) : Color.white;
            }
            potionCount.text = player.Potions.ToString();
        }

        private void UpdatePrompt() {
            var target = Session.Paused || Session.Player.Dead ? null : Session.CurrentTarget;
            prompt.gameObject.SetActive(target != null);
            if (target == null) return;
            promptLabel.text = Session.Interaction;
            float width = 48 + promptLabel.preferredWidth;
            prompt.sizeDelta = new Vector2(width, 32);
            Vector2 screen = Camera.main.WorldToScreenPoint(target.Position + Vector2.up * target.PromptHeight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            var area = canvas.rect;
            local.x = Mathf.Clamp(local.x, area.xMin + width / 2 + 12, area.xMax - width / 2 - 12);
            local.y = Mathf.Clamp(local.y + 10, area.yMin + 90, area.yMax - 130);
            prompt.anchoredPosition = local;
        }

        private void UpdateBoss() {
            var boss = Session.Enemies.Find(e => e && e.Boss && e.Alerted && !e.Dead);
            bossPanel.gameObject.SetActive(boss);
            if (!boss) return;
            bossName.text = boss.DisplayName;
            bossFill.fillAmount = boss.CurrentHp / boss.MaxHp;
        }

        public void OnChanged(ArcaneCaster p) {
            if (!healthText) return;
            healthText.text = $"{p.CurrentHp:0} / {p.MaxHp:0}" + (p.CurrentShield > 0 ? $"  +{p.CurrentShield:0}" : "");
            healthFill.fillAmount = p.CurrentHp / p.MaxHp;
            manaFill.fillAmount = p.Mana / 100;
            levelText.text = $"연구 Lv {p.Book.Save.level}";
        }

        // ---------- toast, damage, death ----------
        private void ShowToast(string value) {
            if (!toastText) return;
            toastText.text = value;
            toastPanel.sizeDelta = new Vector2(Mathf.Max(300, toastText.preferredWidth + 48), 34);
            toastText.rectTransform.sizeDelta = toastPanel.sizeDelta;
            toastUntil = Time.unscaledTime + 4.5f;
        }

        private void Hurt(float amount) => hurtAt = Time.unscaledTime;
        private void ShowDeath() { if (deathPanel) deathPanel.SetActive(true); }
        private void HideDeath() { if (deathPanel) deathPanel.SetActive(false); }

        private void FloatText(Vector2 position, string value, Color color) {
            if (canvas) StartCoroutine(Float(position, value, color));
        }

        private IEnumerator Float(Vector2 position, string value, Color color) {
            var label = Label(canvas, value, Vector2.zero, new Vector2(100, 30), 18, color, Center, TextAnchor.MiddleCenter);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .7f);
            for (float t = 0; t < .7f; t += Time.unscaledDeltaTime) {
                Vector2 screen = Camera.main.WorldToScreenPoint(position + Vector2.up * t * .7f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
                label.rectTransform.anchoredPosition = local + new Vector2(-50, 15);
                label.color = new Color(color.r, color.g, color.b, 1 - Mathf.Clamp01((t - .45f) / .25f));
                yield return null;
            }
            Destroy(label.gameObject);
        }

        // ---------- dialogue ----------
        private void ShowDialogue(IReadOnlyList<DialogueLine> lines, Action then) {
            if (journal.activeSelf) CloseJournal();
            if (pause.activeSelf) { pause.SetActive(false); }
            pending.Clear();
            foreach (var line in lines) pending.Enqueue(line);
            afterDialogue = then;
            pageCount = lines.Count;
            pageIndex = 0;
            dialoguePanel.SetActive(true);
            dim.SetActive(true);
            Session.Pause(true);
            RefreshUiMode();
            NextLine();
        }

        private void NextLine() {
            var line = pending.Dequeue();
            pageIndex++;
            bool orinSpeaks = line.Speaker == ArcaneSession.Orin;
            leftPortrait.sprite = Session.Assets.Portrait("mira");
            rightPortrait.sprite = Session.Assets.Portrait("orin");
            bool orinInScene = orinSpeaks || ConversationHasOrin();
            rightPortrait.enabled = orinInScene;
            leftPortrait.color = orinSpeaks ? new Color(.55f, .6f, .7f) : Color.white;
            rightPortrait.color = orinSpeaks ? Color.white : new Color(.55f, .6f, .7f);
            float textX = 300;
            speakerPlate.anchoredPosition = new Vector2(orinSpeaks ? 520 : textX, 16);
            speakerText.text = line.Speaker;
            speechRect.anchoredPosition = new Vector2(textX, -40);
            speechRect.sizeDelta = new Vector2(orinInScene ? 340 : 600, 80);
            speechText.text = line.Text;
            pageText.rectTransform.sizeDelta = new Vector2(orinInScene ? 340 : 600, 20);
            pageText.text = $"{pageIndex} / {pageCount}   F · Enter  계속";
            Session.SetSpeaker(line.Speaker);
        }

        private bool orinInConversation;
        private bool ConversationHasOrin() {
            if (pageIndex == 1) {
                orinInConversation = false;
                foreach (var line in pending) if (line.Speaker == ArcaneSession.Orin) orinInConversation = true;
            }
            return orinInConversation;
        }

        private void AdvanceDialogue() {
            if (!dialoguePanel || !dialoguePanel.activeSelf) return;
            Session.Audio.Click();
            if (pending.Count > 0) { NextLine(); return; }
            dialoguePanel.SetActive(false);
            dim.SetActive(false);
            Session.SetSpeaker("");
            Session.Pause(false);
            RefreshUiMode();
            var then = afterDialogue;
            afterDialogue = null;
            then?.Invoke();
        }

        // ---------- journal and pause ----------
        public void ToggleJournal() {
            if (dialoguePanel.activeSelf) return;
            if (journal.activeSelf) { CloseJournal(); return; }
            if (pause.activeSelf) pause.SetActive(false);
            journal.SetActive(true);
            Session.Pause(true);
            RefreshUiMode();
            RefreshJournal();
        }

        private void CloseJournal() { journal.SetActive(false); Session.Pause(false); RefreshUiMode(); }

        public void TogglePause() {
            if (dialoguePanel.activeSelf) { AdvanceDialogue(); return; }
            if (journal.activeSelf) { CloseJournal(); return; }
            pause.SetActive(!pause.activeSelf);
            Session.Pause(pause.activeSelf);
            RefreshUiMode();
            if (pause.activeSelf) Session.Save();
        }

        private void Upgrade() {
            if (!Session.Player.Book.Upgrade(selected, Session.InCombat)) {
                ShowToast(Session.InCombat ? "전투 중에는 강화할 수 없어." : "배운 마법인지, 연구점이 충분한지 확인해 줘.");
            } else {
                Session.Audio.Chime();
                Session.Save();
            }
            RefreshJournal();
        }

        private void Equip(int slot) {
            if (!Session.Player.Book.Equip(slot, selected, Session.InCombat)) {
                ShowToast(Session.InCombat ? "전투 중에는 바꿀 수 없어." : "마력탄과 회피는 고정이야. 배운 마법을 골라 줘.");
            } else {
                Session.Audio.Click();
                Session.Save();
            }
            RefreshJournal();
        }

        private void RefreshJournal() {
            var book = Session.Player.Book;
            journalPoints.text = "연구점 " + book.Save.research;
            foreach (var row in rows) {
                int rank = book.Rank(row.spell);
                bool chosen = row.spell == selected;
                row.marker.enabled = chosen;
                row.back.color = chosen ? new Color32(40, 76, 112, 255) : NavyLight;
                row.icon.color = rank > 0 ? Color.white : new Color(.4f, .45f, .55f, .8f);
                row.name.color = rank > 0 ? Color.white : Muted;
                row.rank.text = rank > 0 ? new string('●', rank) + new string('○', 3 - rank) + "  " + rank + "단계" : "미발견";
                row.rank.color = rank > 0 ? Orange : Muted;
            }
            var definition = book.Definition(selected);
            int current = book.Rank(selected);
            var stats = book.Stats(selected);
            detailIcon.sprite = Session.Assets.Icon(IconNames[(int)selected]);
            detailIcon.color = current > 0 ? Color.white : new Color(.4f, .45f, .55f, .8f);
            detailName.text = definition.name + (current > 0 ? $"  <size=16><color=#ff9a1c>{current} / 3 단계</color></size>" : "");
            detailRole.text = definition.role;
            string numbers = current == 0 ? "발견 장소 · " + definition.source
                : $"피해 {stats.damage:0}    마력 {stats.cost:0}    재사용 {stats.cooldown:0.#}초";
            detailBody.text = definition.description + "\n\n<color=#9db4c8>" + numbers + "</color>";
            upgradeLabel.text = current == 0 ? "아직 발견하지 못했어" : current >= 3 ? "최대 단계" : $"강화하기 · 연구점 {(current == 1 ? 2 : 3)}";
            string[] slotNames = { "Q", "E · 우클릭", "1" };
            for (int i = 0; i < 3; i++) {
                var equipped = book.Slot(i);
                equipLabels[i].text = slotNames[i] + "\n<size=12><color=#9db4c8>" + (equipped == ArcaneSpell.None ? "비어 있음" : book.Definition(equipped).name) + "</color></size>";
            }
        }

        // ---------- widgets ----------
        private static readonly Vector2 TopLeft = new Vector2(0, 1), TopRight = new Vector2(1, 1), TopCenter = new Vector2(.5f, 1), BottomCenter = new Vector2(.5f, 0), Center = new Vector2(.5f, .5f);
        private static Vector2? Stretch() => null;

        private RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Vector2 anchor) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            Place(rect, pos, size, anchor);
            return rect;
        }

        private Image Img(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Vector2? anchor) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            if (anchor.HasValue) Place(rect, pos, size, anchor.Value);
            else { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            return image;
        }

        private Text Label(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize, Color color, Vector2 anchor, TextAnchor alignment = TextAnchor.UpperLeft) {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Session.Assets.font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Place(text.rectTransform, pos, size, anchor);
            return text;
        }

        private Button Button(Transform parent, string value, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, Color color) {
            var rect = Panel(parent, value, pos, size, color, TopLeft);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.1f, 1f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            button.colors = colors;
            button.onClick.AddListener(action);
            Label(rect, value, Vector2.zero, size, 16, Color.white, TopLeft, TextAnchor.MiddleCenter);
            return button;
        }

        private Image Bar(Transform parent, Vector2 pos, Vector2 size, Color color) {
            var back = Panel(parent, "Bar", pos, size, new Color32(6, 16, 28, 255), TopLeft);
            var fill = Img(back, "Fill", Vector2.zero, size, color, TopLeft);
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }

        private static void Outline(RectTransform rect, Color color) {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static void Place(RectTransform rect, Vector2 pos, Vector2 size, Vector2 anchor) {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(pos.x, -pos.y);
            rect.sizeDelta = size;
        }

        private static Sprite MakeTriangle() {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                float half = (1 - y / (float)(size - 1)) * size / 2f;
                for (int x = 0; x < size; x++) {
                    float d = Mathf.Abs(x + .5f - size / 2f);
                    byte a = (byte)(Mathf.Clamp01(half - d + .5f) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f);
        }

        private static Sprite MakeVignette() {
            const int width = 128, height = 72;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    float dx = (x + .5f) / width * 2 - 1, dy = (y + .5f) / height * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx * .8f + dy * dy * .8f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(Mathf.Clamp01((d - .72f) / .4f), 1.5f) * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f);
        }

        private void OnDestroy() {
            if (!Session) return;
            Session.OnToast -= ShowToast;
            Session.OnDialogue -= ShowDialogue;
            Session.OnFloatText -= FloatText;
            Session.OnPlayerHurt -= Hurt;
            Session.OnPlayerDown -= ShowDeath;
            Session.OnPlayerRespawn -= HideDeath;
        }
    }
}
