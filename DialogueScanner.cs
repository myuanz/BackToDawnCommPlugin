using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

static class WinInput
{
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public INPUTUNION U; }
    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const ushort VK_SPACE = 0x20;
    const ushort VK_RETURN = 0x0D; // 回车键
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void PressSpace()
    {
        var a = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_SPACE } } };
        var b = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_SPACE, dwFlags = KEYEVENTF_KEYUP } } };
        SendInput(2, new[] { a, b }, Marshal.SizeOf<INPUT>());
    }
    public static void PressEnter()
    {
        var a = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_RETURN } } };
        var b = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_RETURN, dwFlags = KEYEVENTF_KEYUP } } };
        SendInput(2, new[] { a, b }, Marshal.SizeOf<INPUT>());
    }
    
    public static void ClickLMB()
    {
        var a = new INPUT { type = INPUT_MOUSE, U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } } };
        var b = new INPUT { type = INPUT_MOUSE, U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } } };
        SendInput(2, new[] { a, b }, Marshal.SizeOf<INPUT>());
    }
}

namespace BackToDawnCommPlugin.Scanner
{


    /// <summary>场景对象信息</summary>
    [System.Serializable]
    public class SceneObjectInfo
    {
        public string Path { get; set; }           // 对象路径
        public string Name { get; set; }           // 对象名称
        public bool IsActive { get; set; }         // 是否激活
        public string TextContent { get; set; }   // 文本内容（如果有）
        public string ComponentType { get; set; } // 组件类型
        public Vector3 Position { get; set; }     // 位置
        public Vector3 Scale { get; set; }        // 缩放
    }



    public static class SceneDiff
    {
        private static readonly string SnapshotFilePath = Path.Combine(Path.GetTempPath(), "BackToDawn_SceneSnapshot.json");

        public static void Snap(ManualLogSource logger)
        {
            logger.LogInfo("开始执行场景快照...");

            var currentSnapshot = CaptureSceneSnapshot(logger);

            // 尝试加载上一次的快照
            var lastSnapshot = LoadLastSnapshot(logger);

            if (lastSnapshot == null || lastSnapshot.Count == 0)
            {
                // 第一次执行，只保存快照
                SaveSnapshot(currentSnapshot, logger);
                logger.LogInfo($"首次快照完成，捕获了 {currentSnapshot.Count} 个对象");
            }
            else
            {
                // 第二次及以后执行，进行对比
                CompareSnapshots(lastSnapshot, currentSnapshot, logger);
                SaveSnapshot(currentSnapshot, logger);
            }
        }

        /// <summary>保存快照到临时文件</summary>
        private static void SaveSnapshot(Dictionary<string, SceneObjectInfo> snapshot, ManualLogSource logger)
        {
            try
            {
                var lines = new List<string>();
                lines.Add($"SNAPSHOT_COUNT:{snapshot.Count}");

                foreach (var kvp in snapshot)
                {
                    var obj = kvp.Value;
                    var line = $"{obj.Path}|{obj.Name}|{obj.IsActive}|{obj.TextContent}|{obj.ComponentType}|{obj.Position.x},{obj.Position.y},{obj.Position.z}|{obj.Scale.x},{obj.Scale.y},{obj.Scale.z}";
                    lines.Add(line);
                }

                File.WriteAllLines(SnapshotFilePath, lines);
                logger.LogInfo($"快照已保存到: {SnapshotFilePath}");
            }
            catch (Exception ex)
            {
                logger.LogError($"保存快照失败: {ex.Message}");
            }
        }

