#!/usr/bin/env bash
# Собирает dist/SubPanel-Setup.exe. Нужен makensis (Ubuntu/Debian: apt install nsis; Windows: NSIS).
set -euo pipefail
cd "$(dirname "$0")"

v_js=$(grep -oP "APP_VERSION = '\K[^']+" js/main.js)
v_xml=$(grep -oP 'ExtensionBundleVersion="\K[^"]+' CSXS/manifest.xml)
v_nsi=$(grep -oP '!define VERSION "\K[^"]+' installer/installer.nsi)
if [ "$v_js" != "$v_xml" ] || [ "$v_js" != "$v_nsi" ]; then
  echo "Версии не совпадают: main.js=$v_js manifest=$v_xml installer=$v_nsi" >&2; exit 1
fi

rm -rf build dist/SubPanel-Setup.exe
mkdir -p build/payload dist
cp -r CSXS css js jsx index.html build/payload/

( cd installer && makensis -V2 installer.nsi )
ls -l dist/SubPanel-Setup.exe
