using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackToDawnCommPlugin
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Back To The Dawn.exe")]
    public class Plugin : BasePlugin
    {
        internal static new ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            // 关键：在场景里挂一个扫描器（BepInEx v6 IL2CPP 支持）
            AddComponent<DialogueScanner>();
        }
    }

    /// <summary>一次快照里的对白条目</summary>
    public class DialogueItem
    {
        public string Character;      // 角色名（即 "Character Canvas" 的父节点名）
        public string BubbleType;     // 气泡类型，如 TalkWord_black(Clone)
        public string FullPath;       // 完整层级路径
        public string Text;           // 文本
        public bool   IsTyping;       // 是否在“打字机”显示中
        public TMP_Text TextComp;     // 原组件引用（可后续操作）
        public GameObject Root;       // 气泡的根（TalkWord_*）
    }

    /// <summary>对白扫描器：定期在所有 "Character Canvas" 下收集对白文本</summary>
    public class DialogueScanner : MonoBehaviour
    {
        // 只看名字包含这些关键字的 Canvas（你也可以收紧为等于 "Character Canvas"）
        private static readonly string[] CanvasNameHints = { "Character Canvas" };

        // Talk 气泡的根名关键字（前缀匹配更鲁棒，避免只锁 "TalkWord_black"）
        private static readonly string[] TalkRootHints = { "TalkWord" };

        // 文本常见相对路径的优先解（不同状态尾部略有变化，这里先给几条常见路由；找不到就 fallback 为任意 TMP_Text）
        private static readonly string[][] CandidateTextRelPaths = new[]
        {
            new[] { "new", "wordNew", "word" },
            new[] { "new", "word" },
            new[] { "wordNew", "word" },
            new[] { "word" }
        };



        // 公开方法：可从其他代码调用，获取当前对白快照
        public static List<DialogueItem> Snapshot()
        {
            var list = new List<DialogueItem>();

            // 1) 找到所有 "Character Canvas"
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>()
                .Where(cv => cv && cv.gameObject.activeInHierarchy
                             && NameMatches(cv.gameObject.name, CanvasNameHints));

            foreach (var cv in canvases)
            {
                var canvasGo = cv.gameObject;
                // 角色名：一般就是 Canvas 的父节点名（<角色名>/Character Canvas）
                var characterName = canvasGo.transform.parent ? canvasGo.transform.parent.name : canvasGo.name;

                // 2) 在该 Canvas 子树中查找所有 TalkWord_* 作为“气泡根”
                var talkRoots = canvasGo.GetComponentsInChildren<Transform>(true)
                    .Where(t => t && t.gameObject.activeInHierarchy
                                && NameStartsWithAny(t.gameObject.name, TalkRootHints))
                    .Select(t => t.gameObject);

                foreach (var root in talkRoots)
                {
                    // 3) 在气泡根下定位文本节点
                    var tmp = FindTextUnderTalkRoot(root.transform);
                    if (tmp == null || string.IsNullOrEmpty(tmp.text))
                        continue;

                    // 4) 组装条目
                    var item = new DialogueItem
                    {
                        Character = characterName,
                        BubbleType = root.name,
                        FullPath = GetPath(tmp.transform),
                        Text = tmp.text,
                        IsTyping = IsTyping(tmp),
                        TextComp = tmp,
                        Root = root
                    };
                    list.Add(item);
                }
            }
            return list;
        }

        void Update()
        {
            // 检测F8键按下（使用GetKeyDown避免重复触发）
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Plugin.Log.LogInfo("=== F8 Key Pressed - Taking Dialogue Snapshot ===");
                ExecuteSnapshot();
            }
        }

        private void ExecuteSnapshot()
        {
            Plugin.Log.LogInfo("Starting dialogue scan...");
            
            var dialogues = Snapshot();
            
            Plugin.Log.LogInfo($"Scan completed, found {dialogues.Count} dialogue entries");
            
            if (dialogues.Count == 0)
            {
                Plugin.Log.LogInfo("No dialogue content detected");
                return;
            }

            // 打印中间结果和最终结果
            for (int i = 0; i < dialogues.Count; i++)
            {
                var d = dialogues[i];
                Plugin.Log.LogInfo(
                    $"[Dialogue {i + 1}/{dialogues.Count}] " +
                    $"Character=\"{d.Character}\" " +
                    $"BubbleType=\"{d.BubbleType}\" " +
                    $"Typing={(d.IsTyping ? "Yes" : "No")} " +
                    $"Path={d.FullPath} " +
                    $"Text=\"{Short(d.Text, 100)}\""
                );
            }
            
            Plugin.Log.LogInfo("=== Snapshot execution completed ===");
        }

        // —— helpers ——

        private static TMP_Text FindTextUnderTalkRoot(Transform talkRoot)
        {
            // 先尝试若干常见的相对路径
            foreach (var segs in CandidateTextRelPaths)
            {
                var tr = FindBySegments(talkRoot, segs);
                if (tr)
                {
                    var tmp = tr.GetComponent<TMP_Text>();
                    if (tmp != null) return tmp;
                }
            }
            // fallback：在该子树里找第一个 TMP_Text
            return talkRoot.GetComponentInChildren<TMP_Text>(true);
        }

        private static Transform FindBySegments(Transform start, string[] segs)
        {
            var cur = start;
            foreach (var seg in segs)
            {
                cur = cur.Find(seg);
                if (!cur) return null;
            }
            return cur;
        }

        private static bool NameMatches(string name, string[] hints)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var h in hints)
                if (!string.IsNullOrEmpty(h) && name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
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
