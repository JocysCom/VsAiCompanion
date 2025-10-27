using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Aspire.ManifestBridge.Model;

internal static class Program
{
    // Entry: round-trip manifest.json, write manifest.roundtrip.json, compare byte-wise and structural equality
    // Usage:
    //   dotnet run --project Files/Aspire/AspireBridgeTests/AspireBridgeTests.csproj
    //   dotnet run --project Files/Aspire/AspireBridgeTests/AspireBridgeTests.csproj -- Files/Aspire/manifest.json
    //   dotnet run --project Files/Aspire/AspireBridgeTests/AspireBridgeTests.csproj -- Files/Aspire/manifest.json local
    static int Main(string[] args)
    {
        try
        {
            // Resolve base manifest path relative to build output folder: Files/Aspire/manifest.json
            var defaultManifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\manifest.json"));
            var manifestPath = defaultManifestPath;

            // Optional overrides: arg0 manifest path, arg1 profile overlay name (e.g., "local" / "prod")
            string? profile = null;
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                manifestPath = args[0];
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                profile = args[1];

            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"Manifest file not found: {manifestPath}");
                return 1;
            }

            // If profile provided and overlay exists, load overlay for merge test; otherwise base only
            string? overlayPath = null;
            if (!string.IsNullOrWhiteSpace(profile))
            {
                var dir = Path.GetDirectoryName(manifestPath)!;
                var fileName = Path.GetFileName(manifestPath);
                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var candidate = Path.Combine(dir, $"{nameNoExt}.{profile}{ext}");
                if (File.Exists(candidate))
                    overlayPath = candidate;
                else
                    Console.WriteLine($"Profile overlay not found: {candidate}. Proceeding without overlay.");
            }

            // Read original content exactly as bytes and text
            var originalBytes = File.ReadAllBytes(manifestPath);
            var originalText = File.ReadAllText(manifestPath);

            // Serializer options:
            // - PropertyNameCaseInsensitive: allows lowercase model properties to match JSON keys
            // - WriteIndented: pretty-print output (System.Text.Json uses 2-space indent; original uses wider indent)
            // Note: System.Text.Json does not provide indent width customization, so formatting will differ (most similar achievable is WriteIndented = true).
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            // Deserialize base manifest into lowercase model (simple, attribute-free)
            manifest baseModel;
            try
            {
                baseModel = JsonSerializer.Deserialize<manifest>(originalText, jsonOptions)
                            ?? throw new InvalidOperationException("Deserialized base manifest is null.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to deserialize base manifest: {ex.Message}");
                return 1;
            }

            // If overlay present, deserialize and merge for alternate round-trip test
            manifest? mergedModel = null;
            if (overlayPath != null)
            {
                var overlayText = File.ReadAllText(overlayPath);
                var overlayModel = JsonSerializer.Deserialize<manifest>(overlayText, jsonOptions)
                                  ?? throw new InvalidOperationException("Deserialized overlay manifest is null.");
                mergedModel = Merge(baseModel, overlayModel);
            }

            // Serialize round-trip outputs
            var roundtripBaseText = JsonSerializer.Serialize(baseModel, jsonOptions);
            string roundtripMergedText = mergedModel != null
                ? JsonSerializer.Serialize(mergedModel, jsonOptions)
                : roundtripBaseText;

