using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace InterviewSpire
{
    /// <summary>UGUI 运行时搭建界面：不依赖预制体，打开工程按 Play 即可看到。</summary>
    public partial class GameUI : MonoBehaviour
    {
        private GameController game;
        private Font font;
        private Canvas canvas;
        private Transform root, hud;
        private GameObject modal, modalBlocker;
        private TextMeshProUGUI floorText, turnText, hpText, blockText, energyText, enemyText,
                      enemyHpText, questionText, questionTag, intentText, statusText;
        private TextMeshProUGUI drawCountText, discardCountText;
        private Transform handArea;
        private Image enemyAvatar, playerAvatar, hpFill, enemyHpFill, dropHighlight;
        private Image[] dropBorderSides;
        private RectTransform fxAnchor;
        private TextMeshProUGUI sideLogText;
        private RectTransform dropZone;
        private Button starButton;
        private Sprite buttonSprite;
        private TMP_FontAsset tmpFont;
        private CanvasScaler canvasScaler;
        private int lastScreenW, lastScreenH;
        private System.Action settingsBack;
        private AudioSource sfx;
        private AudioClip clickClip, hitClip;
        private bool loadFromStart = false;
        private bool effectsEnabled = true;
        private float effectIntensity = 1f;

        private static readonly Color Panel = Hex("#151C28");
        private static readonly Color Panel2 = Hex("#1B2432");
        private static readonly Color Line = Hex("#2B3648");
        private static readonly Color TextColor = Hex("#E8EEF6");
        private static readonly Color Muted = Hex("#8C9BAD");
        private static readonly Color Gold = Hex("#E2B45F");
        private static readonly Color Red = Hex("#EE6B6B");
        private static readonly Color Teal = Hex("#5CC9B1");
        private static readonly Color Dark = Hex("#0A0E15");

        private static Color Hex(string s)
        {
            Color c; return ColorUtility.TryParseHtmlString(s, out c) ? c : Color.white;
        }

        private void Update()
        {
            if (canvasScaler != null && (lastScreenW != Screen.width || lastScreenH != Screen.height))
                ApplyCanvasMatch();
            if (modal != null || game == null || game.State == null || game.State.phase != 0) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) game.EndTurn();
            for (int i = 1; i <= 8; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + i)) continue;
                if (game.State.hand.Count >= i) game.PlayCard(game.State.hand[i - 1].uid);
            }
        }

        private void Start()
        {
            game = GameController.Instance != null ? GameController.Instance : GetComponent<GameController>();
            if (game == null) game = FindObjectOfType<GameController>();
            font = LoadFont();
            try
            {
                if (font != null) tmpFont = TMP_FontAsset.CreateFontAsset(font);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("TMP 动态字体创建失败，TextMeshPro 将使用默认字体资源：" + ex.Message);
            }
            InitAudio();
            EnsureEventSystem();
            Build();
            game.Changed += Refresh;
            game.NeedReward += ShowReward;
            game.NeedEvent += ShowEvent;
            game.GameOver += ShowGameOver;
            game.Effect += ShowEffect;
            ShowStart();
        }

        private void InitAudio()
        {
            sfx = gameObject.GetComponent<AudioSource>();
            if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            clickClip = MakeTone("ui_click", 880f, 0.06f);
            hitClip = MakeTone("fx_hit", 180f, 0.12f);
        }

        private static AudioClip MakeTone(string name, float freq, float seconds)
        {
            int rate = 22050;
            int len = Mathf.RoundToInt(rate * seconds);
            var clip = AudioClip.Create(name, len, 1, rate, false);
            var data = new float[len];
            for (int i = 0; i < len; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - i / (float)len) * 0.35f;
            clip.SetData(data, 0);
            return clip;
        }

        private Font LoadFont()
        {
            var bundled = Resources.Load<Font>("Fonts/SimHei");
            if (bundled != null) return bundled;
            var f = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private Image PanelAt(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var r = Place(parent, name, x, y, w, h);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Image StretchAt(Transform parent, string name, Sprite sprite, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite Spr(string path)
        {
            // Resources.Load 的路径不需要扩展名
            if (path.EndsWith(".png")) path = path.Substring(0, path.Length - 4);
            return Resources.Load<Sprite>("Art/" + path);
        }

        private Image SpriteAt(Transform parent, string name, float x, float y, float w, float h,
                               Sprite sprite, Color? tint = null)
        {
            var img = PanelAt(parent, name, x, y, w, h, tint ?? Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return img;
        }

        private TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float w, float h,
                           int size = 14, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var r = Place(parent, name, x, y, w, h);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = tmpFont; t.text = text; t.fontSize = size; t.color = color ?? TextColor;
            t.alignment = ToAlignment(align);
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private static TextAlignmentOptions ToAlignment(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.MidlineLeft;
            }
        }

        private Button ButtonAt(Transform parent, string label, float x, float y, float w, float h,
                                Action action, Color? bg = null, Color? fg = null, int size = 13)
        {
            var img = PanelAt(parent, "Btn_" + label, x, y, w, h, bg ?? Panel2);
            if (buttonSprite != null && bg == null)
            {
                img.sprite = buttonSprite;
                img.color = Color.white;
            }
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Gold;
            colors.pressedColor = Gold * .8f;
            btn.colors = colors;
            if (action != null) btn.onClick.AddListener(() =>
            {
                if (sfx != null && clickClip != null) sfx.PlayOneShot(clickClip, 0.18f);
                action();
            });
            Label(img.transform, "Label", label, 2, 2, w - 4, h - 4, size, fg ?? TextColor,
                  TextAnchor.MiddleCenter);
            return btn;
        }

        private void ApplyCanvasMatch()
        {
            if (canvasScaler == null) return;
            float refAspect = 1240f / 820f;
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : refAspect;
            // 宽屏按高度匹配避免底部裁切；窄屏按宽度匹配避免右侧裁切
            canvasScaler.matchWidthOrHeight = screenAspect >= refAspect ? 1f : 0f;
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
        }

        private void Build()
        {
            buttonSprite = Spr("UI/ui_btn_normal@v3.png");
            var canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasScaler = canvasGo.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1240, 820);
            ApplyCanvasMatch();
            root = canvasGo.transform;

            var display = DisplaySettings.Load();
            DisplaySettings.Apply(display.fullScreen, display.width, display.height, false);

            // 选择界面专用的纯色背景，战斗 HUD 隐藏时仍然可见
            StretchAt(root, "StartBG", null, Hex("#0B0F16"));
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(root, false);
            hud = hudGo.transform;
            var hr = (RectTransform)hud;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = hr.offsetMax = Vector2.zero;

            var bgSprite = Spr("Backgrounds/面试间背景@P1_v1.png");
            StretchAt(hud, "BG", bgSprite, bgSprite != null ? new Color(1, 1, 1, .35f) : Hex("#0B0F16"));
            var top = PanelAt(hud, "Top", 16, 12, 1208, 58, Panel);
            Label(top.transform, "Title", "面试尖塔 · INTERVIEW SPIRE", 18, 8, 460, 40, 20, Gold);
            floorText = Label(top.transform, "Floor", "", 560, 8, 160, 40, 14, Muted, TextAnchor.MiddleCenter);
            turnText = Label(top.transform, "Turn", "", 720, 8, 120, 40, 14, Muted, TextAnchor.MiddleCenter);
            string[] names = { "说明", "设置", "统计", "图鉴", "存档", "读档", "重开", "退出" };
            Action[] acts = { ShowHelp, () => { settingsBack = null; ShowSettings(); }, ShowStats, () => ShowCardGallery(0), ShowSave,
                              () => { loadFromStart = false; ShowLoad(); }, Restart, Exit };
            for (int i = 0; i < names.Length; i++)
                ButtonAt(top.transform, names[i], 830 + i * 48, 12, 44, 32, acts[i], Panel2, TextColor, 12);

            var player = PanelAt(hud, "Player", 16, 86, 240, 350, Panel);
            Label(player.transform, "Header", "候选人 · 你   YOU", 14, 10, 210, 22, 12, Muted);
            playerAvatar = SpriteAt(player.transform, "PlayerAvatar", 68, 38, 104, 92,
                                    Spr("Player/avatar.png"));
            hpText = Label(player.transform, "HP", "", 14, 142, 210, 22, 15, TextColor);
            var hpBarBg = PanelAt(player.transform, "HPBarBg", 14, 165, 212, 14, Hex("#2B3648"));
            hpFill = PanelAt(hpBarBg.transform, "Fill", 0, 0, 212, 14, Teal);
            Label(player.transform, "BlockLbl", "镇定 BLOCK", 14, 190, 100, 20, 12, Muted);
            Label(player.transform, "EnergyLbl", "精力 ENERGY", 124, 190, 100, 20, 12, Muted);
            blockText = Label(player.transform, "Block", "", 14, 210, 100, 28, 20, Teal);
            energyText = Label(player.transform, "Energy", "", 124, 210, 100, 28, 20, Gold);
            statusText = Label(player.transform, "Status", "", 14, 244, 212, 62, 11, Red, TextAnchor.UpperLeft);

            var arena = PanelAt(hud, "Arena", 270, 86, 698, 350, Panel);
            intentText = Label(arena.transform, "Intent", "", 16, 4, 660, 26, 14, Hex("#F3B9A4"), TextAnchor.MiddleCenter);
            enemyText = Label(arena.transform, "Enemy", "", 190, 30, 318, 26, 18, TextColor, TextAnchor.MiddleCenter);
            enemyAvatar = SpriteAt(arena.transform, "EnemyAvatar", 279, 64, 140, 104, null);
            var enemyBarBg = PanelAt(arena.transform, "EnemyHPBarBg", 199, 176, 300, 12, Hex("#2B3648"));
            enemyHpFill = PanelAt(enemyBarBg.transform, "Fill", 0, 0, 300, 12, Gold);
            enemyHpText = Label(arena.transform, "EnemyHP", "", 190, 192, 318, 22, 13, Gold, TextAnchor.MiddleCenter);
            dropHighlight = PanelAt(arena.transform, "DropHighlight", 0, 0, 698, 350, new Color(1f, .84f, .37f, 0f));
            dropHighlight.raycastTarget = false;
            dropZone = dropHighlight.rectTransform;
            fxAnchor = enemyAvatar.rectTransform;
            dropBorderSides = new[]
            {
                PanelAt(arena.transform, "DropBorderTop", 0, 0, 698, 4, Gold),
                PanelAt(arena.transform, "DropBorderBottom", 0, 346, 698, 4, Gold),
                PanelAt(arena.transform, "DropBorderLeft", 0, 0, 4, 350, Gold),
                PanelAt(arena.transform, "DropBorderRight", 694, 0, 4, 350, Gold)
            };
            foreach (var side in dropBorderSides)
            {
                side.raycastTarget = false;
                side.transform.SetAsLastSibling();
                side.gameObject.SetActive(false);
            }
            questionTag = Label(arena.transform, "QTag", "", 20, 226, 200, 24, 12, Gold);
            questionText = Label(arena.transform, "Question", "", 20, 252, 660, 64, 15, TextColor);

            var logPanel = PanelAt(hud, "LogPanel", 984, 86, 240, 350, Panel);
            Label(logPanel.transform, "LogTitle", "面试记录 · LOG", 12, 8, 210, 20, 12, Muted);
            sideLogText = Label(logPanel.transform, "SideLog", "", 12, 36, 216, 300, 11,
                                TextColor, TextAnchor.UpperLeft);

            var handPanel = PanelAt(hud, "HandPanel", 16, 452, 1208, 352, Hex("#0D121B"));
            starButton = ButtonAt(handPanel.transform, "⭐ STAR · 回答本题", 16, 14, 300, 52,
                                  ShowStarQuestion, Gold, Dark, 16);
            handArea = Place(handPanel.transform, "Hand", 16, 84, 1080, 224);
            ButtonAt(handPanel.transform, "结束回合\n↵", 1104, 108, 92, 54, () => game.EndTurn(), Gold, Hex("#1A1409"), 15);
            drawCountText = Label(handPanel.transform, "DrawCount", "", 16, 310, 240, 26, 12, Teal);
            discardCountText = Label(handPanel.transform, "DiscardCount", "", 952, 310, 240, 26, 12, Muted, TextAnchor.MiddleRight);

        }

        private void UpdateEnemyAvatar(GameState s)
        {
            if (enemyAvatar == null || s.enemy == null || s.enemy.intent == null) return;
            string[] prefixes = { "hr_", "tech_", "stress_", "final_" };
            string prefix = prefixes[Mathf.Clamp(s.enemy.index, 0, prefixes.Length - 1)];
            string mood = game != null && !string.IsNullOrEmpty(game.EnemyMood) ? game.EnemyMood : "沉思";
            var all = Resources.LoadAll<Sprite>("Art/Enemies");
            var sprite = all.FirstOrDefault(x => x.name.Contains(prefix) && x.name.Contains(mood));
            if (sprite == null) sprite = all.FirstOrDefault(x => x.name.Contains(prefix) && x.name.Contains("头像"));
            enemyAvatar.sprite = sprite;
            enemyAvatar.color = sprite != null ? Color.white : new Color(0, 0, 0, 0);
        }

        private void Refresh()
        {
            if (game.State == null) return;
            var s = game.State;
            var diff = GameDatabase.Data.difficulties.Find(d => d.name == s.diff);
            floorText.text = "第 " + (s.floor + 1) + " / " + GameDatabase.Data.enemies.Count + " 轮";
            turnText.text = "回合 " + s.turn;
            hpText.text = s.confidence + " / " + s.maxConfidence + "  ·  " + s.diff;
            if (hpFill != null)
                hpFill.rectTransform.sizeDelta = new Vector2(212f * Mathf.Clamp01(s.confidence / (float)s.maxConfidence), 14f);
            blockText.text = s.block.ToString();
            energyText.text = s.energy + " / " + s.maxEnergy;
            drawCountText.text = "抽牌堆  " + s.drawPile.Count;
            discardCountText.text = "弃牌堆  " + s.discard.Count;
            var status = new List<string>();
            if (s.block > 0) status.Add("镇定 " + s.block);
            if (s.vulnerable) status.Add("紧张 · 下次受击 ×1.5");
            if (s.selfDoubt) status.Add("自我怀疑 · 伤害 -3");
            if (s.energyPenalty) status.Add("语塞 · 下回合精力 -1");
            if (s.discount > 0) status.Add("下一张费用 -1");
            if (s.techChain) status.Add("追问链 · 下一击 +4");
            if (s.interviewerPressured) status.Add("面试官加压 · 下次造成自信伤害 ×1.5");
            statusText.text = status.Count == 0 ? "当前无状态" : string.Join("\n", status);
            if (starButton != null) starButton.interactable = !s.starUsedThisTurn && s.phase == 0;
            enemyText.text = GameDatabase.Data.enemies[Mathf.Min(s.floor, GameDatabase.Data.enemies.Count - 1)].name;
            UpdateEnemyAvatar(s);
            enemyHpText.text = "血量（耐心） " + s.enemy.hp + " / " + s.enemy.maxHp + "  防御 " + s.enemy.block;
            if (enemyHpFill != null)
                enemyHpFill.rectTransform.sizeDelta = new Vector2(300f * Mathf.Clamp01(s.enemy.hp / (float)s.enemy.maxHp), 12f);
            if (s.enemy.intent == null) intentText.text = "";
            else
            {
                string it = s.enemy.intent.type;
                string detail = it == "block" ? "获得 " + s.enemy.intent.value + " 点防御"
                    : it == "heal" ? "恢复 " + s.enemy.intent.value + " 点耐心"
                    : it == "attackDebuff" ? s.enemy.intent.value + " 点伤害 + 负面状态"
                    : s.enemy.intent.value + " 点自信伤害";
                intentText.text = s.enemy.intent.name + "  ·  " + detail +
                    (s.interviewerPressured ? "  [加压×1.5]" : "");
            }
            questionTag.text = s.job + " · " + s.question.type + "题";
            questionText.text = s.question.text;
            if (sideLogText != null)
                sideLogText.text = string.Join("\n\n", s.logs.Select(l => l.turn + "  " + l.text));

            foreach (Transform child in handArea) Destroy(child.gameObject);
            float cardW = 112f;
            float areaW = ((RectTransform)handArea).rect.width;
            int handCount = s.hand.Count;
            float spacing = handCount <= 1 ? 0f : Mathf.Min(120f, (areaW - cardW) / (handCount - 1));
            if (spacing < 70f && handCount > 1) spacing = 70f;
            float totalW = cardW + (handCount > 1 ? (handCount - 1) * spacing : 0f);
            float startX = (areaW - totalW) * .5f;
            for (int i = 0; i < handCount; i++)
            {
                var rc = s.hand[i];
                var def = GameDatabase.Card(rc.key);
                if (def == null) continue;
                int uid = rc.uid;
                var card = PanelAt(handArea, "Card_" + i, startX + i * spacing, 0, cardW, 200,
                                   def.kind == "attack" ? Hex("#3D2A28") : Hex("#223D3C"));
                card.GetComponent<Image>().raycastTarget = true;
                var drag = card.gameObject.AddComponent<CardDragHandler>();
                drag.game = game; drag.dropZone = dropZone; drag.dropBorderSides = dropBorderSides; drag.canvas = canvas; drag.cardUid = rc.uid;
                drag.Init(((RectTransform)card.transform).anchoredPosition);
                PaintCardFace(card.transform, def, def.key);
            }
        }

        // 手牌 / 奖励 / 图鉴共用：完整插画卡面 + 底部暗色文字条
        private void PaintCardFace(Transform card, CardData def, string key, bool owned = false)
        {
            var art = Spr("Cards/" + key);
            bool fullArt = art != null && art.texture.width >= 300;
            if (fullArt) SpriteAt(card, "Art", 0, 0, 112, 200, art);
            else if (art != null) SpriteAt(card, "Art", 24, 78, 64, 70, art);
            if (owned)
            {
                PanelAt(card, "OwnedTint", 0, 0, 112, 200, new Color(Gold.r, Gold.g, Gold.b, .32f));
                PanelAt(card, "OwnedTop", 0, 197, 112, 3, new Color(Gold.r, Gold.g, Gold.b, .85f));
                PanelAt(card, "OwnedBottom", 0, 0, 112, 3, new Color(Gold.r, Gold.g, Gold.b, .85f));
                PanelAt(card, "OwnedLeft", 0, 0, 3, 200, new Color(Gold.r, Gold.g, Gold.b, .85f));
                PanelAt(card, "OwnedRight", 109, 0, 3, 200, new Color(Gold.r, Gold.g, Gold.b, .85f));
            }
            PanelAt(card, "TextBg", 0, 0, 112, 76, new Color(.04f, .06f, .10f, .82f));
            PanelAt(card, "CostBg", 5, 172, 24, 24, Gold);
            Label(card, "Cost", def.cost.ToString(), 5, 172, 24, 24, 15, Dark, TextAnchor.MiddleCenter);
            Label(card, "Name", def.name, 32, 172, 75, 24, 11, TextColor);
            Label(card, "Meta", def.tag + " · " + Rarity(def.rarity), 6, 150, 100, 16, 9, Muted);
            Label(card, "Desc", def.desc, 6, 6, 100, 62, 10, TextColor, TextAnchor.UpperLeft);
            Label(card, "Rarity", Rarity(def.rarity), 6, 70, 100, 14, 9, Gold, TextAnchor.MiddleCenter);
            if (owned) Label(card, "Owned", "已获得", 34, 156, 62, 16, 9, Red, TextAnchor.MiddleCenter);
        }

        private static string Rarity(string r)
        {
            return r == "rare" ? "稀有" : r == "uncommon" ? "进阶" : r == "common" ? "普通" : "起始";
        }

        private void ShowEffect(string file, int target)
        {
            if (!effectsEnabled) return;
            StartCoroutine(EffectRoutine(file, target));
        }

        private IEnumerator EffectRoutine(string file, int target)
        {
            var sprite = Spr("FX/" + file);
            if (sprite == null) yield break;
            if (sfx != null && hitClip != null) sfx.PlayOneShot(hitClip, 0.22f);
            float size = 140 * effectIntensity;
            Image img;
            if (target == 1 && fxAnchor != null)
            {
                img = SpriteAt(fxAnchor, "FX_" + file,
                    (fxAnchor.rect.width - size) * .5f, (fxAnchor.rect.height - size) * .5f,
                    size, size, sprite);
            }
            else if (target == 0 && playerAvatar != null)
            {
                var pr = playerAvatar.rectTransform;
                img = SpriteAt(pr, "FX_" + file,
                    (pr.rect.width - size) * .5f, (pr.rect.height - size) * .5f,
                    size, size, sprite);
            }
            else
            {
                float x = target == -1 ? 250 : 500;
                img = SpriteAt(root, "FX_" + file, x, 110, size, size, sprite);
            }
            img.raycastTarget = false;
            img.transform.SetAsLastSibling();
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                img.color = new Color(1, 1, 1, 1 - t / 0.45f);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        private void Restart()
        {
            game.ClearCurrentRun();
            ShowStart();
        }

        private void Exit()
        {
            string msg;
            if (game.HasActiveRun && game.SaveOnExit(out msg)) Debug.Log("退出自动存档：" + msg);
            Application.Quit();
        }
    }
}
