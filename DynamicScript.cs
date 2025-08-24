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
                var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                var characterLayers = allGameObjects
                        .Where(go => go != null && go.activeInHierarchy && go.layer == layer)
                        .ToList();
                foreach (var go in characterLayers)
                {
                    if (regex.IsMatch(go.name))
                    {
                        characters[go.name] = go;
                    }
                }
            }, logger);
            logger.LogInfo($"场景中CharacterLayer数量: {characters.Count}");

            WithTimer(() => {
                foreach (var characterkv in characters)
                {
                    // logger.LogInfo($"Character: {characterkv.Key}");
                    GetCharacterInteraction(characterkv.Value, logger);
                    GetCharacterTalk(characterkv.Value, logger);
                    GetCharacterQuest(characterkv.Value, logger);
                    GetCharacterEmoji(characterkv.Value, logger);
                }
            }, logger);
        }

        public static List<string> GetCharacterInteraction(GameObject character, ManualLogSource logger)
        {
            var res = new List<string>();
            var interactionRoot = character.transform.Find("Interaction/Root Interaction Character Canvas/Interaction Character Canvas/InteractionList(Clone)");
            if (interactionRoot != null) {
                logger.LogInfo($"Root InteractionList: {interactionRoot.name}");
                
                var interactionListItems = interactionRoot.GetComponentsInChildren<UnityEngine.UI.Text>().Where(item => item.gameObject.activeInHierarchy).Reverse();
                foreach (var item in interactionListItems)
                {
                    res.Add(item.text);
                    logger.LogInfo($"\t交互 [{character.name}]: {item.text}");
                }
            } else {
                // logger.LogInfo($"Root InteractionList: not found");
            }

            return res;
        }

        public static List<string> GetCharacterTalk(GameObject character, ManualLogSource logger)
        {
            var res = new List<string>();
            var talkRoot = character.transform.Find("Character Canvas/TalkWord_black(Clone)");
            if (talkRoot != null) {
                logger.LogInfo($"Root Talk: {talkRoot.name}");
                
                var talkListItems = talkRoot.GetComponentsInChildren<UnityEngine.UI.Text>().Where(item => item.gameObject.activeInHierarchy).Reverse();
                foreach (var item in talkListItems)
                {
                    res.Add(item.text);
                    logger.LogInfo($"\t聊天 [{character.name}]: {item.text}");
                }
            } else {
                // logger.LogInfo($"Root Talk: not found");
            }
            return res;
        }
        public static void showAllFields(Component o, ManualLogSource logger) {
            foreach (var f in o.GetIl2CppType()
                    .GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly|BindingFlags.Static|BindingFlags.FlattenHierarchy|BindingFlags.GetField|BindingFlags.GetProperty|BindingFlags.IgnoreCase|BindingFlags.Default)
                ) {
                    logger.LogInfo($"FIELD {f.FieldType.Name} {f.Name} = {f.GetValue(o)}");
                }
        }
        public static object GetMember(Component o, string name) {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            var t = o.GetIl2CppType();
            while (t != null) {
                var p = t.GetProperty(name, BF);
                if (p != null) return p.GetValue(o, null);

                var f = t.GetField(name, BF);
                if (f != null) return f.GetValue(o);

                // auto-property 的后备字段：<showName>k__BackingField
                f = t.GetField($"<{name}>k__BackingField", BF);
                if (f != null) return f.GetValue(o);

                t = t.BaseType;
            }
            return null;
        }

        public static string GetCharacterQuest(GameObject character, ManualLogSource logger)
        {
            var questRoot = character.transform.Find("Interaction/Root Interaction Character Canvas/Interaction Character Canvas/OverheadAnimator(Clone)/questAnimation");
            if (questRoot != null && questRoot.gameObject.activeInHierarchy) {
                logger.LogInfo($"Root Quest: {character.name} {questRoot.name}");
                // 路径: 11_驴子_山姆/Interaction/Root Interaction Character Canvas/Interaction Character Canvas/OverheadAnimator(Clone)/questAnimation
                // 组件名称: Widget_Animator 
                // 内容: Widget_Animator.showName = "question"

                var try_c = questRoot.GetComponent("Widget_Animator");
                if (try_c != null) {
                    // logger.LogInfo($"\t\tQuest by name: {try_c.gameObject.name} {try_c.GetType().Name}");

                    var t = try_c.GetIl2CppType();
                    var showNameProperty = t.GetField("showName");
                    var showName = showNameProperty.GetValue(try_c).ToString();

                    if (showNameProperty != null)
                    {
                        logger.LogInfo($"\t\t\tShowName: {showName}");
                    } else {
                        logger.LogInfo($"\t\t\tShowName: not found");
                    }
                    return showName;
                } else {
                    logger.LogInfo($"\t\tQuest by name: not found");
                }
            } else {
                // logger.LogInfo($"Root Quest: not found");
            }
            return null;
        }
        public static string GetCharacterEmoji(GameObject character, ManualLogSource logger) {
            var emojiRoot = character.transform.Find("Interaction/Root Interaction Character Canvas/Interaction Character Canvas/OverheadAnimator(Clone)/emojiRootPos/emojiRoot/emoji");
            if (emojiRoot != null && emojiRoot.gameObject.activeInHierarchy) {
                logger.LogInfo($"Root Emoji: {character.name} {emojiRoot.name}");
                // get UIImageAnimator.currentAnimation.animationName
                var try_c = emojiRoot.GetComponent("UIImageAnimator");
                // logger.LogInfo($"\t\t\tEmoji by name: {try_c.gameObject.name} {try_c.GetType().Name}");
                // showAllFields(try_c)
                if (try_c != null) {
                    var t = try_c.GetIl2CppType();
                    // showAllFields(try_c, logger);
                    var currentAnimationProperty = t.GetField("currentAnimation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var currentAnimation = currentAnimationProperty.GetValue(try_c);
                    var animationName = currentAnimation.GetIl2CppType().GetField("animationName").GetValue(currentAnimation).ToString();
                    logger.LogInfo($"\t\t\tcurrentAnimation: {animationName}");
                    return animationName;
                } else {
                    logger.LogInfo($"\t\tEmoji by name: not found");
                }
            }
            return null;
        }

        public static void WithTimer(Action action, ManualLogSource logger)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            action();
            timer.Stop();
            logger.LogInfo($"\t\t执行时间: {timer.ElapsedMilliseconds}ms");
        }
    }


}
