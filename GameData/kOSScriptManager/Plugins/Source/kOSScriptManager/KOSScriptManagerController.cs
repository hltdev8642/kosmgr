using System;
using System.Collections.Generic;
using System.Linq;
using kOS.Module;
using kOS.Safe.Persistence;
using KSP.UI.Screens;
using UnityEngine;

namespace kOSScriptManager
{
    public sealed class KOSScriptManagerController : MonoBehaviour
    {
        private const string EditorControlName = "kOSScriptManager.Editor";
        private const int WindowId = 380914;

        private static KOSScriptManagerController? instance;

        private readonly KOSIntegrationService kosService = new KOSIntegrationService();
        private readonly CraftTagService craftTagService = new CraftTagService();
        private readonly List<Volume> volumes = new List<Volume>(8);
        private readonly List<VolumeItemView> directoryItems = new List<VolumeItemView>(128);
        private readonly Dictionary<uint, string> partTagEditBuffer = new Dictionary<uint, string>();

        private ApplicationLauncherButton? toolbarButton;
        private Texture2D? toolbarTexture;

        private string statusLine = "Ready.";
        private string currentDirectory = string.Empty;
        private string selectedFilePath = string.Empty;
        private string selectedFileName = string.Empty;
        private string editorText = string.Empty;
        private string fileNameInput = "script.ks";
        private string debugOutput = string.Empty;

        private int selectedVolumeIndex;
        private int selectedRightTab;
        private int cursorIndex;

        private double nextRefreshTime;
        private bool needsRefresh = true;

        public static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            var go = new GameObject("kOSScriptManagerController");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KOSScriptManagerController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            LoadUiState();

            GameEvents.onGUIApplicationLauncherReady.Add(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneLoadRequested);

            if (ApplicationLauncher.Ready)
            {
                OnLauncherReady();
            }
        }

        private void OnDestroy()
        {
            SaveUiState();

            GameEvents.onGUIApplicationLauncherReady.Remove(OnLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneLoadRequested);

            RemoveLauncherButton();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (!IsSupportedScene())
            {
                return;
            }

            if (!UiStateStore.IsOpen)
            {
                return;
            }

            var now = Planetarium.GetUniversalTime();
            if (!needsRefresh && now < nextRefreshTime)
            {
                return;
            }

            RefreshVolumesAndDirectory();
            nextRefreshTime = now + 0.8;
            needsRefresh = false;

            var processor = kosService.GetPreferredProcessor();
            debugOutput = kosService.GetDebugOutput(processor);
        }

        private void OnGUI()
        {
            if (!UiStateStore.IsOpen || !IsSupportedScene())
            {
                return;
            }

            UiStateStore.WindowRect = GUILayout.Window(WindowId, UiStateStore.WindowRect, DrawWindow, "kOS Script Manager");
            HandleResize();
        }

