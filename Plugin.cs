using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using HarmonyLib;
using UnityEngine.EventSystems;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using BepInEx.Configuration;

namespace BackToDawnCommPlugin
{
    
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Back To The Dawn.exe")]
    public class Plugin : BasePlugin
    {
        internal static new ManualLogSource Log;

        /// <summary>
        /// 轮询间隔, 单位为 秒, 默认 0.5s
        /// </summary>
        internal static ConfigEntry<float> ScanInterval;
        public override void Load()
        {
            Log = base.Log;
            ScanInterval = Config.Bind("Settings", "ScanInterval", 0.5f, "轮询间隔（秒）");
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            AddComponent<DialogueManager>();
        }
    }

    /// <summary>对话管理器：监听按键，管理对话扫描和处理</summary>
    public class DialogueManager : MonoBehaviour
    {        
        // 轮询相关字段
        private bool _isPolling = false;
        private float _lastPollTime = 0f;
        
        // 对话历史记录
        private List<string> _talkHistory = [];
        

        void Start()
        {
            // 设置控制台编码为UTF-8（全局设置）
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Plugin.Log.LogInfo("Console encoding set to UTF-8");

        }

        void Update()
        {
            // 检测F8键按下 - 一次性扫描
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Plugin.Log.LogInfo("=== F8 Key Pressed - Executing Force Keyboard Mouse ===");

                var inputManage = InputManageHelper.FindInputManage();
                inputManage.SetForceKeyboardMouse();
                Plugin.Log.LogInfo($"InputManage set to force keyboard mouse, res: {inputManage.GetForceKeyboardMouseFlag()}");
            }
            
            // 检测F9键按下 - 开始轮询
            if (Input.GetKeyDown(KeyCode.F9))
            {
                StartPolling();
            }
            
            // 检测F10键按下 - 停止轮询
            if (Input.GetKeyDown(KeyCode.F10))
            {
                StopPolling();
            }
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Plugin.Log.LogInfo("=== F11 Key Pressed - Dynamic Script Execution ===");
                ExecuteDynamicScript();
            }
            
            // 执行轮询逻辑
            if (_isPolling)
            {
                UpdatePolling();
            }
        }

        private static void ExecuteScanner()
        {
            try
            {
                Plugin.Log.LogInfo("Executing scanner...");
                var r = DialogueScanner.CollectCharacterInfo(Plugin.Log);
                foreach (var item in r)
                {
                    Plugin.Log.LogInfo($"{item.Key} {item.Value.talks.Count} {item.Value.interactions.Count} {item.Value.quest} {item.Value.emoji}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to execute scanner: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void ExecuteDynamicScript()
        {
            try
            {
                Plugin.Log.LogInfo("开始动态脚本执行...");
                DynamicScript.Execute(Plugin.Log);
                Plugin.Log.LogInfo("动态脚本执行完成");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"动态脚本执行失败: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void StartPolling()
        {
            if (_isPolling)
            {
                Plugin.Log.LogInfo("轮询已在运行中");
                return;
            }
            
            _isPolling = true;
            _lastPollTime = Time.time;
            _talkHistory.Clear();
            
            Plugin.Log.LogInfo("=== F9 按下 - 开始对话轮询 ===");
        }

        private void StopPolling()
        {
            if (!_isPolling)
            {
                Plugin.Log.LogInfo("轮询未在运行");
                return;
            }
            
            _isPolling = false;
            
            Plugin.Log.LogInfo("=== F10 按下 - 停止对话轮询 ===");
        }

        private void UpdatePolling()
        {
            // 检查是否到了轮询时间
            if (Time.time - _lastPollTime < Plugin.ScanInterval.Value)
                return;
                
            _lastPollTime = Time.time;
            
            try
            {
                // 获取当前对话
                var characterInfos = DynamicScript.CollectCharacterInfo(Plugin.Log);
                Plugin.Log.LogInfo($"获取到 {characterInfos.Count} 条对话");
                // 处理对话逻辑
                ProcessDialogue(characterInfos);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"轮询过程中发生错误: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void ProcessDialogue(Dictionary<string, CharacterInfo> characterInfos)
        {
            foreach (var characterInfo in characterInfos)
            {
                Plugin.Log.LogInfo($"处理对话: {characterInfo.Key} {characterInfo.Value.talks.Count}");
                if (characterInfo.Value.WaitingInteraction) {
                    Plugin.Log.LogInfo("等待交互");
                    return;
                }
                if (characterInfo.Value.IsTyping) {
                    Plugin.Log.LogInfo("检测到打字机效果，按鼠标左键继续");
                    WinInput.ClickLMB();
                    return;
                }
                if (characterInfo.Value.CanContinue) {
                    Plugin.Log.LogInfo("可继续，按鼠标左键继续");
                    WinInput.ClickLMB();
                    return;
                }
            }
        }
    }
}
