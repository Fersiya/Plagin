Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"

!define APPNAME "SubPanel"
!define EXTID "com.fersiya.subpanel"
!define VERSION "1.0.0"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${EXTID}"

Name "${APPNAME} ${VERSION}"
OutFile "..\dist\SubPanel-Setup.exe"
; Панель ставится для текущего пользователя, права администратора не нужны.
InstallDir "$APPDATA\Adobe\CEP\extensions\${EXTID}"
RequestExecutionLevel user
BrandingText "${APPNAME} ${VERSION}"

!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "SubPanel для Premiere Pro и After Effects"
!define MUI_WELCOMEPAGE_TEXT "Панель с заготовками стиля субтитров и очисткой знаков препинания.$\r$\n$\r$\nПеред установкой закрой Premiere Pro и After Effects. После установки запусти их и открой панель: Окно > Расширения > SubPanel."
!define MUI_FINISHPAGE_TITLE "Готово"
!define MUI_FINISHPAGE_TEXT "SubPanel установлена.$\r$\n$\r$\nПерезапусти Premiere Pro и After Effects, затем открой Окно > Расширения > SubPanel."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Section "Install"
  SetOutPath "$INSTDIR"
  RMDir /r "$INSTDIR\CSXS"
  RMDir /r "$INSTDIR\js"
  RMDir /r "$INSTDIR\jsx"
  RMDir /r "$INSTDIR\css"
  File /r "..\build\payload\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Неподписанные панели CEP загружаются только при включённом PlayerDebugMode.
  ${ForEach} $0 9 13 + 1
    WriteRegStr HKCU "Software\Adobe\CSXS.$0" "PlayerDebugMode" "1"
  ${Next}

  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "Fersiya"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ; PlayerDebugMode не трогаем: он может быть нужен другим панелям.
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNINST_KEY}"
SectionEnd
