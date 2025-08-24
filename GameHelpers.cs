using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BackToDawnCommPlugin
{
    /// <summary>游戏辅助工具类</summary>
    static class InputManageHelper
    {
        /// <summary>查找游戏中的InputManage组件</summary>
        public static MonoBehaviour FindInputManage()
        {
            var gm = GameObject.Find("GameManage");
            // Console.WriteLine($"gm = {gm}");
            // gm?.GetComponents<MonoBehaviour>().ToList().ForEach(m => Console.WriteLine($"m = {m}, name={m.GetScriptClassName()}"));
            var im = gm?.GetComponents<MonoBehaviour>().FirstOrDefault(
                m => m && m.GetScriptClassName() == "InputManage"
            );
            // Console.WriteLine($"im = {im}");
            return im;
        }
    }

    /// <summary>MonoBehaviour扩展方法</summary>
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
}
