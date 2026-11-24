#!/usr/bin/env bash
# Собирает dist/SubPanel-Setup.exe.
# Требуется NSIS (Ubuntu/Debian: apt install nsis; macOS: brew install makensis; Windows: winget install NSIS.NSIS)
set -euo pipefail
cd "$(dirname "$0")"

# Извлекаем версии кроссплатформенным способом (sed вместо grep -P)
v_js=$(sed -n "s/.*APP_VERSION = '\([^']*\)'.*/\1/p" js/main.js | head -n 1)
v_xml=$(sed -n 's/.*ExtensionBundleVersion="\([^"]*\)".*/\1/p' CSXS/manifest.xml | head -n 1)
v_nsi=$(sed -n 's/.*!define VERSION "\([^"]*\)".*/\1/p' installer/installer.nsi | head -n 1)

if [ -z "$v_js" ] || [ -z "$v_xml" ] || [ -z "$v_nsi" ]; then
  echo "Ошибка: не удалось прочитать версии из файлов." >&2
  exit 1
fi

if [ "$v_js" != "$v_xml" ] || [ "$v_js" != "$v_nsi" ]; then
  echo "Версии не совпадают: main.js=$v_js manifest=$v_xml installer=$v_nsi" >&2
  exit 1
fi

echo "Сборка SubPanel v$v_js..."

# Поиск makensis
MAKENSIS="makensis"
if ! command -v "$MAKENSIS" &> /dev/null; then
  if [ -f "/c/Program Files (x86)/NSIS/makensis.exe" ]; then
    MAKENSIS="/c/Program Files (x86)/NSIS/makensis.exe"
  elif [ -f "/c/Program Files/NSIS/makensis.exe" ]; then
    MAKENSIS="/c/Program Files/NSIS/makensis.exe"
  else
    echo "Ошибка: makensis не найден. Установите NSIS." >&2
    exit 1
  fi
fi

rm -rf build dist/SubPanel-Setup.exe
mkdir -p build/payload dist
cp -r CSXS css js jsx index.html build/payload/
if [ -f "dist/PrLauncher.exe" ]; then
  cp "dist/PrLauncher.exe" build/payload/
fi

( cd installer && "$MAKENSIS" -V2 installer.nsi )

ls -lh dist/SubPanel-Setup.exe
echo "Сборка успешно завершена!"
