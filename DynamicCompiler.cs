using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Logging;
using UnityEngine;

namespace FastDialog
{
    /// <summary>
    /// 真正的动态编译器 - 支持运行时编译C#代码
    /// </summary>
    public static class DynamicCompiler
    {
        /// <summary>
        /// 动态编译并执行脚本
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public static void CompileAndExecute(ManualLogSource logger)
        {
            try
            {
                logger.LogInfo("=== 开始动态编译 ===");
                
                // 获取项目目录
                string projectDir = @"C:\Users\myuan\projects\BackToDawnCommPlugin";
                string scriptPath = Path.Combine(projectDir, "DynamicScript.cs");
                
                if (!File.Exists(scriptPath))
                {
                    logger.LogError($"找不到脚本文件: {scriptPath}");
                    return;
                }
                
                logger.LogInfo($"找到脚本文件: {scriptPath}");
                
                // 使用dotnet命令编译
                string dllPath = CompileWithDotnet(projectDir, logger);
                if (string.IsNullOrEmpty(dllPath))
                {
                    logger.LogError("编译失败");
                    return;
                }
                
                // 加载并执行编译后的程序集
                ExecuteCompiledAssembly(dllPath, logger);
                
                logger.LogInfo("=== 动态编译执行完成 ===");
            }
            catch (Exception ex)
            {
                logger.LogError($"动态编译失败: {ex.Message}");
                logger.LogError($"堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 使用dotnet命令编译项目
        /// </summary>
        private static string CompileWithDotnet(string projectDir, ManualLogSource logger)
        {
            try
            {
                logger.LogInfo("使用dotnet命令编译项目...");
                
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --configuration Debug",
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        logger.LogInfo("编译成功");
                        logger.LogInfo($"编译输出: {output}");
                        
                        // 返回编译后的DLL路径
                        string dllPath = Path.Combine(projectDir, "bin", "Debug", "net6.0", "BackToDawnCommPlugin.dll");
                        if (File.Exists(dllPath))
                        {
                            logger.LogInfo($"找到编译后的DLL: {dllPath}");
                            return dllPath;
                        }
                        else
                        {
                            logger.LogError($"找不到编译后的DLL: {dllPath}");
                            return null;
                        }
                    }
                    else
                    {
                        logger.LogError($"编译失败，退出码: {process.ExitCode}");
                        logger.LogError($"错误输出: {error}");
                        logger.LogError($"标准输出: {output}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"执行dotnet编译时发生异常: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 加载并执行编译后的程序集
        /// </summary>
        private static void ExecuteCompiledAssembly(string dllPath, ManualLogSource logger)
        {
            try
            {
                logger.LogInfo("加载编译后的程序集...");
                
                // 读取DLL文件
                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                
                // 加载程序集
                Assembly assembly = Assembly.Load(assemblyBytes);
                
                // 查找DynamicScript类
                Type scriptType = assembly.GetType("BackToDawnCommPlugin.DynamicScript");
                if (scriptType == null)
                {
                    logger.LogError("找不到DynamicScript类");
                    return;
                }
                
                // 查找Execute方法
                MethodInfo executeMethod = scriptType.GetMethod("Execute", 
                    BindingFlags.Public | BindingFlags.Static);
                if (executeMethod == null)
                {
                    logger.LogError("找不到Execute方法");
                    return;
                }
                
                // 调用Execute方法
                logger.LogInfo("执行编译后的脚本...");
                executeMethod.Invoke(null, new object[] { logger });
                logger.LogInfo("脚本执行成功");
            }
            catch (Exception ex)
            {
                logger.LogError($"执行脚本时发生错误: {ex.Message}");
                logger.LogError($"堆栈跟踪: {ex.StackTrace}");
            }
        }

    }
}
