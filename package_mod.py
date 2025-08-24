#!/usr/bin/env python3
"""
BepInEx Mod Packaging Script
打包FastDialog插件为标准的BepInEx mod格式
"""

import os
import json
import shutil
import zipfile
from pathlib import Path
from datetime import datetime
import subprocess

# 路径配置
PROJECT_DIR = Path(__file__).parent

manifest_path = PROJECT_DIR / "manifest.json"
manifest_content = manifest_path.read_text(encoding='utf-8')
manifest = json.loads(manifest_content)

MOD_NAME = manifest["name"]
VERSION = manifest["version_number"]
AUTHOR = "myuan"
DESCRIPTION = manifest["description"]
WEBSITE_URL = manifest["website_url"]
DLL_NAME = "FastDialog.dll"

BUILD_DIR = PROJECT_DIR / "bin" / "Release" / "net6.0"
PACKAGE_DIR = PROJECT_DIR / "package"; PACKAGE_DIR.mkdir(exist_ok=True)
DIST_DIR = PROJECT_DIR / "dist"; DIST_DIR.mkdir(exist_ok=True)
OUTPUT_ZIP = DIST_DIR / f"{MOD_NAME}-{VERSION}.zip"


def package_mod():
    """打包mod为zip文件"""
    print(f"开始打包 {MOD_NAME}")
    
    # 清理并创建打包目录
    if PACKAGE_DIR.exists():
        shutil.rmtree(PACKAGE_DIR)
    PACKAGE_DIR.mkdir(parents=True)
    
    subprocess.run(["dotnet", "build", "--configuration", "Release"], cwd=PROJECT_DIR)
    
    # 创建目录结构
    plugin_dir = PACKAGE_DIR / "BepInEx" / "plugins" / f"{AUTHOR}-{MOD_NAME}"
    plugin_dir.mkdir(parents=True)
    
    # 复制DLL文件
    dll_source = BUILD_DIR / DLL_NAME
    if not dll_source.exists():
        print(f"错误: 找不到 {dll_source}")
        print("请先编译项目: dotnet build")
        return False
        
    shutil.copy2(dll_source, plugin_dir / DLL_NAME)
    print(f"✓ 复制 {DLL_NAME}")
    
    # 创建 manifest.json
    manifest_path = PACKAGE_DIR / "manifest.json"
    manifest_path.write_text(manifest_content, encoding='utf-8')
    print("✓ 创建 manifest.json")
    
    # 创建 README.md
    readme_path = PACKAGE_DIR / "README.md"
    readme_path.write_text(open('README.md', 'r', encoding='utf-8').read(), encoding='utf-8')
    print("✓ 创建 README.md")
    
    # 复制图标（如果存在）
    icon_source = PROJECT_DIR / "icon.png"
    if icon_source.exists():
        shutil.copy2(icon_source, PACKAGE_DIR / "icon.png")
        print("✓ 复制 icon.png")
    else:
        print("⚠ 未找到 icon.png，跳过")
    
    # 创建ZIP文件
    if OUTPUT_ZIP.exists():
        OUTPUT_ZIP.unlink()
        
    with zipfile.ZipFile(OUTPUT_ZIP, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(PACKAGE_DIR):
            for file in files:
                file_path = Path(root) / file
                arcname = file_path.relative_to(PACKAGE_DIR)
                zipf.write(file_path, arcname)
                print(f"  添加: {arcname}")
    
    # 清理临时目录
    shutil.rmtree(PACKAGE_DIR)
    
    print(f"✅ 打包完成: {OUTPUT_ZIP}")
    print(f"📦 文件大小: {OUTPUT_ZIP.stat().st_size / 1024:.1f} KB")
    
    return True

def main():
    """主函数"""
    print("=" * 50)
    print(f"    {MOD_NAME} Mod 打包工具")
    print("=" * 50)
    
    if package_mod():
        print("\n🎉 打包成功！")
        print(f"输出文件: {OUTPUT_ZIP.name}")
        print("\n可以上传到 Thunderstore 或分享给其他玩家了！")
    else:
        print("\n❌ 打包失败！")
        return 1
    
    return 0

if __name__ == "__main__":
    exit(main())