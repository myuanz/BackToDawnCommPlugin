using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackToDawnCommPlugin.Scanner
{
    /// <summary>一次快照里的对白条目</summary>
    public class DialogueItem
    {
        public string Character;      // 角色名（即 "Character Canvas" 的父节点名）
        public string BubbleType;     // 气泡类型，如 TalkWord_black(Clone)
        public string FullPath;       // 完整层级路径
        public string Text;           // 文本
        public bool IsTyping;       // 是否在"打字机"显示中
        public TMP_Text TextComp;     // 原组件引用（可后续操作）
        public GameObject Root;       // 气泡的根（TalkWord_*）
    }

    /// <summary>对话框扫描器</summary>
    public static class DialogueScanner
    {
        // 只看名字包含这些关键字的 Canvas
        private static readonly string[] CanvasNameHints = { 
            "Character Canvas", 
            "Charcter Canvas", // 开发组手误
        };

        // Talk 气泡的根名关键字（前缀匹配更鲁棒）
        private static readonly string[] TalkRootHints = { "TalkWord" };

        /// <summary>动态加载器调用的入口方法</summary>
        public static void Execute(ManualLogSource logger)
        {
            logger.LogInfo("Starting dialogue scan...");

            var dialogues = Snapshot(logger);

            logger.LogInfo($"Scan completed, found {dialogues.Count} dialogue entries");

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
                    $"BubbleType=\"{d.BubbleType}\" " +
                    $"Typing={(d.IsTyping ? "Yes" : "No")} " +
                    $"Path={d.FullPath} " +
                    $"Text=\"{Short(d.Text, 100)}\""
                );
            }

            logger.LogInfo("=== Snapshot execution completed ===");
        }

        /// <summary>获取当前对白快照</summary>
        public static List<DialogueItem> Snapshot(ManualLogSource logger)
        {
            var list = new List<DialogueItem>();

            // 1) 找到所有活跃的 "Character Canvas"
            logger.LogInfo("Searching for Character Canvas objects...");
            
            var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
            var characterCanvases = allCanvases
                .Where(cv => cv != null && 
                           cv.gameObject.activeInHierarchy &&
                           NameMatches(cv.gameObject.name, CanvasNameHints))
                .ToList();

            logger.LogInfo($"Found {characterCanvases.Count} Character Canvas objects");

            foreach (var canvas in characterCanvases)
            {
                var canvasGo = canvas.gameObject;
                // 角色名：一般就是 Canvas 的父节点名（<角色名>/Character Canvas）
                var characterName = canvasGo.transform.parent ? 
                                  canvasGo.transform.parent.name : 
                                  canvasGo.name;

                // logger.LogInfo($"Processing canvas: {GetPath(canvasGo.transform)} (Character: {characterName})");

                // 2) 找到该Canvas下所有的Text组件（包括Text和TMP_Text）
                var allTexts = new List<(Component textComp, string text, string path)>();
                
                // 查找Unity Text组件（只包含可见的）
                var unityTexts = canvasGo.GetComponentsInChildren<Text>(true);
                foreach (var txt in unityTexts)
                {
                    if (txt != null && 
                        !string.IsNullOrEmpty(txt.text) && 
                        IsTextVisible(txt))
                    {
                        allTexts.Add((txt, txt.text, GetPath(txt.transform)));
                        logger.LogInfo($"Found Unity Text: {txt.text} at {GetPath(txt.transform)}");
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
                        allTexts.Add((tmp, tmp.text, GetPath(tmp.transform)));
                        logger.LogInfo($"Found TMP Text: {tmp.text} at {GetPath(tmp.transform)}");
                    }
                }

                // logger.LogInfo($"Found {allTexts.Count} text components in {characterName}");

                // 3) 输出所有找到的文本
                for (int i = 0; i < allTexts.Count; i++)
                {
                    var (textComp, text, path) = allTexts[i];
                    
                    // 判断是否是对话气泡（TalkWord开头的父节点）
                    var bubbleRoot = FindTalkWordParent(textComp.transform);
                    var bubbleType = bubbleRoot?.name ?? "Unknown";
                    var isTyping = textComp is TMP_Text tmp ? IsTyping(tmp) : false;

                    logger.LogInfo($"  [{i + 1}/{allTexts.Count}] {bubbleType} - {text}");
                    logger.LogInfo($"    Path: {path}");
                    logger.LogInfo($"    Typing: {(isTyping ? "Yes" : "No")}");

                    // 创建对话项
                    var item = new DialogueItem
                    {
                        Character = characterName,
                        BubbleType = bubbleType,
                        FullPath = path,
                        Text = text,
                        IsTyping = isTyping,
                        TextComp = textComp as TMP_Text,
                        Root = bubbleRoot
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

        /// <summary>向上查找TalkWord开头的父节点</summary>
        private static GameObject FindTalkWordParent(Transform transform)
        {
            var current = transform;
            while (current != null)
            {
                if (NameStartsWithAny(current.name, TalkRootHints))
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return null;
        }


        private static bool NameMatches(string name, string[] hints)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var h in hints){
                if (!string.IsNullOrEmpty(h) && name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool NameStartsWithAny(string name, string[] prefixes)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var p in prefixes)
                if (!string.IsNullOrEmpty(p) && name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsTyping(TMP_Text t)
        {
            int total = 0;
            try { total = t.textInfo?.characterCount ?? t.text?.Length ?? 0; }
            catch { total = t.text?.Length ?? 0; }
            return total > 0 && t.maxVisibleCharacters < total;
        }

        private static string GetPath(Transform tr)
        {
            var stack = new List<string>(16);
            var cur = tr;
            while (cur != null)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }

        private static string Short(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
