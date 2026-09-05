import os
import sys
import platform
import subprocess
import shutil
from pathlib import Path

APP_NAME = "PIMS"
EXEC_NAME = "wankplanner"
ROOT_DIR = Path(__file__).parent.resolve()
VENV_DIR = ROOT_DIR / "venv"
OS_TYPE = platform.system()

def run_cmd(cmd, check=True):
    print(f"[*] Running: {' '.join(cmd)}")
    subprocess.run(cmd, check=check, cwd=ROOT_DIR)

def check_dependencies():
    print("[+] Checking system dependencies...")
    try:
        subprocess.run(["dotnet", "--version"], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    except FileNotFoundError:
        sys.exit("[-] Error: .NET SDK is not installed or not in PATH.")
    
    if shutil.which("sqlite3") is None and OS_TYPE != "Windows":
        print("[-] Warning: sqlite3 CLI not found. Backups may fail on Unix systems.")

    if OS_TYPE == "Linux":
        print("[*] Checking for WebKitGTK (Required for Photino UI)...")
        if shutil.which("pkg-config"):
            if subprocess.run(["pkg-config", "--exists", "webkit2gtk-4.0"]).returncode != 0 and \
               subprocess.run(["pkg-config", "--exists", "webkit2gtk-4.1"]).returncode != 0:
                print("[-] Warning: webkit2gtk-4.0/4.1 not found. Ensure it is installed via dnf/apt.")

def build_dotnet():
    print("[+] Building .NET application...")
    run_cmd(["dotnet", "build", "-c", "Release"])

def get_exec_path():
    ext = ".exe" if OS_TYPE == "Windows" else ""
    search_dir = ROOT_DIR / "bin" / "Release"
    
    if search_dir.exists():
        for path in search_dir.rglob(f"{EXEC_NAME}{ext}"):
            if path.is_file() and "publish" not in path.parts:
                return path
                
    return ROOT_DIR / "bin" / "Release" / "net10.0" / f"{EXEC_NAME}{ext}"

def setup_python_env():
    print("[+] Setting up Python virtual environment for the System Tray...")
    if not VENV_DIR.exists():
        run_cmd([sys.executable, "-m", "venv", "venv"])
    
    pip_exe = VENV_DIR / "Scripts" / "pip" if OS_TYPE == "Windows" else VENV_DIR / "bin" / "pip"
    run_cmd([str(pip_exe), "install", "PyQt6"])

def configure_linux():
    print("[+] Configuring Linux Systemd Services and Autostart...")
    user_systemd = Path.home() / ".config" / "systemd" / "user"
    autostart_dir = Path.home() / ".config" / "autostart"
    user_systemd.mkdir(parents=True, exist_ok=True)
    autostart_dir.mkdir(parents=True, exist_ok=True)

    exec_path = get_exec_path()
    python_exe = VENV_DIR / "bin" / "python"
    tray_script = ROOT_DIR / "tray.py"
    
    service_content = f"""[Unit]
Description={APP_NAME} Daemon
After=graphical-session.target

[Service]
Type=simple
WorkingDirectory={ROOT_DIR}
ExecStart={exec_path} --log
Restart=on-failure
RestartSec=10

[Install]
WantedBy=default.target
"""
    (user_systemd / f"{EXEC_NAME}.service").write_text(service_content)
    
    desktop_content = f"""[Desktop Entry]
Type=Application
Name={APP_NAME} Tray
Exec={python_exe} {tray_script}
Terminal=false
StartupNotify=false
NoDisplay=true
"""
    (autostart_dir / f"{EXEC_NAME}-tray.desktop").write_text(desktop_content)

    print("[*] Enabling systemd daemon...")
    run_cmd(["systemctl", "--user", "daemon-reload"])
    run_cmd(["systemctl", "--user", "enable", "--now", f"{EXEC_NAME}.service"])

def configure_windows():
    print("[+] Configuring Windows Startup and Scheduled Tasks...")
    startup_dir = Path(os.getenv('APPDATA')) / "Microsoft" / "Windows" / "Start Menu" / "Programs" / "Startup"
    
    python_exe = VENV_DIR / "Scripts" / "pythonw.exe"
    tray_script = ROOT_DIR / "tray.py"
    exec_path = get_exec_path()
    
    tray_bat = startup_dir / f"{APP_NAME}Tray.bat"
    tray_bat.write_text(f'start "" "{python_exe}" "{tray_script}"')
    
    daemon_bat = startup_dir / f"{APP_NAME}Daemon.bat"
    daemon_bat.write_text(f'start "" "{exec_path}" --log')

def configure_mac():
    print("[+] Configuring macOS LaunchAgents...")
    launch_agents = Path.home() / "Library" / "LaunchAgents"
    launch_agents.mkdir(parents=True, exist_ok=True)
    
    exec_path = get_exec_path()
    python_exe = VENV_DIR / "bin" / "python"
    tray_script = ROOT_DIR / "tray.py"

    plist_content = f"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.{APP_NAME.lower()}.tray</string>
    <key>ProgramArguments</key>
    <array>
        <string>{python_exe}</string>
        <string>{tray_script}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>
"""
    (launch_agents / f"com.{APP_NAME.lower()}.tray.plist").write_text(plist_content)
    print("[*] Note: macOS notifications require `osascript` (built-in).")

def main():
    print(f"=== {APP_NAME} Cross-Platform Installer ===")
    print(f"[*] Detected OS: {OS_TYPE}")
    
    check_dependencies()
    build_dotnet()
    setup_python_env()
    
    if OS_TYPE == "Linux":
        configure_linux()
    elif OS_TYPE == "Windows":
        configure_windows()
    elif OS_TYPE == "Darwin":
        configure_mac()
    else:
        print(f"[-] Unsupported OS: {OS_TYPE}")

    print("\n[✓] Installation complete!")

if __name__ == "__main__":
    main()
