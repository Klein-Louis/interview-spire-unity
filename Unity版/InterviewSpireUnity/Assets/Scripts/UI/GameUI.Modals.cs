using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InterviewSpire
{
    public partial class GameUI
    {
        private void CloseModal()
        {
            // 先立即禁用，避免 Destroy 延迟期间遮罩继续拦截点击
            if (modal != null) { modal.SetActive(false); Destroy(modal); }
            if (modalBlocker != null) { modalBlocker.SetActive(false); Destroy(modalBlocker); }
            modal = null; modalBlocker = null;
        }

        private GameObject Modal(string title, string subtitle, float w = 760, float h = 520)
        {
            CloseModal();
            // 全屏拦截层：防止弹窗打开时点击到底层手牌或结束回合
            modalBlocker = new GameObject("ModalBlocker", typeof(RectTransform), typeof(Image));
            modalBlocker.transform.SetParent(root, false);
            var br = (RectTransform)modalBlocker.transform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = br.offsetMax = Vector2.zero;
            var blockerImg = modalBlocker.GetComponent<Image>();
            blockerImg.color = new Color(0, 0, 0, 0.01f);
            blockerImg.raycastTarget = true;

            modal = new GameObject("Modal", typeof(RectTransform));
            modal.transform.SetParent(root, false);
            var r = (RectTransform)modal.transform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = Vector2.zero;
            var img = modal.AddComponent<Image>();
            img.color = Panel;
            img.raycastTarget = true;
            Label(modal.transform, "Title", title, 20, 14, w - 40, 38, 20, Gold, TextAnchor.MiddleCenter);
            Label(modal.transform, "Sub", subtitle, 30, 54, w - 60, 50, 13, Muted, TextAnchor.UpperCenter);
            return modal;
        }

        private void SetHudVisible(bool visible)
        {
            if (hud != null) hud.gameObject.SetActive(visible);
        }

        public void ShowStart()
        {
            SetHudVisible(false);
            var m = Modal("面试尖塔 · 开局设置", "选择岗位方向与难度，开始一局新的面试挑战。", 780, 560);
            Label(m.transform, "JobLbl", "岗位方向（决定题目池）", 40, 120, 300, 26, 14, Gold);
            var jobs = GameDatabase.Data.job_questions.Select(j => j.job).ToList();
            for (int i = 0; i < jobs.Count; i++)
            {
                string job = jobs[i];
                ButtonAt(m.transform, job, 40 + i * 175, 154, 160, 40, () => { game.Job = job; ShowStart(); },
                         game.Job == job ? Gold : Panel2, game.Job == job ? Dark : TextColor);
            }
            Label(m.transform, "DiffLbl", "难度", 40, 220, 300, 26, 14, Gold);
            for (int i = 0; i < GameDatabase.Data.difficulties.Count; i++)
            {
                string d = GameDatabase.Data.difficulties[i].name;
                ButtonAt(m.transform, d, 40 + i * 175, 254, 160, 40, () => { game.Difficulty = d; ShowStart(); },
                         game.Difficulty == d ? Gold : Panel2, game.Difficulty == d ? Dark : TextColor);
            }
            var cur = GameDatabase.Data.difficulties.Find(d => d.name == game.Difficulty);
            Label(m.transform, "Desc", cur == null ? "" : cur.desc, 40, 300, 700, 40, 12, Muted);
            ButtonAt(m.transform, "开始面试", 280, 370, 200, 48, () =>
            { CloseModal(); SetHudVisible(true); game.NewGame(); Refresh(); ShowTutorial(1); }, Gold, Dark, 16);
            ButtonAt(m.transform, "读取存档", 280, 430, 200, 40,
                     () => { loadFromStart = true; ShowLoad(); });
            ButtonAt(m.transform, "退出游戏", 250, 480, 130, 40, Exit, Panel2, Red);
            ButtonAt(m.transform, "设置", 395, 480, 130, 40, () => { settingsBack = ShowStart; ShowSettings(); });
        }

        private void ShowSave()
        {
            if (game.State == null || game.State.phase != 0)
            {
                Modal("存档", "只能在你的回合存档。\n退出时会自动保存当前进度。", 560, 260);
                ButtonAt(modal.transform, "知道了", 220, 180, 120, 40, CloseModal, Gold, Dark);
                return;
            }
            var m = Modal("存档", "输入存档名，会自动附上当前日期时间。", 600, 360);
            var inputBg = PanelAt(m.transform, "Input", 60, 130, 480, 42, Dark);
            inputBg.raycastTarget = true;
            var input = inputBg.gameObject.AddComponent<TMP_InputField>();
            var text = Label(input.transform, "Text", "", 8, 4, 464, 34, 14);
            input.textComponent = text;
            input.textViewport = (RectTransform)input.transform;
            input.text = "存档1";
            ButtonAt(m.transform, "保存", 135, 210, 130, 44, () =>
            {
                string msg;
                bool ok = game.Save(input.text, out msg);
                CloseModal();
                Modal(ok ? "存档成功" : "存档失败", msg, 620, 260);
                ButtonAt(modal.transform, "关闭", 250, 170, 120, 40, CloseModal, Gold, Dark);
            }, Gold, Dark);
            ButtonAt(m.transform, "取消", 335, 210, 130, 44, CloseModal);
            ButtonAt(m.transform, "管理存档（读取 / 删除）", 150, 275, 300, 40, () =>
            {
                loadFromStart = false;
                ShowLoad();
            });
        }

        private void ShowLoad()
        {
            var saves = SaveSystem.List();
            var m = Modal("读取存档", saves.Count == 0 ? "没有找到存档。" : "共 " + saves.Count + " 个存档，选择其中一个继续。", 800, 560);
            int shown = 0;
            foreach (var rec in saves.Take(10))
            {
                string path = rec.path;
                ButtonAt(m.transform, rec.name + "  |  " + rec.time + "  |  " + rec.job + "·" + rec.diff + "  |  第 " + rec.floor + " 轮",
                         30, 120 + shown * 40, 640, 34, () =>
                         {
                             string error;
                             var s = SaveSystem.Read(path, out error);
                             if (s == null)
                             {
                                 Modal("读档失败", error, 600, 240);
                                 ButtonAt(modal.transform, "关闭", 240, 160, 120, 40,
                                          () => { if (loadFromStart) ShowStart(); else CloseModal(); },
                                          Gold, Dark);
                                 return;
                             }
                             CloseModal(); SetHudVisible(true); game.LoadState(s); Refresh();
                         });
                ButtonAt(m.transform, "删除", 680, 120 + shown * 40, 90, 34, () =>
                {
                    Modal("删除存档", "确定删除「" + rec.name + "」吗？此操作不可恢复。", 560, 260);
                    ButtonAt(modal.transform, "确认删除", 120, 170, 140, 42, () =>
                    {
                        SaveSystem.Delete(path);
                        ShowLoad();
                    }, Red, Color.white);
                    ButtonAt(modal.transform, "取消", 300, 170, 140, 42, ShowLoad);
                }, Panel2, Red, 12);
                shown++;
            }
            ButtonAt(m.transform, "取消", 330, 500, 140, 40, () =>
            {
                if (loadFromStart) ShowStart();
                else CloseModal();
            }, Gold, Dark);
        }

        private void ShowReward()
        {
            var m = Modal("本轮通过", "先选择一张卡，再点确认；跳过则恢复 5 点自信。", 520, 470);
            string selectedKey = null;
            TextMeshProUGUI selectedText = Label(m.transform, "Selected", "尚未选择卡牌", 30, 132, 460, 20, 11, Muted);
            var cards = new System.Collections.Generic.List<Image>();
            var highlights = new System.Collections.Generic.List<Image>();
            Button confirmBtn = null;
            var keys = game.RandomRewardKeys(3);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                var def = GameDatabase.Card(key);
                if (def == null) continue;
                float x = 62 + i * 142f;
                // 与手牌完全一致的卡面布局（112×200）
                var card = PanelAt(m.transform, "RewardCard_" + key, x, 160, 112, 200,
                                   def.kind == "attack" ? Hex("#3D2A28") : Hex("#223D3C"));
                cards.Add(card);
                PaintCardFace(card.transform, def, key);
                var hl = PanelAt(card.transform, "SelectedTint", 0, 0, 112, 200,
                                 new Color(Gold.r, Gold.g, Gold.b, 0f));
                highlights.Add(hl);
                ButtonAt(m.transform, "选中此卡", x + 11, 108, 90, 26, () =>
                {
                    selectedKey = key;
                    selectedText.text = "已选择：" + def.name;
                    for (int j = 0; j < highlights.Count; j++)
                        highlights[j].color = new Color(Gold.r, Gold.g, Gold.b,
                            highlights[j].gameObject.transform.parent.name == "RewardCard_" + key ? .42f : 0f);
                    confirmBtn.interactable = true;
                }, Panel2, TextColor, 11);
            }
            confirmBtn = ButtonAt(m.transform, "确认选择", 280, 410, 200, 36, () =>
            {
                if (string.IsNullOrEmpty(selectedKey)) return;
                CloseModal();
                game.TakeReward(selectedKey);
            }, Gold, Dark, 14);
            confirmBtn.interactable = false;
            ButtonAt(m.transform, "跳过选牌 · 恢复 5 点自信", 40, 410, 220, 36,
                     () => { CloseModal(); game.TakeReward(""); }, Gold, Dark);
        }

        private void ShowEvent()
        {
            var events = GameDatabase.Data.events;
            var ev = events[UnityEngine.Random.Range(0, events.Count)];
            var m = Modal("EVENT · " + ev.name, ev.desc, 760, 480);
            for (int i = 0; i < ev.options.Count; i++)
            {
                string effect = ev.options[i].effect;
                ButtonAt(m.transform, ev.options[i].label, 40, 150 + i * 80, 680, 56, () =>
                {
                    CloseModal(); game.ApplyEvent(effect); game.AdvanceToNextBattle();
                });
            }
        }

        private void ShowGameOver(bool won)
        {
            var m = Modal(won ? "恭喜拿到 Offer" : "自信耗尽 · 面试结束",
                          won ? "你穿过了四轮面试。本局存档已清除。" : "本局存档已清除，可以重新开始。", 720, 360);
            Label(m.transform, "Stats", "累计伤害 " + game.State.totalDamage + " · 打出卡牌 " + game.State.totalCards +
                  " · 总回合 " + game.State.totalTurns, 40, 160, 640, 40, 14, Gold, TextAnchor.MiddleCenter);
            ButtonAt(m.transform, "重新挑战", 260, 230, 200, 48, () => { CloseModal(); game.ClearCurrentRun(); ShowStart(); }, Gold, Dark);
        }

        private void ShowHelp()
        {
            var m = Modal("玩法说明", "把面试准备变成卡牌战斗。", 760, 480);
            Label(m.transform, "Rules",
                  "出牌：双击手牌，或拖到面试官区域打出；数字键 1–8 快速出牌。\n" +
                  "STAR：每回合回答一道题，按评分要点转成伤害；低于 60 分会让面试官下次造成伤害 ×1.5。\n" +
                  "题意契合：打出与问题类型相同的卡，伤害 ×1.5。\n" +
                  "精力：每回合 4 点，通用伤害牌首张 +2。\n" +
                  "存档：顶栏可命名存档；通关或重开会清除本局存档。",
                  40, 130, 680, 240, 14, TextColor, TextAnchor.UpperLeft);
            ButtonAt(m.transform, "关闭", 320, 400, 120, 40, CloseModal, Gold, Dark);
        }

        private void ShowStarQuestion()
        {
            if (game == null || game.State == null || game.State.phase != 0 ||
                game.State.starUsedThisTurn) return;
            var q = game.State.question;
            var meta = GameDatabase.Data.question_meta.Find(m => m.id == q.id);
            var m = Modal("STAR · 亮点前置", "按关键词打分：命中关键词或同义词计入要点，每个要点只算一次。", 800, 640);
            Label(m.transform, "QType", "题型：" + q.type, 40, 120, 720, 26, 13, Gold);
            Label(m.transform, "QText", q.text, 40, 152, 720, 70, 17, TextColor, TextAnchor.UpperLeft);
            string points = meta == null ? "（本题暂无关键词）" :
                string.Join("、", meta.keyPoints.Select(k => k.term));
            Label(m.transform, "Points", "评分要点：" + points, 40, 226, 720, 42, 12, Muted);
            Label(m.transform, "Rule", "评分 = 关键词命中（每项20分，最多3项）+ 结构 + 具体性；每个关键词即使出现多个同义词也只计 1 次。", 40, 262, 720, 30, 11, Teal);
            Label(m.transform, "Damage", "优秀（≥80）10 伤害　合格（60–79）5 伤害　低于 60：3 伤害 + 面试官加压", 40, 292, 720, 28, 11, Gold);

            var inputBg = PanelAt(m.transform, "AnswerInput", 40, 322, 720, 178, Dark);
            inputBg.raycastTarget = true;
            var input = inputBg.gameObject.AddComponent<TMP_InputField>();
            var t = Label(input.transform, "Text", "", 10, 6, 700, 166, 13, TextColor, TextAnchor.UpperLeft);
            input.textComponent = t;
            input.textViewport = (RectTransform)input.transform;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;

            ButtonAt(m.transform, "提交回答", 190, 530, 180, 48, () =>
            {
                var report = game.UseStar(input.text);
                CloseModal();
                if (report != null) ShowStarFeedback(report);
            }, Gold, Dark, 15);
            ButtonAt(m.transform, "跳过本题（面试官 +10 防御）", 430, 530, 180, 48, () =>
            {
                game.SkipStar();
                CloseModal();
                Refresh();
            });
        }

        private void ShowStarFeedback(ScoreReport report)
        {
            var m = Modal("STAR 评分结果", "评级：" + report.grade + "　得分：" + report.score, 720, 520);
            Color gradeColor = report.grade == "优秀" ? Teal : report.grade == "合格" ? Gold : Red;
            Label(m.transform, "Grade", report.grade + "  ·  " + report.damage + " 点伤害",
                  40, 120, 640, 60, 26, gradeColor, TextAnchor.MiddleCenter);
            Label(m.transform, "Hit", "命中要点：" + (report.hitTerms.Count > 0 ?
                  string.Join("、", report.hitTerms) : "无"), 50, 200, 620, 36, 13, TextColor);
            Label(m.transform, "Miss", "缺失要点：" + (report.missingTerms.Count > 0 ?
                  string.Join("、", report.missingTerms) : "无"), 50, 246, 620, 36, 13, Muted);
            Label(m.transform, "Tip", "建议：" + report.suggestion, 50, 292, 620, 60, 13, Teal);
            if (report.score < 60)
                Label(m.transform, "Warn", "评分低于 60：面试官获得「加压」，下次造成伤害 ×1.5。", 50, 360, 620, 36, 13, Red);
            ButtonAt(m.transform, "继续面试", 280, 430, 160, 46, CloseModal, Gold, Dark);
        }

        private void ShowCardGallery(int page)
        {
            var all = GameDatabase.Data.cards.OrderBy(c => c.key).ToList();
            int perPage = 12;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(all.Count / (float)perPage));
            page = Mathf.Clamp(page, 0, pageCount - 1);
            var acquired = new System.Collections.Generic.HashSet<string>();
            if (game != null && game.State != null && game.State.masterDeck != null)
                foreach (var c in game.State.masterDeck) acquired.Add(c.key);

            var m = Modal("卡牌图鉴", "当前对局已获得的卡牌以金色高亮 · 已获得 " + acquired.Count + " / " + all.Count,
                          1120, 700);
            for (int i = 0; i < perPage; i++)
            {
                int index = page * perPage + i;
                if (index >= all.Count) break;
                var def = all[index];
                int col = i % 6, row = i / 6;
                float x = 149 + col * 142f, y = 150 + row * 240f;
                bool owned = acquired.Contains(def.key);
                // 图鉴卡面与手牌/奖励牌完全一致的美术 UI
                var card = PanelAt(m.transform, "Gallery_" + def.key, x, y, 112, 200,
                                   def.kind == "attack" ? Hex("#3D2A28") : Hex("#223D3C"));
                PaintCardFace(card.transform, def, def.key, owned);
            }
            Label(m.transform, "Page", "第 " + (page + 1) + " / " + pageCount + " 页",
                  470, 635, 180, 30, 13, Gold, TextAnchor.MiddleCenter);
            ButtonAt(m.transform, "上一页", 220, 632, 130, 40, () => ShowCardGallery(page - 1));
            ButtonAt(m.transform, "下一页", 770, 632, 130, 40, () => ShowCardGallery(page + 1));
            ButtonAt(m.transform, "关闭", 940, 632, 130, 40, CloseModal, Gold, Dark);
        }

        private void ShowTutorial(int step)
        {
            string[] texts = {
                "出牌：双击手牌，或按住拖到面试官区域松手即可打出。",
                "STAR 不强制：可以先出普通牌，也可以先点 STAR 答题。若没点 STAR 就直接结束回合，结果等同于跳过本题：不造成伤害，面试官获得 10 点防御（最多 20 点）。",
                "题意契合：打出的卡与当前问题类型相同，伤害 ×1.5；通用伤害牌首张 +2。",
                "状态：左侧会显示镇定与负面状态；数字键 1–8 快速出牌，Enter 结束回合。"
            };
            var m = Modal("新手引导 " + step + " / 4", texts[step - 1], 760, 340);
            ButtonAt(m.transform, step < 4 ? "下一步" : "开始面试", 300, 220, 160, 46,
                     () => { CloseModal(); if (step < 4) ShowTutorial(step + 1); }, Gold, Dark);
        }

        private void ShowSettings()
        {
            var m = Modal("设置", "窗口、反馈、题库与数据目录", 660, 620);
            ButtonAt(m.transform, "战斗特效：" + (effectsEnabled ? "开启" : "关闭"), 60, 130, 540, 40,
                     () => { effectsEnabled = !effectsEnabled; ShowSettings(); });
            ButtonAt(m.transform, "动效强度：" + Mathf.RoundToInt(effectIntensity * 100) + "%（点击切换）",
                     60, 180, 540, 40, () =>
                     {
                         effectIntensity = effectIntensity >= 1f ? .5f : effectIntensity + .25f;
                         ShowSettings();
                     });
            ButtonAt(m.transform, "窗口模式：" + (Screen.fullScreen ? "全屏" : "窗口化") + "（点击切换）",
                     60, 230, 540, 40, () =>
                     {
                         DisplaySettings.Apply(!Screen.fullScreen, Screen.width, Screen.height);
                         ShowSettings();
                     });
            ButtonAt(m.transform, "分辨率：1240×820", 60, 280, 260, 40,
                     () => { DisplaySettings.Apply(Screen.fullScreen, 1240, 820); ShowSettings(); });
            ButtonAt(m.transform, "分辨率：1600×900", 340, 280, 260, 40,
                     () => { DisplaySettings.Apply(Screen.fullScreen, 1600, 900); ShowSettings(); });
            ButtonAt(m.transform, "分辨率：1920×1080", 60, 330, 260, 40,
                     () => { DisplaySettings.Apply(Screen.fullScreen, 1920, 1080); ShowSettings(); });
            ButtonAt(m.transform, "恢复当前屏幕", 340, 330, 260, 40,
                     () =>
                     {
                         DisplaySettings.Apply(Screen.fullScreen, Screen.currentResolution.width,
                                               Screen.currentResolution.height);
                         ShowSettings();
                     });
            Label(m.transform, "Info", "当前：" + Screen.width + "×" + Screen.height +
                  "    岗位：" + game.Job + "    难度：" + game.Difficulty +
                  "\n存档目录：" + SaveSystem.SaveDir, 60, 390, 540, 55, 11, Muted);
            ButtonAt(m.transform, "关闭", 270, 470, 120, 40, () =>
            {
                CloseModal();
                var back = settingsBack;
                settingsBack = null;
                if (back != null) back();
            }, Gold, Dark);
        }

        private void ShowImport()
        {
            var m = Modal("导入题库", "每行格式：题型,题目；支持 CSV / Markdown 列表。确认后保存到本机。", 760, 520);
            var img = PanelAt(m.transform, "Input", 40, 130, 680, 260, Dark);
            img.raycastTarget = true;
            var input = img.gameObject.AddComponent<TMP_InputField>();
            var t = Label(input.transform, "Text", "", 8, 4, 664, 252, 12, TextColor, TextAnchor.UpperLeft);
            input.textComponent = t;
            input.textViewport = (RectTransform)input.transform;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            ButtonAt(m.transform, "确认导入", 200, 420, 160, 44, () =>
            {
                int added = 0, errors = 0;
                var bank = GameDatabase.Imported ?? (GameDatabase.Imported = new ImportedBank());
                foreach (string raw in input.text.Split('\n'))
                {
                    string line = raw.Trim().TrimStart('-', '•').Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split(new[] { ',', '：' }, 2);
                    if (parts.Length != 2) { errors++; continue; }
                    string type = parts[0].Trim();
                    string text = parts[1].Trim();
                    if (string.IsNullOrEmpty(text) || !GameDatabase.Data.questions.Exists(g => g.type == type))
                    { errors++; continue; }
                    var group = bank.groups.Find(g => g.type == type);
                    if (group == null) { group = new QuestionGroup { type = type }; bank.groups.Add(group); }
                    group.texts.Add(text);
                    added++;
                }
                GameDatabase.SaveImported();
                CloseModal();
                Modal("导入完成", "成功 " + added + " 道，格式错误 " + errors + " 行。", 560, 240);
                ButtonAt(modal.transform, "关闭", 220, 160, 120, 40, CloseModal, Gold, Dark);
            }, Gold, Dark);
            ButtonAt(m.transform, "取消", 400, 420, 160, 44, CloseModal);
        }

        private void ShowStats()
        {
            var stats = StatsSystem.Load();
            var m = Modal("统计", "本地匿名复盘数据（不含自由文本回答）", 720, 520);
            var lines = new List<string>();
            lines.Add("总局数 " + stats.runs.Count + "    通关 " + stats.runs.FindAll(r => r.win).Count);
            if (game.State != null)
                lines.Add("本局：伤害 " + game.State.totalDamage + " · 卡牌 " + game.State.totalCards +
                          " · 回合 " + game.State.totalTurns);
            lines.Add("");
            foreach (var r in stats.runs.GetRange(Mathf.Max(0, stats.runs.Count - 8), Mathf.Min(8, stats.runs.Count)))
                lines.Add(r.time + "  " + r.job + "·" + r.diff + "  " + (r.win ? "通关" : "失败") +
                          "  第" + r.floor + "轮  伤害" + r.damage);
            Label(m.transform, "Info", string.Join("\n", lines), 40, 130, 640, 300, 13, TextColor, TextAnchor.UpperLeft);
            ButtonAt(m.transform, "关闭", 300, 450, 120, 40, CloseModal, Gold, Dark);
        }

    }
}
