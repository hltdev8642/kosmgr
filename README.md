# kOS Script Manager (KSP 1.12.x)

In-game script manager and editor for kOS with:
- kOS volume and directory browser
- .ks editor with create/edit/save/duplicate/rename/delete
- Run and Debug actions against active kOS CPU
- kOS snippet/reference panel
- VAB/SPH craft part and tag mapping panel with tag-reference insertion

## Source Layout

- `GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/KOSScriptManagerController.cs`
  Main addon controller, GUI window, toolbar integration, panel routing.
- `GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/KOSIntegrationService.cs`
  kOS integration layer for volumes, files, script execution, and debug snapshot capture.
- `GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/CraftTagService.cs`
  VAB/SPH and flight part/tag querying and assignment.
- `GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/KOSSnippetCatalog.cs`
  Snippet/reference categories for kOS syntax and common structures.
- `GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/KOSScriptManagerBootstrap.cs`
  Scene bootstrap add-ons for Flight and Editor.

## Build

1. Set environment variable `KSP_ROOT` to your KSP 1.12 installation path.
2. Build project:
   - `dotnet build GameData/kOSScriptManager/Plugins/Source/kOSScriptManager/kOSScriptManager.csproj -c Release`
3. Copy produced `kOSScriptManager.dll` into:
   - `GameData/kOSScriptManager/Plugins/`

## Install

1. Install kOS in KSP first.
2. Copy `GameData/kOSScriptManager` into your KSP root `GameData` folder.
3. Start KSP and enter Flight or VAB/SPH.
4. Use the ApplicationLauncher toolbar button (kOS Script Manager icon) to open the window.

## Notes

- File operations are routed through kOS `Volume` and `VolumeFile` APIs.
- Run/Debug executes by sending `runpath(...)` through the active CPU terminal input path.
- Window position, open file path, and editor content are retained across scene transitions.
