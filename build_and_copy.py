import subprocess
import shutil
from pathlib import Path
import os

game_path = Path(r'C:\Program Files (x86)\Steam\steamapps\common\MetalHeadGames\Back To The Dawn.exe')
game_id = 1735700

process_name = game_path.name

print('killing process...')
cmd = f'taskkill /F /IM "{process_name}"'
r = subprocess.Popen(cmd, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
r.wait()
if r.returncode not in [
    128,  # 进程不存在
    0     # 进程已结束
]:
    print('kill process failed')
    print(r.stdout.read().decode('utf-8'))
    print(r.stderr.read().decode('utf-8'))
    exit(1)

r = subprocess.Popen(
    'dotnet build', stdout=subprocess.PIPE, stderr=subprocess.PIPE
)
print('building...')
r.wait()

if r.returncode != 0:
    print('build failed')
    print(r.stdout.read().decode('utf-8'))
    print(r.stderr.read().decode('utf-8'))
    exit(1)

shutil.copy(
    'bin/Debug/net6.0/BackToDawnCommPlugin.dll', 
    game_path.parent / 'BepInEx' / 'plugins' / 'BackToDawnCommPlugin'
)

print('build and copy success')

os.startfile(f'steam://rungameid/{game_id}')