        /// <summary>从临时文件加载上一次的快照</summary>
        private static Dictionary<string, SceneObjectInfo> LoadLastSnapshot(ManualLogSource logger)
        {
            try
            {
                if (!File.Exists(SnapshotFilePath))
                {
                    logger.LogInfo("未找到上一次的快照文件");
                    return null;
                }

                var lines = File.ReadAllLines(SnapshotFilePath);
                if (lines.Length == 0)
                {
                    logger.LogWarning("快照文件为空");
                    return null;
                }

                var snapshot = new Dictionary<string, SceneObjectInfo>();

                for (int i = 1; i < lines.Length; i++) // 跳过第一行的计数
                {
                    var parts = lines[i].Split('|');
                    if (parts.Length >= 7)
                    {
                        var positionParts = parts[5].Split(',');
                        var scaleParts = parts[6].Split(',');

                        var obj = new SceneObjectInfo
                        {
                            Path = parts[0],
                            Name = parts[1],
                            IsActive = bool.Parse(parts[2]),
                            TextContent = parts[3],
                            ComponentType = parts[4],
                            Position = new Vector3(
                                float.Parse(positionParts[0]),
                                float.Parse(positionParts[1]),
                                float.Parse(positionParts[2])
                            ),
                            Scale = new Vector3(
                                float.Parse(scaleParts[0]),
                                float.Parse(scaleParts[1]),
                                float.Parse(scaleParts[2])
                            )
                        };

                        snapshot[obj.Path] = obj;
                    }
                }

                logger.LogInfo($"已加载上一次快照，包含 {snapshot.Count} 个对象");
                return snapshot;
            }
            catch (Exception ex)
            {
                logger.LogError($"加载快照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>捕获当前场景的所有对象快照</summary>
        private static Dictionary<string, SceneObjectInfo> CaptureSceneSnapshot(ManualLogSource logger)
        {
            var snapshot = new Dictionary<string, SceneObjectInfo>();

            // 遍历所有已加载的场景
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                logger.LogInfo($"扫描场景: {scene.name}");

                // 获取场景中的所有根对象
                var rootObjects = scene.GetRootGameObjects();

                foreach (var rootObj in rootObjects)
                {
                    CaptureGameObjectRecursive(rootObj, snapshot, "");
                }
            }

            logger.LogInfo($"场景快照完成，总共捕获 {snapshot.Count} 个对象");
            return snapshot;
        }

        /// <summary>递归捕获GameObject及其子对象</summary>
        private static void CaptureGameObjectRecursive(GameObject obj, Dictionary<string, SceneObjectInfo> snapshot, string parentPath)
        {
            if (obj == null) return;

            var currentPath = string.IsNullOrEmpty(parentPath) ? obj.name : $"{parentPath}/{obj.name}";

            // 获取文本内容
            string textContent = "";
            string componentType = "";

            // 检查Unity Text组件
            var unityText = obj.GetComponent<Text>();
            if (unityText != null)
            {
                textContent = unityText.text ?? "";
                componentType = "UnityText";
            }

            // 检查TextMeshPro组件
            var tmpText = obj.GetComponent<TMP_Text>();
            if (tmpText != null)
            {
                textContent = tmpText.text ?? "";
                componentType = "TMPText";
            }

            // 创建对象信息
            var objInfo = new SceneObjectInfo
            {
                Path = currentPath,
                Name = obj.name,
                IsActive = obj.activeInHierarchy,
                TextContent = textContent,
                ComponentType = componentType,
                Position = obj.transform.position,
                Scale = obj.transform.localScale
            };

            snapshot[currentPath] = objInfo;

            // 递归处理子对象
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                CaptureGameObjectRecursive(child, snapshot, currentPath);
            }
        }

        /// <summary>对比两次快照并输出差异</summary>
        private static void CompareSnapshots(Dictionary<string, SceneObjectInfo> oldSnapshot, Dictionary<string, SceneObjectInfo> newSnapshot, ManualLogSource logger)
        {
            logger.LogInfo("=== 开始对比场景变化 ===");

            int changedCount = 0;
            int newCount = 0;
            int removedCount = 0;

            // 检查新增和变化的对象
            foreach (var kvp in newSnapshot)
            {
                var path = kvp.Key;
                var newObj = kvp.Value;

                if (!oldSnapshot.ContainsKey(path))
                {
                    // 新增的对象
                    logger.LogInfo($"[新增] {path}");
                    if (!string.IsNullOrEmpty(newObj.TextContent))
                    {
                        logger.LogInfo($"  文本内容: \"{newObj.TextContent}\"");
                    }
                    logger.LogInfo($"  激活状态: {newObj.IsActive}");
                    newCount++;
                }
                else
                {
                    // 检查变化的对象
                    var oldObj = oldSnapshot[path];
                    var changes = new List<string>();

                    // 检查激活状态变化
                    if (oldObj.IsActive != newObj.IsActive)
                    {
                        changes.Add($"激活状态: {oldObj.IsActive} → {newObj.IsActive}");
                    }

                    // 检查文本内容变化
                    if (oldObj.TextContent != newObj.TextContent)
                    {
                        changes.Add($"文本内容: \"{oldObj.TextContent}\" → \"{newObj.TextContent}\"");
                    }


                    // 只有当有非位置变化时才输出
                    if (changes.Count > 0)
                    {
                        logger.LogInfo($"[变化] {path}");
                        foreach (var change in changes)
                        {
                            logger.LogInfo($"  {change}");
                        }
                        changedCount++;
                    }
                }
            }

            // 检查消失的对象
            foreach (var kvp in oldSnapshot)
            {
                var path = kvp.Key;
                var oldObj = kvp.Value;

                if (!newSnapshot.ContainsKey(path))
                {
                    logger.LogInfo($"[消失] {path}");
                    if (!string.IsNullOrEmpty(oldObj.TextContent))
                    {
                        logger.LogInfo($"  之前的文本内容: \"{oldObj.TextContent}\"");
                    }
                    logger.LogInfo($"  之前的激活状态: {oldObj.IsActive}");
                    removedCount++;
                }
            }

            logger.LogInfo($"=== 对比完成 ===");
            logger.LogInfo($"新增对象: {newCount} 个");
            logger.LogInfo($"变化对象: {changedCount} 个");
            logger.LogInfo($"消失对象: {removedCount} 个");
            logger.LogInfo($"总计差异: {newCount + changedCount + removedCount} 个");
        }
    }

