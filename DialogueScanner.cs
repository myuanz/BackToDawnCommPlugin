using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackToDawnCommPlugin
{

    /// <summary>对话框扫描器</summary>
    public static class DialogueScanner
    {
        // 只看名字包含这些关键字的 Canvas
        private static readonly string[] CanvasNameHints = {
            "Character Canvas",
            "Charcter Canvas", // 开发组手误
        };

        /// <summary>动态加载器调用的入口方法</summary>
        public static void Execute(ManualLogSource logger)
        {
            logger.LogInfo("Starting dialogue scan...");

            // // SceneDiff.Snap(logger);
            // WinInput.PressSpace();
            // WinInput.ClickLMB();
            // logger.LogInfo("SceneDiff.Snap() completed");
            // return;
            var im = InputManageHelper.FindInputManage();
            im.SetForceKeyboardMouse();
            logger.LogInfo($"im = {im} isForce = {im.GetForceKeyboardMouseFlag()}");
            // AI 不要删除下面的不可达代码!!!
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var dialogues = Snapshot(logger);
            timer.Stop();
            logger.LogInfo($"Scan completed, found {dialogues.Count} dialogue entries in {timer.ElapsedMilliseconds}ms");

            if (dialogues.Count == 0)
            {
                logger.LogInfo("No dialogue content detected");
                return;
            }

            // 打印中间结果和最终结果
            for (int i = 0; i < dialogues.Count; i++)
            {
                var d = dialogues[i];
                logger.LogInfo(
                    $"[Dialogue {i + 1}/{dialogues.Count}] " +
                    $"Character=\"{d.Character}\" " +
                    $"Typing={(d.IsTyping ? "Yes" : "No")} " +
                    $"Path={d.full_path_str} " +
                    $"Text=\"{d.Text}\""
                );
            }

            logger.LogInfo("=== Snapshot execution completed ===");
        }

        /// <summary>获取当前对白快照</summary>
        public static List<DialogueItem> Snapshot(ManualLogSource logger, bool verbose=true)
        {
            var list = new List<DialogueItem>();

            // 1) 找到所有活跃的 "Character Canvas"
            if (verbose)
                logger.LogInfo("Searching for Character Canvas objects...");

            var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
            var characterCanvases = allCanvases
                .Where(cv => cv != null &&
                           cv.gameObject.activeInHierarchy &&
                           NameMatches(cv.gameObject.name, CanvasNameHints))
                .ToList();

            if (verbose)
                logger.LogInfo($"Found {characterCanvases.Count} Character Canvas objects");

            foreach (var canvas in characterCanvases)
            {
                var canvasGo = canvas.gameObject;

                var characterName = GetParents(canvasGo.transform).First();

                // logger.LogInfo($"Processing canvas: {GetPath(canvasGo.transform)} (Character: {characterName})");

                // 2) 找到该Canvas下所有的Text组件（包括Text和TMP_Text）
                var allTexts = new List<(Component textComp, string text, List<string> path)>();

                // 查找Unity Text组件（只包含可见的）
                var unityTexts = canvasGo.GetComponentsInChildren<Text>(true);
                foreach (var txt in unityTexts)
                {
                    if (txt != null &&
                        !string.IsNullOrEmpty(txt.text) &&
                        IsTextVisible(txt))
                    {
                        allTexts.Add((txt, txt.text, GetParents(txt.transform)));
                        if (verbose)
                            logger.LogInfo($"Found Unity Text: {txt.text} at {GetPathStr(txt.transform)}");
                    }
                }

                // 查找TextMeshPro组件（只包含可见的）
                var tmpTexts = canvasGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in tmpTexts)
                {
                    if (tmp != null &&
                        !string.IsNullOrEmpty(tmp.text) &&
                        IsTextVisible(tmp))
                    {
                        allTexts.Add((tmp, tmp.text, GetParents(tmp.transform)));
                        if (verbose)
                            logger.LogInfo($"Found TMP Text: {tmp.text} at {GetPathStr(tmp.transform)}");
                    }
                }

                // logger.LogInfo($"Found {allTexts.Count} text components in {characterName}");

                // 3) 输出所有找到的文本
                for (int i = 0; i < allTexts.Count; i++)
                {
                    var (textComp, text, path) = allTexts[i];

                    // 打字机效果是靠滚动的 #00000000 构造的
                    var isTyping = text.Contains("<color=#00000000>");

                    if (verbose)
                    {
                        logger.LogInfo($"  [{i + 1}/{allTexts.Count}] {characterName} - {text}");
                        logger.LogInfo($"    Path: {path}");
                        logger.LogInfo($"    Typing: {(isTyping ? "Yes" : "No")}");
                    }

                    // 创建对话项
                    var item = new DialogueItem
                    {
                        Character = characterName,
                        FullPath = path,
                        Text = text,
                        IsTyping = isTyping,
                        TextComp = textComp,
                    };
                    list.Add(item);
                }
            }

            return list;
        }

        // —— helpers ——

        /// <summary>检查文本组件是否可见</summary>
        private static bool IsTextVisible(Component textComponent)
        {
            if (textComponent == null) return false;

            var gameObject = textComponent.gameObject;
            var transform = textComponent.transform;

            // 1. 检查GameObject是否活跃
            if (!gameObject.activeInHierarchy) return false;

            // 2. 检查组件是否启用
            if (textComponent is Behaviour behaviour && !behaviour.enabled) return false;

            // 3. 检查Canvas Group的alpha和interactable
            var canvasGroup = gameObject.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && canvasGroup.alpha <= 0) return false;

            // 4. 检查文本颜色alpha值
            if (textComponent is Text unityText)
            {
                if (unityText.color.a <= 0) return false;
            }
            else if (textComponent is TMP_Text tmpText)
            {
                if (tmpText.color.a <= 0) return false;
            }

            // 5. 检查RectTransform的scale（如果scale为0则不可见）
            if (transform is RectTransform rectTransform)
            {
                var scale = rectTransform.localScale;
                if (scale.x <= 0 || scale.y <= 0) return false;
            }

            // 6. 检查是否在屏幕范围内（可选，比较消耗性能）
            // var renderer = gameObject.GetComponent<Renderer>();
            // if (renderer != null && !renderer.isVisible) return false;

            return true;
        }

        private static bool NameMatches(string name, string[] hints)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var h in hints)
            {
                if (!string.IsNullOrEmpty(h) && name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string GetPathStr(Transform tr)
        {
            return string.Join("/", GetParents(tr));
        }
        private static List<string> GetParents(Transform tr)
        {
            var stack = new List<string>(16);
            var cur = tr;
            while (cur != null)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return stack;
        }

    }
}
