using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using kOS.Module;
using kOS.Safe;
using kOS.Safe.Persistence;
using kOS.Safe.Utilities;

namespace kOSScriptManager
{
    internal sealed class VolumeItemView
    {
        public string Name = string.Empty;
        public string Path = string.Empty;
        public bool IsDirectory;
        public bool IsKsFile;
    }

    internal sealed class KOSIntegrationService
    {
        private readonly List<Volume> volumeCache = new List<Volume>(8);
        private readonly List<string> outputLines = new List<string>(200);
        private double lastSnapshotTime;

        public IReadOnlyList<Volume> GetAccessibleVolumes(kOSProcessor? processor)
        {
            volumeCache.Clear();

            if (processor != null)
            {
                if (processor.Archive != null)
                {
                    volumeCache.Add(processor.Archive);
                }

                var vessel = processor.vessel;
                if (vessel != null)
                {
                    foreach (var part in vessel.parts)
                    {
                        if (part == null)
                        {
                            continue;
                        }

                        foreach (var cpu in part.Modules.OfType<kOSProcessor>())
                        {
                            if (cpu != null && cpu.HardDisk != null && !volumeCache.Contains(cpu.HardDisk))
                            {
                                volumeCache.Add(cpu.HardDisk);
                            }
                        }
                    }
                }
            }
            else
            {
                try
                {
                    var archive = new Archive(SafeHouse.ArchiveFolder);
                    volumeCache.Add(archive);
                }
                catch
                {
                    // kOS can be absent while a scene is changing.
                }
            }

            return volumeCache;
        }