    static class InputManageHelper
    {
        public static MonoBehaviour FindInputManage()
        {
            var gm = GameObject.Find("GameManage");
            var im = gm?.GetComponents<MonoBehaviour>().FirstOrDefault(
                m => m && m.GetType().Name == "InputManage"
            );
            return im;
        }
    }

    public static class MonoBehaviourExtensions
    {
        /// <summary>设置强制键盘鼠标模式</summary>
        public static void SetForceKeyboardMouse(this MonoBehaviour self)
        {
            var tp = self.GetType();
            var mth = tp.GetMethod("SetForceCanUseKeyboardMouse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            mth?.Invoke(self, []);
        }

        /// <summary>获取强制键盘鼠标标志</summary>
        public static bool? GetForceKeyboardMouseFlag(this MonoBehaviour self)
        {
            var tp = self.GetType();
            var prop = tp.GetProperty("isForceCanUseKeyboardMouse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return (bool?)prop?.GetValue(self, null);
        }
    }
    /// <summary>一次快照里的对白条目</summary>
    public class DialogueItem
    {
        public string Character;    // 角色名
        public List<string> FullPath;     // 完整层级路径
        public string Text;         // 文本
        public bool IsTyping;       // 是否在"打字机"显示中
        public object TextComp;   // 原组件引用（可后续操作）

        public string full_path_str => string.Join("/", FullPath);
    }

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

                    logger.LogInfo($"  [{i + 1}/{allTexts.Count}] {characterName} - {text}");
                    logger.LogInfo($"    Path: {path}");
                    logger.LogInfo($"    Typing: {(isTyping ? "Yes" : "No")}");

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

        private static string Short(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
