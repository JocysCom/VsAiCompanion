using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using ManifestBridge.Model;

namespace ManifestBridge
{
    public static class ManifestLoader
    {
        public static manifest Load(out string resolvedManifestPath, string? manifestPath = null, string? profile = null)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                // Default path: Files/Aspire/manifest.json (relative to AppHost bin folder: bin\<cfg>\<tfm>)
                // From AppHost/bin/<cfg>/<tfm> go up four levels to reach Files/Aspire/manifest.json
                // bin -> AppHost -> Aspire -> Files -> manifest.json
                manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\manifest.json"));
            }

            var basePath = manifestPath!;
            string? overlayPath = null;

            if (!string.IsNullOrWhiteSpace(profile))
            {
                var dir = Path.GetDirectoryName(manifestPath)!;
                var fileName = Path.GetFileName(manifestPath);
                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var candidate = Path.Combine(dir, $"{nameNoExt}.{profile}{ext}");
                if (File.Exists(candidate))
                {
                    overlayPath = candidate;
                }
            }

            if (!File.Exists(basePath))
            {
                throw new FileNotFoundException($"Manifest file not found: {basePath}");
            }

            resolvedManifestPath = overlayPath ?? basePath;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var jsonBase = File.ReadAllText(basePath);
            var baseRoot = JsonSerializer.Deserialize<manifest>(jsonBase, options)
                          ?? throw new InvalidOperationException("Failed to parse base manifest.");

            if (overlayPath == null)
            {
                return baseRoot;
            }

            var jsonOverlay = File.ReadAllText(overlayPath);
            var overlayRoot = JsonSerializer.Deserialize<manifest>(jsonOverlay, options)
                             ?? throw new InvalidOperationException("Failed to parse overlay manifest.");

            return Merge(baseRoot, overlayRoot);
        }

        public static resource? GetResource(manifest root, string name)
        {
            if (root.resources == null)
                return null;

            return root.resources.TryGetValue(name, out var res) ? res : null;
        }

        public static IEnumerable<KeyValuePair<string, resource>> EnumerateContainerResources(manifest root)
        {
            if (root.resources == null)
                yield break;

            foreach (var kvp in root.resources)
                yield return kvp;
        }

        private static manifest Merge(manifest baseRoot, manifest overlay)
        {
            // Merge schemaVersion (optional)
            if (!string.IsNullOrWhiteSpace(overlay.schemaVersion))
                baseRoot.schemaVersion = overlay.schemaVersion;

            // Merge resources (add/override)
            if (overlay.resources != null)
            {
                if (baseRoot.resources == null)
                    baseRoot.resources = new Dictionary<string, resource>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in overlay.resources)
                {
                    var name = kvp.Key;
                    var ovRes = kvp.Value;

                    if (baseRoot.resources.TryGetValue(name, out var baseRes))
                    {
                        // Merge resource type
                        if (!string.IsNullOrWhiteSpace(ovRes.type))
                            baseRes.type = ovRes.type;

                        // Merge properties (deep)
                        if (ovRes.properties != null)
                        {
                            if (baseRes.properties == null)
                                baseRes.properties = new properties();

                            MergeProperties(baseRes.properties, ovRes.properties);
                        }
                    }
                    else
                    {
                        // New resource entirely from overlay
                        baseRoot.resources[name] = ovRes;
                    }
                }
            }

            return baseRoot;
        }

        private static void MergeProperties(properties target, properties overlay)
        {
            // Scalars override when present
            if (!string.IsNullOrWhiteSpace(overlay.image))
                target.image = overlay.image;

            if (!string.IsNullOrWhiteSpace(overlay.restart))
                target.restart = overlay.restart;

            if (!string.IsNullOrWhiteSpace(overlay.platform))
                target.platform = overlay.platform;

            // Lists replace when provided
            if (overlay.bindings != null)
                target.bindings = overlay.bindings;

            if (overlay.volumes != null)
                target.volumes = overlay.volumes;

            if (overlay.dependencies != null)
                target.dependencies = overlay.dependencies;

            if (overlay.networks != null)
                target.networks = overlay.networks;

            if (overlay.command != null)
                target.command = overlay.command;

            // Dictionaries: merge keys, overlay wins
            if (overlay.environment != null)
            {
                if (target.environment == null)
                    target.environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in overlay.environment)
                    target.environment[kv.Key] = kv.Value;
            }

            if (overlay.additionalHosts != null)
            {
                if (target.additionalHosts == null)
                    target.additionalHosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in overlay.additionalHosts)
                    target.additionalHosts[kv.Key] = kv.Value;
            }

            // Resource limits: per-field overrides
            if (overlay.resources != null)
            {
                if (target.resources == null)
                    target.resources = new resourceLimits();

                if (!string.IsNullOrWhiteSpace(overlay.resources.memory))
                    target.resources.memory = overlay.resources.memory;

                if (!string.IsNullOrWhiteSpace(overlay.resources.memorySwap))
                    target.resources.memorySwap = overlay.resources.memorySwap;
            }
        }
    }
}