        public kOSProcessor? GetPreferredProcessor()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return null;
            }

            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return null;
            }

            var root = vessel.rootPart;
            if (root != null)
            {
                var rootCpu = root.Modules.OfType<kOSProcessor>().FirstOrDefault();
                if (rootCpu != null)
                {
                    return rootCpu;
                }
            }

            foreach (var part in vessel.parts)
            {
                var cpu = part.Modules.OfType<kOSProcessor>().FirstOrDefault();
                if (cpu != null)
                {
                    return cpu;
                }
            }

            return null;
        }

        public List<VolumeItemView> ListDirectory(Volume volume, string directoryPath)
        {
            var result = new List<VolumeItemView>(64);
            if (volume == null)
            {
                return result;
            }

            var normalized = NormalizePath(directoryPath);
            VolumeDirectory? directory;
            if (string.IsNullOrEmpty(normalized))
            {
                directory = volume.Root;
            }
            else
            {
                directory = volume.Open(VolumePath.FromString(normalized)) as VolumeDirectory;
            }

            if (directory == null)
            {
                return result;
            }

            var list = directory.List();
            foreach (var kv in list)
            {
                var item = kv.Value;
                if (item == null)
                {
                    continue;
                }

                result.Add(new VolumeItemView
                {
                    Name = item.Name,
                    Path = item.Path.ToString(),
                    IsDirectory = item is VolumeDirectory,
                    IsKsFile = string.Equals(item.Extension, Volume.KERBOSCRIPT_EXTENSION, StringComparison.OrdinalIgnoreCase)
                });
            }

            result.Sort((a, b) =>
            {
                if (a.IsDirectory != b.IsDirectory)
                {
                    return a.IsDirectory ? -1 : 1;
                }
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        public bool TryReadText(Volume volume, string path, out string text, out string error)
        {
            text = string.Empty;
            error = string.Empty;

            try
            {
                var file = volume.Open(VolumePath.FromString(path)) as VolumeFile;
                if (file == null)
                {
                    error = "File not found: " + path;
                    return false;
                }

                text = file.ReadAll().String;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TrySaveText(Volume volume, string path, string text, out string error)
        {
            error = string.Empty;
            try
            {
                var content = new FileContent(FileContent.EncodeString(text ?? string.Empty));
                var saved = volume.SaveFile(VolumePath.FromString(path), content);
                if (saved == null)
                {
                    error = "Save failed. Volume reported no target file.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryDelete(Volume volume, string path, out string error)
        {
            error = string.Empty;
            try
            {
                if (!volume.Delete(VolumePath.FromString(path)))
                {
                    error = "Delete failed for " + path;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryDuplicate(Volume volume, string sourcePath, string destinationPath, out string error)
        {
            error = string.Empty;
            if (!TryReadText(volume, sourcePath, out var text, out error))
            {
                return false;
            }

            return TrySaveText(volume, destinationPath, text, out error);
        }

        public bool TryRenameByCopy(Volume volume, string sourcePath, string destinationPath, out string error)
        {
            error = string.Empty;
            if (!TryDuplicate(volume, sourcePath, destinationPath, out error))
            {
                return false;
            }

            if (!TryDelete(volume, sourcePath, out error))
            {
                return false;
            }

            return true;
        }

        public bool TryRunScript(kOSProcessor processor, Volume sourceVolume, string sourcePath, bool debugMode, out string error)
        {
            error = string.Empty;
            try
            {
                if (processor == null)
                {
                    error = "No active kOS processor.";
                    return false;
                }

                if (string.Equals(processor.ProcessorMode.ToString(), "OFF", StringComparison.OrdinalIgnoreCase))
                {
                    processor.TogglePower();
                }

                var runPath = BuildAbsoluteRunPath(sourceVolume, sourcePath);

                var command = debugMode
                    ? string.Format("print \"[kOSMgr debug] running {0}\". runpath(\"{1}\").", sourcePath, runPath)
                    : string.Format("runpath(\"{0}\").", runPath);

                SendCommandToTerminal(processor, command);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public string GetDebugOutput(kOSProcessor? processor)
        {
            if (processor == null)
            {
                return string.Empty;
            }

            var now = Planetarium.GetUniversalTime();
            if (now - lastSnapshotTime < 0.2)
            {
                return string.Join("\n", outputLines);
            }

            lastSnapshotTime = now;

            try
            {
                var screen = processor.GetScreen();
                if (screen == null)
                {
                    return string.Empty;
                }

                var bufferProperty = screen.GetType().GetProperty("Buffer");
                if (bufferProperty == null)
                {
                    return string.Join("\n", outputLines);
                }

                var rawBuffer = bufferProperty.GetValue(screen, null) as System.Collections.IEnumerable;
                if (rawBuffer == null)
                {
                    return string.Join("\n", outputLines);
                }

                outputLines.Clear();
                foreach (var row in rawBuffer)
                {
                    if (row == null)
                    {
                        continue;
                    }

                    var line = row.ToString().TrimEnd('\0');
                    outputLines.Add(line);
                }

                while (outputLines.Count > 120)
                {
                    outputLines.RemoveAt(0);
                }

                return string.Join("\n", outputLines);
            }
            catch
            {
                return string.Join("\n", outputLines);
            }
        }

        public string DisplayVolumeName(Volume volume)
        {
            if (volume == null)
            {
                return "(null)";
            }

            var typeName = volume.GetType().Name;
            if (typeName.Equals("Archive", StringComparison.OrdinalIgnoreCase))
            {
                return "archive";
            }

            var name = volume.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return "local";
            }

            return name;
        }

        public string ComposePath(string directory, string fileName)
        {
            var normalizedName = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                return NormalizePath(directory);
            }

            var normalizedDirectory = NormalizePath(directory);
            if (string.IsNullOrEmpty(normalizedDirectory))
            {
                return normalizedName;
            }

            return normalizedDirectory + "/" + normalizedName;
        }

        private static string NormalizePath(string path)
        {
            var value = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (value.EndsWith("/", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 1);
            }

            return value;
        }

        private static string BuildAbsoluteRunPath(Volume volume, string path)
        {
            var normalized = NormalizePath(path);
            var volumeType = volume != null ? volume.GetType().Name : string.Empty;
            if (string.Equals(volumeType, "Archive", StringComparison.OrdinalIgnoreCase))
            {
                return "archive:/" + normalized;
            }

            return "0:/" + normalized;
        }

        private static void SendCommandToTerminal(kOSProcessor processor, string command)
        {
            var window = processor.GetWindow();
            if (window == null)
            {
                throw new InvalidOperationException("kOS terminal window is not available for this processor.");
            }

            foreach (var ch in command)
            {
                window.ProcessOneInputChar(ch, null, true);
            }

            window.ProcessOneInputChar('\r', null, true);
        }
    }
}
