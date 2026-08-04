#!/bin/bash
# ==============================================================================
# SYSTEM PULSE v4.0 - Linux Uninstaller Script
# ==============================================================================

set -e

echo "================================================="
echo "     SYSTEM PULSE v4.0 - Linux Uninstaller      "
echo "================================================="

INSTALL_DIR="/usr/local/bin/systempulse"
SHARE_DIR="/usr/local/share/systempulse"
DESKTOP_SHORTCUT="$HOME/.local/share/applications/systempulse.desktop"

echo "[1/3] Removing systempulse executable..."
if [ -f "$INSTALL_DIR" ]; then
    sudo rm -f "$INSTALL_DIR"
    echo "  -> Removed $INSTALL_DIR"
fi

echo "[2/3] Removing static web assets directory..."
if [ -d "$SHARE_DIR" ]; then
    sudo rm -rf "$SHARE_DIR"
    echo "  -> Removed $SHARE_DIR"
fi

echo "[3/3] Removing Desktop application menu shortcut..."
if [ -f "$DESKTOP_SHORTCUT" ]; then
    rm -f "$DESKTOP_SHORTCUT"
    echo "  -> Removed $DESKTOP_SHORTCUT"
fi

echo "================================================="
echo " SYSTEM PULSE has been uninstalled successfully!"
echo "================================================="