            // Write round-trip files alongside original for inspection
            var baseDir = Path.GetDirectoryName(manifestPath)!;
            var baseRoundtripPath = Path.Combine(baseDir, "manifest.roundtrip.json");
            File.WriteAllText(baseRoundtripPath, roundtripBaseText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            if (mergedModel != null)
            {
                var mergedRoundtripPath = Path.Combine(baseDir, $"manifest.{profile}.roundtrip.json");
                File.WriteAllText(mergedRoundtripPath, roundtripMergedText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine($"Wrote merged round-trip: {mergedRoundtripPath}");
            }

            Console.WriteLine($"Wrote round-trip: {baseRoundtripPath}");

            // Byte-wise equality check (exact formatting, whitespace, order)
            var roundtripBaseBytes = Encoding.UTF8.GetBytes(roundtripBaseText);
            bool byteEqual = ByteArrayEqual(originalBytes, roundtripBaseBytes);

            // Structural JSON equality (ignores object property order and formatting; compares values)
            using var docOriginal = JsonDocument.Parse(originalText);
            using var docRoundtrip = JsonDocument.Parse(roundtripBaseText);
            bool structuralEqual = JsonEquals(docOriginal.RootElement, docRoundtrip.RootElement);

            Console.WriteLine("Round-trip comparison results:");
            Console.WriteLine($"- Byte-wise identical: {byteEqual}");
            Console.WriteLine($"- Structural equality: {structuralEqual}");

            if (!byteEqual)
            {
                Console.WriteLine("Note: System.Text.Json uses 2-space indentation for WriteIndented and may reorder dictionary entries;");
                Console.WriteLine("      the original manifest appears to use wider indentation and specific key orders. Exact byte equality is not guaranteed.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex);
            return 1;
        }
    }

    // Merge overlay into base manifest (lowercase model), following simple rules
    private static manifest Merge(manifest baseRoot, manifest overlay)
    {
        // schemaVersion override
        if (!string.IsNullOrWhiteSpace(overlay.schemaVersion))
            baseRoot.schemaVersion = overlay.schemaVersion;

        // resources merge/add
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
                    if (!string.IsNullOrWhiteSpace(ovRes.type))
                        baseRes.type = ovRes.type;

                    if (ovRes.properties != null)
                    {
                        if (baseRes.properties == null)
                            baseRes.properties = new properties();
                        MergeProperties(baseRes.properties, ovRes.properties);
                    }
                }
                else
                {
                    baseRoot.resources[name] = ovRes;
                }
            }
        }

        return baseRoot;
    }

    private static void MergeProperties(properties target, properties overlay)
    {
        // Scalars
        if (!string.IsNullOrWhiteSpace(overlay.image))
            target.image = overlay.image;
        if (!string.IsNullOrWhiteSpace(overlay.restart))
            target.restart = overlay.restart;
        if (!string.IsNullOrWhiteSpace(overlay.platform))
            target.platform = overlay.platform;

        // Lists replace
        if (overlay.bindings != null)     target.bindings = overlay.bindings;
        if (overlay.volumes != null)      target.volumes = overlay.volumes;
        if (overlay.dependencies != null) target.dependencies = overlay.dependencies;
        if (overlay.networks != null)     target.networks = overlay.networks;
        if (overlay.command != null)      target.command = overlay.command;

        // Dictionaries merge
        if (overlay.environment != null)
        {
            target.environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in overlay.environment)
                target.environment[kv.Key] = kv.Value;
        }
        if (overlay.additionalHosts != null)
        {
            target.additionalHosts ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in overlay.additionalHosts)
                target.additionalHosts[kv.Key] = kv.Value;
        }

        // Resource limits
        if (overlay.resources != null)
        {
            target.resources ??= new resourceLimits();
            if (!string.IsNullOrWhiteSpace(overlay.resources.memory))
                target.resources.memory = overlay.resources.memory;
            if (!string.IsNullOrWhiteSpace(overlay.resources.memorySwap))
                target.resources.memorySwap = overlay.resources.memorySwap;
        }
    }

    private static bool ByteArrayEqual(byte[] a, byte[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    // Structural JSON comparison ignoring object property order and whitespace
    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var aProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in a.EnumerateObject())
                    aProps[p.Name] = p.Value;

                var bProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in b.EnumerateObject())
                    bProps[p.Name] = p.Value;

                if (aProps.Count != bProps.Count) return false;
                foreach (var kv in aProps)
                {
                    if (!bProps.TryGetValue(kv.Key, out var bVal)) return false;
                    if (!JsonEquals(kv.Value, bVal)) return false;
                }
                return true;

            case JsonValueKind.Array:
                var aArr = a.EnumerateArray();
                var bArr = b.EnumerateArray();
                var aList = new List<JsonElement>(aArr);
                var bList = new List<JsonElement>(bArr);
                if (aList.Count != bList.Count) return false;
                for (int i = 0; i < aList.Count; i++)
                {
                    if (!JsonEquals(aList[i], bList[i])) return false;
                }
                return true;

            case JsonValueKind.String:
                return a.GetString() == b.GetString();

            case JsonValueKind.Number:
                // Compare numeric value; use decimal when possible to preserve precision of integers
                if (a.TryGetInt64(out var ai) && b.TryGetInt64(out var bi)) return ai == bi;
                if (a.TryGetDecimal(out var ad) && b.TryGetDecimal(out var bd)) return ad == bd;
                // Fallback to raw text match if numeric parsing fails
                return a.GetRawText() == b.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();

            case JsonValueKind.Null:
                return true;

            default:
                // Undefined or other kinds: compare raw text
                return a.GetRawText() == b.GetRawText();
        }
    }
}