        private void DrawWindow(int id)
        {
            DrawStatusBar();

            GUILayout.BeginHorizontal();
            DrawFileBrowserPanel();
            DrawEditorPanel();
            DrawRightPanel();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, UiStateStore.WindowRect.width - 18f, 24f));
        }

        private void DrawStatusBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(24f));
            GUILayout.Label(statusLine, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            {
                needsRefresh = true;
                RefreshVolumesAndDirectory();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFileBrowserPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(310f));
            GUILayout.Label("Volumes", HighLogic.Skin.label);

            if (volumes.Count == 0)
            {
                GUILayout.Label("No kOS volumes detected.");
                GUILayout.EndVertical();
                return;
            }

            var volumeNames = volumes.Select(kosService.DisplayVolumeName).ToArray();
            var newSelectedVolume = GUILayout.SelectionGrid(selectedVolumeIndex, volumeNames, 1);
            if (newSelectedVolume != selectedVolumeIndex)
            {
                selectedVolumeIndex = newSelectedVolume;
                currentDirectory = string.Empty;
                selectedFilePath = string.Empty;
                needsRefresh = true;
                RefreshDirectoryOnly();
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(currentDirectory) ? "/" : "/" + currentDirectory, GUILayout.ExpandWidth(true));
            GUI.enabled = !string.IsNullOrEmpty(currentDirectory);
            if (GUILayout.Button("Up", GUILayout.Width(52f)))
            {
                NavigateUp();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            UiStateStore.FileTreeScroll = GUILayout.BeginScrollView(UiStateStore.FileTreeScroll, GUILayout.ExpandHeight(true));
            foreach (var item in directoryItems)
            {
                DrawFileBrowserItem(item);
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawFileBrowserItem(VolumeItemView item)
        {
            var oldColor = GUI.contentColor;
            if (item.IsDirectory)
            {
                GUI.contentColor = Color.cyan;
            }
            else if (item.IsKsFile)
            {
                GUI.contentColor = Color.green;
            }

            var prefix = item.IsDirectory ? "[DIR] " : "[FILE] ";
            if (GUILayout.Button(prefix + item.Name, GUILayout.ExpandWidth(true)))
            {
                if (item.IsDirectory)
                {
                    currentDirectory = item.Path;
                    selectedFilePath = string.Empty;
                    needsRefresh = true;
                    RefreshDirectoryOnly();
                }
                else
                {
                    OpenFile(item.Path);
                }
            }

            GUI.contentColor = oldColor;
        }

        private void DrawEditorPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUILayout.BeginHorizontal();
            GUILayout.Label("File", GUILayout.Width(28f));
            fileNameInput = GUILayout.TextField(fileNameInput, GUILayout.MinWidth(180f));

            if (GUILayout.Button("New", GUILayout.Width(70f)))
            {
                CreateNewFile();
            }
            if (GUILayout.Button("Save", GUILayout.Width(70f)))
            {
                SaveOpenFile();
            }
            if (GUILayout.Button("Duplicate", GUILayout.Width(90f)))
            {
                DuplicateOpenFile();
            }
            if (GUILayout.Button("Rename", GUILayout.Width(80f)))
            {
                RenameOpenFile();
            }
            if (GUILayout.Button("Delete", GUILayout.Width(75f)))
            {
                DeleteOpenFile();
            }
            if (GUILayout.Button("Run", GUILayout.Width(65f)))
            {
                RunOpenFile(false);
            }
            if (GUILayout.Button("Debug", GUILayout.Width(70f)))
            {
                RunOpenFile(true);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(string.IsNullOrEmpty(selectedFilePath) ? "No file selected." : selectedFilePath);

            UiStateStore.EditorScroll = GUILayout.BeginScrollView(UiStateStore.EditorScroll, GUILayout.ExpandHeight(true));
            GUI.SetNextControlName(EditorControlName);
            editorText = GUILayout.TextArea(editorText, GUILayout.ExpandHeight(true));
            CaptureCursor();
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(360f));

            var tabs = new[] { "Reference", "Craft", "Debug" };
            selectedRightTab = GUILayout.Toolbar(selectedRightTab, tabs);

            UiStateStore.SnippetScroll = GUILayout.BeginScrollView(UiStateStore.SnippetScroll, GUILayout.ExpandHeight(true));
            if (selectedRightTab == 0)
            {
                DrawReferencePanel();
            }
            else if (selectedRightTab == 1)
            {
                DrawCraftPanel();
            }
            else
            {
                DrawDebugPanel();
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawReferencePanel()
        {
            foreach (var category in KOSSnippetCatalog.Categories)
            {
                GUILayout.Label(category.Name, HighLogic.Skin.box);
                foreach (var snippet in category.Snippets)
                {
                    if (GUILayout.Button(snippet, GUILayout.ExpandWidth(true)))
                    {
                        InsertAtCursor(snippet);
                    }
                }

                GUILayout.Space(6f);
            }
        }

        private void DrawCraftPanel()
        {
            var entries = craftTagService.BuildPartList();
            if (entries.Count == 0)
            {
                GUILayout.Label("No kOS processors/tags found on this craft.");
                return;
            }

            foreach (var entry in entries)
            {
                var key = entry.Part.craftID;
                if (!partTagEditBuffer.ContainsKey(key))
                {
                    partTagEditBuffer[key] = entry.Tag;
                }

                GUILayout.BeginVertical(HighLogic.Skin.box);
                GUILayout.Label(entry.DisplayName + (entry.IsProcessor ? " [CPU]" : ""));
                partTagEditBuffer[key] = GUILayout.TextField(partTagEditBuffer[key]);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Tag", GUILayout.Width(90f)))
                {
                    if (craftTagService.SetTag(entry.Part, partTagEditBuffer[key], out var error))
                    {
                        statusLine = "Tag applied to " + entry.DisplayName;
                    }
                    else
                    {
                        statusLine = "Tag apply failed: " + error;
                    }
                }

                if (GUILayout.Button("Insert Ref", GUILayout.Width(90f)))
                {
                    var tag = partTagEditBuffer[key];
                    if (craftTagService.SetTag(entry.Part, tag, out var error))
                    {
                        InsertAtCursor(craftTagService.BuildTagReference(tag));
                        statusLine = "Inserted reference for tag '" + tag + "'.";
                    }
                    else
                    {
                        statusLine = "Unable to set tag before insert: " + error;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Tag Mapping", HighLogic.Skin.box);
            var mapped = entries
                .Where(x => !string.IsNullOrWhiteSpace(x.Tag))
                .GroupBy(x => x.Tag)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in mapped)
            {
                var linkedParts = string.Join(", ", group.Select(x => x.Part.partInfo != null ? x.Part.partInfo.title : x.Part.name).ToArray());
                GUILayout.Label(group.Key + " => " + linkedParts);
            }
        }

        private void DrawDebugPanel()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Output", GUILayout.Width(110f)))
            {
                var processor = kosService.GetPreferredProcessor();
                debugOutput = kosService.GetDebugOutput(processor);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                debugOutput = string.Empty;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Active CPU terminal output:");
            GUILayout.TextArea(debugOutput, GUILayout.ExpandHeight(true));
        }

        private void CreateNewFile()
        {
            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            var fileName = NormalizeFileName(fileNameInput);
            var path = kosService.ComposePath(currentDirectory, fileName);
            if (kosService.TrySaveText(volume, path, editorText, out var error))
            {
                statusLine = "Created " + path;
                OpenFile(path);
                needsRefresh = true;
            }
            else
            {
                statusLine = "Create failed: " + error;
            }
        }

        private void SaveOpenFile()
        {
            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            var path = selectedFilePath;
            if (string.IsNullOrEmpty(path))
            {
                path = kosService.ComposePath(currentDirectory, NormalizeFileName(fileNameInput));
            }

            if (kosService.TrySaveText(volume, path, editorText, out var error))
            {
                selectedFilePath = path;
                selectedFileName = System.IO.Path.GetFileName(path);
                fileNameInput = selectedFileName;
                statusLine = "Saved " + path;
                needsRefresh = true;
            }
            else
            {
                statusLine = "Save failed: " + error;
            }
        }

        private void DuplicateOpenFile()
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                statusLine = "Select a source file first.";
                return;
            }

            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            var target = kosService.ComposePath(currentDirectory, NormalizeFileName(fileNameInput));
            if (kosService.TryDuplicate(volume, selectedFilePath, target, out var error))
            {
                statusLine = "Duplicated to " + target;
                OpenFile(target);
                needsRefresh = true;
            }
            else
            {
                statusLine = "Duplicate failed: " + error;
            }
        }

        private void RenameOpenFile()
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                statusLine = "Select a source file first.";
                return;
            }

            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            var target = kosService.ComposePath(currentDirectory, NormalizeFileName(fileNameInput));
            if (string.Equals(target, selectedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                statusLine = "Rename target matches current file.";
                return;
            }

            if (kosService.TryRenameByCopy(volume, selectedFilePath, target, out var error))
            {
                statusLine = "Renamed to " + target;
                OpenFile(target);
                needsRefresh = true;
            }
            else
            {
                statusLine = "Rename failed: " + error;
            }
        }

        private void DeleteOpenFile()
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                statusLine = "Select a file first.";
                return;
            }

            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            if (kosService.TryDelete(volume, selectedFilePath, out var error))
            {
                statusLine = "Deleted " + selectedFilePath;
                selectedFilePath = string.Empty;
                selectedFileName = string.Empty;
                fileNameInput = "script.ks";
                editorText = string.Empty;
                needsRefresh = true;
                RefreshDirectoryOnly();
            }
            else
            {
                statusLine = "Delete failed: " + error;
            }
        }

        private void RunOpenFile(bool debugMode)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                statusLine = "Select a file to run.";
                return;
            }

            var processor = kosService.GetPreferredProcessor();
            if (processor == null)
            {
                statusLine = "No active kOS processor found.";
                return;
            }

            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            if (kosService.TryRunScript(processor, volume, selectedFilePath, debugMode, out var error))
            {
                statusLine = (debugMode ? "Debug started: " : "Run started: ") + selectedFilePath;
            }
            else
            {
                statusLine = "Run failed: " + error;
            }
        }

        private void OpenFile(string path)
        {
            var volume = GetSelectedVolume();
            if (volume == null)
            {
                statusLine = "No volume selected.";
                return;
            }

            if (kosService.TryReadText(volume, path, out var text, out var error))
            {
                selectedFilePath = path;
                selectedFileName = System.IO.Path.GetFileName(path);
                fileNameInput = selectedFileName;
                editorText = text;
                cursorIndex = Math.Min(cursorIndex, editorText.Length);
                statusLine = "Opened " + path;
            }
            else
            {
                statusLine = "Open failed: " + error;
            }
        }

        private void InsertAtCursor(string snippet)
        {
            if (snippet == null)
            {
                return;
            }

            var insertPos = Mathf.Clamp(cursorIndex, 0, editorText.Length);
            editorText = editorText.Insert(insertPos, snippet);
            cursorIndex = insertPos + snippet.Length;
            statusLine = "Inserted snippet.";
        }

        private void CaptureCursor()
        {
            if (GUI.GetNameOfFocusedControl() != EditorControlName)
            {
                return;
            }

            var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (editor == null)
            {
                return;
            }

            cursorIndex = editor.cursorIndex;
        }

        private void NavigateUp()
        {
            if (string.IsNullOrEmpty(currentDirectory))
            {
                return;
            }

            var segments = currentDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                currentDirectory = string.Empty;
            }
            else
            {
                currentDirectory = string.Join("/", segments.Take(segments.Length - 1).ToArray());
            }

            selectedFilePath = string.Empty;
            needsRefresh = true;
            RefreshDirectoryOnly();
        }

        private void RefreshVolumesAndDirectory()
        {
            var processor = kosService.GetPreferredProcessor();
            volumes.Clear();
            volumes.AddRange(kosService.GetAccessibleVolumes(processor));

            if (selectedVolumeIndex < 0 || selectedVolumeIndex >= volumes.Count)
            {
                selectedVolumeIndex = 0;
            }

            RefreshDirectoryOnly();
        }

        private void RefreshDirectoryOnly()
        {
            directoryItems.Clear();
            var volume = GetSelectedVolume();
            if (volume == null)
            {
                return;
            }

            directoryItems.AddRange(kosService.ListDirectory(volume, currentDirectory));
        }

        private Volume? GetSelectedVolume()
        {
            if (volumes.Count == 0)
            {
                return null;
            }

            if (selectedVolumeIndex < 0 || selectedVolumeIndex >= volumes.Count)
            {
                selectedVolumeIndex = 0;
            }

            return volumes[selectedVolumeIndex];
        }

        private static string NormalizeFileName(string fileName)
        {
            var value = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
            {
                value = "script.ks";
            }

            if (!value.EndsWith(".ks", StringComparison.OrdinalIgnoreCase))
            {
                value += ".ks";
            }

            return value;
        }

        private void HandleResize()
        {
            var resizeRect = new Rect(UiStateStore.WindowRect.xMax - 18f, UiStateStore.WindowRect.yMax - 18f, 18f, 18f);
            GUI.Label(resizeRect, "+");

            var e = Event.current;
            if (e == null)
            {
                return;
            }

            if (e.type == EventType.MouseDrag && resizeRect.Contains(e.mousePosition))
            {
                UiStateStore.WindowRect.width = Mathf.Max(820f, UiStateStore.WindowRect.width + e.delta.x);
                UiStateStore.WindowRect.height = Mathf.Max(560f, UiStateStore.WindowRect.height + e.delta.y);
                e.Use();
            }
        }

        private void OnLauncherReady()
        {
            if (toolbarButton != null)
            {
                return;
            }

            toolbarTexture = BuildToolbarTexture();
            toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                OnToolbarOpen,
                OnToolbarClose,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                toolbarTexture);
        }

        private void OnLauncherDestroyed()
        {
            RemoveLauncherButton();
        }

        private void OnSceneLoadRequested(GameScenes scene)
        {
            SaveUiState();
            needsRefresh = true;
            nextRefreshTime = 0;
        }

        private void OnToolbarOpen()
        {
            UiStateStore.IsOpen = true;
            needsRefresh = true;
            RefreshVolumesAndDirectory();
        }

        private void OnToolbarClose()
        {
            UiStateStore.IsOpen = false;
            SaveUiState();
        }

        private void RemoveLauncherButton()
        {
            if (toolbarButton == null || ApplicationLauncher.Instance == null)
            {
                return;
            }

            ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            toolbarButton = null;
        }

        private static Texture2D BuildToolbarTexture()
        {
            var tex = new Texture2D(38, 38, TextureFormat.ARGB32, false);
            var bg = new Color32(18, 34, 40, 255);
            var fg = new Color32(93, 220, 139, 255);

            var pixels = new Color32[38 * 38];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bg;
            }

            for (var y = 7; y < 31; y++)
            {
                pixels[(y * 38) + 7] = fg;
                pixels[(y * 38) + 30] = fg;
            }

            for (var x = 7; x < 31; x++)
            {
                pixels[(7 * 38) + x] = fg;
                pixels[(30 * 38) + x] = fg;
            }

            for (var x = 12; x < 26; x++)
            {
                pixels[(14 * 38) + x] = fg;
                pixels[(19 * 38) + x] = fg;
                pixels[(24 * 38) + x] = fg;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static bool IsSupportedScene()
        {
            return HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor;
        }

        private void LoadUiState()
        {
            currentDirectory = UiStateStore.OpenPath ?? string.Empty;
            editorText = UiStateStore.LastEditorText ?? string.Empty;
            cursorIndex = Mathf.Clamp(UiStateStore.LastCursorIndex, 0, editorText.Length);
            selectedFilePath = UiStateStore.OpenPath ?? string.Empty;
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                selectedFileName = System.IO.Path.GetFileName(selectedFilePath);
                fileNameInput = selectedFileName;
            }
        }

        private void SaveUiState()
        {
            UiStateStore.OpenPath = selectedFilePath;
            UiStateStore.LastEditorText = editorText;
            UiStateStore.LastCursorIndex = cursorIndex;
        }
    }
}
