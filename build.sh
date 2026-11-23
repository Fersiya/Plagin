#!/usr/bin/env bash
# Собирает dist/SubPanel-Setup.exe. Нужен makensis (Ubuntu/Debian: apt install nsis; Windows: NSIS).
set -euo pipefail
cd "$(dirname "$0")"

rm -rf build dist/SubPanel-Setup.exe
mkdir -p build/payload dist
cp -r CSXS css js jsx index.html build/payload/

( cd installer && makensis -V2 installer.nsi )
ls -l dist/SubPanel-Setup.exe
