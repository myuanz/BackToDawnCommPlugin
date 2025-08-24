using System;
using System.Linq;
using Il2CppSystem.Reflection;
using UnityEngine;

namespace FastDialog
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
            var tp = self.GetIl2CppType();
            var mth = tp.GetMethod("SetForceCanUseKeyboardMouse", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Array.Empty<Il2CppSystem.Type>(), null);
            if (mth != null) {
                mth.Invoke(self, new Il2CppSystem.Object[] {  });
            } else {
                Plugin.Log.LogError($"SetForceKeyboardMouse: {tp.Name} not found");
            }
        }

        /// <summary>获取强制键盘鼠标标志</summary>
        public static bool? GetForceKeyboardMouseFlag(this MonoBehaviour self)
        {
            var prop = DialogueScanner.GetMember(self, "isForceCanUseKeyboardMouse");
            return prop?.Unbox<bool>();
        }
    }
}
