using System;
using UnityEngine;
using BepInEx.Logging;
using System.Linq;
using System.Collections.Generic;
using Il2CppSystem.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;                          // Il2Cpp 的 BindingFlags/Type

namespace BackToDawnCommPlugin
{
    /// <summary>
    /// 动态脚本类 - 用于F11键动态编译和调用
    /// </summary>
    public class DynamicScript
    {
        /// <summary>
        /// 执行函数 - 这是动态编译后要调用的主要方法
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public static void Execute(ManualLogSource logger)
        {
            logger.LogInfo("=== 动态脚本执行开始 ===");
            logger.LogInfo("当前时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            var regex = new System.Text.RegularExpressions.Regex(@"^\d+_\w+_\w+$");
            var characters = new Dictionary<string, GameObject>();

            WithTimer(() => {
                var layer = LayerMask.NameToLayer("Character");
                var characterLayers = UnityEngine.Object.FindObjectsOfType<GameObject>()
                        .Where(go => go != null && go.activeInHierarchy && go.layer == layer)
                        .ToList();
                foreach (var go in characterLayers)
                {
                    if (regex.IsMatch(go.name))
                    {
                        characters[go.name] = go;
                    }
                }
                foreach (var characterkv in characters)
                {
                    // logger.LogInfo($"Character: {characterkv.Key}");

                    GetCharacterInteraction(characterkv.Value, logger).ForEach(s => logger.LogInfo($"\t交互 [{characterkv.Key}]: {s}"));
                    GetCharacterTalk(characterkv.Value, logger).ForEach(s => logger.LogInfo($"\t聊天 [{characterkv.Key}]: {s}"));
                    var quest = GetCharacterQuest(characterkv.Value, logger);
                    var emoji = GetCharacterEmoji(characterkv.Value, logger);
                    if (quest != null) logger.LogInfo($"\tquest [{characterkv.Key}]: {quest}");
                    if (emoji != null) logger.LogInfo($"\t表情 [{characterkv.Key}]: {emoji}");
                }
            }, logger);
        }
        public static Transform FindByPath(GameObject character, string path) {
            return character.transform.Find(path);
        }
        public static List<string> GetComponentText(Transform root) {
            var res = new List<string>();
            if (root == null) return res;
            var textListItems = root.GetComponentsInChildren<UnityEngine.UI.Text>().Where(item => item.gameObject.activeInHierarchy).Reverse();
            foreach (var item in textListItems) {
                res.Add(item.text);
            }
            return res;
        }
        public static List<string> GetCharacterInteraction(GameObject character, ManualLogSource logger)
        {
            var interactionRoot = FindByPath(character, "Interaction/Root Interaction Character Canvas/Interaction Character Canvas/InteractionList(Clone)");
            return GetComponentText(interactionRoot);
        }

        public static List<string> GetCharacterTalk(GameObject character, ManualLogSource logger)
        {
            var talkRoot = FindByPath(character, "Character Canvas/TalkWord_black(Clone)");
            var text = GetComponentText(talkRoot);
            if (text == null || text.Count == 0) {
                talkRoot = FindByPath(character, "Interaction/Root Interaction Character Canvas/Interaction Character Canvas/TalkWord_black(Clone)/new/wordNew/word/Text");
                text = GetComponentText(talkRoot);
            }
            return text;
        }
        public static void showAllFields(Component o, ManualLogSource logger) {
            foreach (var f in o.GetIl2CppType()
                    .GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly|BindingFlags.Static|BindingFlags.FlattenHierarchy|BindingFlags.GetField|BindingFlags.GetProperty|BindingFlags.IgnoreCase|BindingFlags.Default)
                ) {
                    logger.LogInfo($"FIELD {f.FieldType.Name} {f.Name} = {f.GetValue(o)}");
                }
        }
        public static Il2CppSystem.Object GetMember(Il2CppSystem.Object o, string name) {
            if (o == null) return null;
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            var t = o.GetIl2CppType();
            while (t != null) {
                var p = t.GetProperty(name, BF);
                if (p != null) return p.GetValue(o, null);

                var f = t.GetField(name, BF);
                if (f != null) return f.GetValue(o);

                t = t.BaseType;
            }
            return null;
        }

        public static string GetCharacterQuest(GameObject character, ManualLogSource logger)
        {
            var questRoot = FindByPath(character, "Interaction/Root Interaction Character Canvas/Interaction Character Canvas/OverheadAnimator(Clone)/questAnimation");
            if (!(questRoot?.gameObject.activeInHierarchy ?? false)) return null;
            var widgetAnimator = questRoot?.GetComponent("Widget_Animator");
            var showName = GetMember(widgetAnimator, "showName");

            return showName?.ToString();
        }
        public static string GetCharacterEmoji(GameObject character, ManualLogSource logger) {
            var emojiRoot = FindByPath(character, "Interaction/Root Interaction Character Canvas/Interaction Character Canvas/OverheadAnimator(Clone)/emojiRootPos/emojiRoot/emoji");
            if (!(emojiRoot?.gameObject.activeInHierarchy ?? false)) return null;

                var UIImageAnimator = emojiRoot.GetComponent("UIImageAnimator");
                var currentAnimation = GetMember(UIImageAnimator, "currentAnimation");
                var animationName = GetMember(currentAnimation, "animationName")?.ToString();
                return animationName;
        }

        public static void WithTimer(Action action, ManualLogSource logger)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            try {
                action();
            } catch (Exception e) {
                logger.LogError($"执行失败: {e.Message}");
                logger.LogError($"{e.StackTrace}");
            }
            timer.Stop();
            logger.LogInfo($"\t\t执行时间: {timer.ElapsedMilliseconds}ms");
        }
    }
}
