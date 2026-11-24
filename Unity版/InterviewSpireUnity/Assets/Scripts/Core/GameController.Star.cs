using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace InterviewSpire
{
    /// <summary>Star 回合行动：本地评分、三档伤害、加压与跳过。</summary>
    public partial class GameController
    {
        public ScoreReport LastStarReport { get; private set; }

        public ScoreReport UseStar(string answer)
        {
            if (State == null || State.phase != 0 || State.starUsedThisTurn) return null;
            State.starUsedThisTurn = true;

            var meta = GameDatabase.Data.question_meta
                .Find(m => m.id == State.currentQuestionId);
            var report = ScoreAnswer(meta, answer ?? "");
            LastStarReport = report;

            Hit(report.damage, "star", true);   // Star 不受自我怀疑影响
            if (report.grade == "不合格")
            {
                State.interviewerPressured = true;
                Log("回答不够充分：面试官获得「加压」，下次造成自信伤害 ×1.5。", "hurt");
            }
            Log("Star 回答评级：" + report.grade + "，造成 " + report.damage + " 点伤害。",
                report.grade == "优秀" ? "good" : report.grade == "合格" ? "" : "hurt");
            Emit();
            CheckStarDefeat();
            return report;
        }

        public bool SkipStar()
        {
            if (State == null || State.phase != 0 || State.starUsedThisTurn) return false;
            State.starUsedThisTurn = true;
            int block = Mathf.Min(20, State.enemy.block + 10);
            State.enemy.block = block;
            Log("跳过 Star：面试官获得 10 点防御（当前 " + block + "）。", "");
            Emit();
            return true;
        }

        private ScoreReport ScoreAnswer(QuestionMeta meta, string answer)
        {
            var report = new ScoreReport();
            if (meta == null || string.IsNullOrWhiteSpace(answer))
            {
                report.score = 0; report.grade = "不合格"; report.damage = 3;
                report.suggestion = "没有作答。至少给出一个具体例子。";
                return report;
            }

            string normalized = Normalize(answer);
            int hits = 0;
            foreach (var kp in meta.keyPoints)
            {
                bool hit = normalized.Contains(Normalize(kp.term)) ||
                           kp.synonyms.Any(s => normalized.Contains(Normalize(s)));
                if (hit)
                {
                    hits++;
                    report.hitTerms.Add(kp.term);
                }
                else report.missingTerms.Add(kp.term);
            }

            int score = Mathf.Min(3, hits) * 20;
            int structure = CountCategories(normalized,
                new[] { "背景", "当时", "情况", "场景" },
                new[] { "任务", "目标", "职责", "需要" },
                new[] { "我负责", "我的动作", "我先", "我推动", "我采取", "行动" },
                new[] { "结果", "最终", "指标", "上线", "提升", "达成" });
            score += structure >= 2 ? 20 : structure == 1 ? 10 : 0;

            int specificity = 0;
            if (Regex.IsMatch(normalized, "\\d")) specificity++;
            if (normalized.Contains("天") || normalized.Contains("周") ||
                normalized.Contains("月") || normalized.Contains("小时") ||
                normalized.Contains("分钟")) specificity++;
            if (Regex.IsMatch(normalized, "[A-Za-z]{2,}") || normalized.Contains("系统") ||
                normalized.Contains("工具") || normalized.Contains("平台")) specificity++;
            score += specificity >= 2 ? 20 : specificity == 1 ? 10 : 0;

            if (answer.Trim().Length < 20) score -= 20;
            score = Mathf.Clamp(score, 0, 100);
            report.score = score;

            if (score >= 80)
            {
                report.grade = "优秀";
                report.damage = 10;
            }
            else if (score >= 60)
            {
                report.grade = "合格";
                report.damage = 5;
            }
            else
            {
                report.grade = "不合格";
                report.damage = 3;
            }

            report.suggestion = report.missingTerms.Count > 0
                ? "补上「" + report.missingTerms[0] + "」会让回答更完整。"
                : structure < 2 ? "按「背景 → 我的动作 → 结果」组织会更清晰。"
                : "已经覆盖主要要点，保持简洁。";
            return report;
        }

        private void CheckStarDefeat() { CheckEnemyDefeat(); }

        private static int CountCategories(string text, params string[][] groups)
        {
            int count = 0;
            foreach (var group in groups)
                if (group.Any(word => text.Contains(word))) count++;
            return count;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s.ToLowerInvariant(), "[\\s，。！？；：、,.!?;:'\"“”‘’（）()\\[\\]]", "");
        }
    }
}
