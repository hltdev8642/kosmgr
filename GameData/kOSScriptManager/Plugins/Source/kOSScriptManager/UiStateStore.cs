using UnityEngine;

namespace kOSScriptManager
{
    internal static class UiStateStore
    {
        public static Rect WindowRect = new Rect(120f, 120f, 1200f, 760f);
        public static string OpenVolumeName = "Archive";
        public static string OpenPath = string.Empty;
        public static Vector2 FileTreeScroll = Vector2.zero;
        public static Vector2 EditorScroll = Vector2.zero;
        public static Vector2 SnippetScroll = Vector2.zero;
        public static Vector2 PartsScroll = Vector2.zero;
        public static string LastEditorText = string.Empty;
        public static int LastCursorIndex = 0;
        public static bool IsOpen;
    }
}
