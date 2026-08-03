#!/bin/bash
# ==============================================================================
# SYSTEM PULSE v3.0 - Linux Setup & Installer Script
# ==============================================================================

set -e

echo "================================================="
echo "   SYSTEM PULSE v3.0 - Linux Setup & Installer   "
echo "================================================="

INSTALL_DIR="/usr/local/bin"
APP_NAME="systempulse"
DESKTOP_DIR="$HOME/.local/share/applications"

# Check if single-file binary exists
if [ ! -f "./SystemMonitor" ]; then
    echo "[Error] 'SystemMonitor' binary not found in current directory."
    echo "Please run this script inside the extracted SYSTEM PULSE Linux package folder."
    exit 1
fi

echo "[1/3] Copying SystemMonitor binary to $INSTALL_DIR/$APP_NAME..."
sudo cp ./SystemMonitor "$INSTALL_DIR/$APP_NAME"
sudo chmod +x "$INSTALL_DIR/$APP_NAME"

echo "[2/3] Copying static web assets to /usr/local/share/systempulse/wwwroot..."
sudo mkdir -p /usr/local/share/systempulse
if [ -d "./wwwroot" ]; then
    sudo cp -r ./wwwroot /usr/local/share/systempulse/
fi

echo "[3/3] Registering Linux Desktop Menu Shortcut..."
mkdir -p "$DESKTOP_DIR"

cat <<EOF > "$DESKTOP_DIR/systempulse.desktop"
[Desktop Entry]
Name=SYSTEM PULSE
Comment=Real-Time C# Telemetry & System Monitor
Exec=/usr/local/bin/systempulse
Icon=utilities-system-monitor
Terminal=false
Type=Application
Categories=System;Monitor;
EOF

chmod +x "$DESKTOP_DIR/systempulse.desktop"

echo "================================================="
echo " 🎉 SYSTEM PULSE v3.0 successfully installed!"
echo " Launch from application menu or run: systempulse"
echo "================================================="
