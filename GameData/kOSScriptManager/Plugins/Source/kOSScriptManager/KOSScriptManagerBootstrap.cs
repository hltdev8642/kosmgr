using UnityEngine;

namespace kOSScriptManager
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KOSScriptManagerFlightBootstrap : MonoBehaviour
    {
        private void Start()
        {
            KOSScriptManagerController.EnsureInstance();
        }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public sealed class KOSScriptManagerEditorBootstrap : MonoBehaviour
    {
        private void Start()
        {
            KOSScriptManagerController.EnsureInstance();
        }
    }
}
