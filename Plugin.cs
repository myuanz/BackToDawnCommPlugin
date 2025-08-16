using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

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
            // 添加动态加载器组件
            AddComponent<DynamicLoader>();
        }
    }



    /// <summary>动态加载器：监听F8键，动态加载并执行外部程序集</summary>
    public class DynamicLoader : MonoBehaviour
    {
        private string _dllPath;
        private Assembly _currentAssembly;



        void Start()
        {
            // 动态程序集路径（从本地构建目录加载，避免文件占用）
            // 假设开发目录结构：游戏安装在 C:\Program Files (x86)\Steam\steamapps\common\MetalHeadGames
            // 开发目录在 C:\Users\{用户}\projects\BackToDawnCommPlugin
            var userName = Environment.UserName;
            var devPath = $@"C:\Users\{userName}\projects\BackToDawnCommPlugin\bin\Debug\net6.0\BackToDawnCommPlugin.Scanner.dll";
            
            // 如果开发路径存在就用开发路径，否则回退到插件目录（用于发布版本）
            if (File.Exists(devPath))
            {
                _dllPath = devPath;
                Plugin.Log.LogInfo($"Using dev scanner path: {_dllPath}");
            }
            else
            {
                var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _dllPath = Path.Combine(pluginDir, "BackToDawnCommPlugin.Scanner.dll");
                Plugin.Log.LogInfo($"Using release scanner path: {_dllPath}");
            }
        }

        void Update()
        {
            // 检测F8键按下
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Plugin.Log.LogInfo("=== F8 Key Pressed - Loading Dynamic Scanner ===");
                LoadAndExecuteScanner();
            }
        }

        private void LoadAndExecuteScanner()
        {
            try
            {
                if (!File.Exists(_dllPath))
                {
                    Plugin.Log.LogWarning($"Scanner DLL not found: {_dllPath}");
                    return;
                }

                // 每次都重新加载程序集以获取最新版本
                var assemblyBytes = File.ReadAllBytes(_dllPath);
                _currentAssembly = Assembly.Load(assemblyBytes);
                
                Plugin.Log.LogInfo("Scanner assembly loaded successfully");

                // 查找入口类和方法
                var scannerType = _currentAssembly.GetType("BackToDawnCommPlugin.Scanner.DialogueScanner");
                if (scannerType == null)
                {
                    Plugin.Log.LogError("DialogueScanner type not found in scanner assembly");
                    return;
                }

                var executeMethod = scannerType.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public);
                if (executeMethod == null)
                {
                    Plugin.Log.LogError("Execute method not found in DialogueScanner");
                    return;
                }

                // 执行扫描器
                Plugin.Log.LogInfo("Executing scanner...");
                executeMethod.Invoke(null, new object[] { Plugin.Log });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load/execute scanner: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }


    }
}
