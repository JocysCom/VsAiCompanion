using System.Text.Json;
using ManifestBridge;

namespace AspireTests
{
    [TestClass]
    public class ManifestTests
    {
        private static string ResolveBaseManifestPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\manifest.json"));
        }

        private static string ResolveOverlayPath(string manifestPath, string profile)
        {
            var dir = Path.GetDirectoryName(manifestPath)!;
            var nameNoExt = Path.GetFileNameWithoutExtension(manifestPath);
            var ext = Path.GetExtension(manifestPath);
            return Path.Combine(dir, $"{nameNoExt}.{profile}{ext}");
        }

        [TestMethod]
        public void BaseManifestLoadsAndContainsExpectedResources()
        {
            var manifestPath = ResolveBaseManifestPath();
            string resolved;
            var root = ManifestLoader.Load(out resolved, manifestPath, null);
            Assert.IsNotNull(root.resources, "resources should not be null");
            Assert.IsTrue(root.resources.Count > 0, "resources should not be empty");
            Assert.IsTrue(root.resources.ContainsKey("n8n"), "n8n should exist");
            Assert.IsTrue(root.resources.ContainsKey("firecrawl-api"), "firecrawl-api should exist");
            Assert.IsTrue(root.resources.ContainsKey("qdrant"), "qdrant should exist");
        }

        [TestMethod]
        public void LocalOverlayResolvesAndMergedContent()
        {
            var manifestPath = ResolveBaseManifestPath();
            var expectedOverlay = ResolveOverlayPath(manifestPath, "local");
            string resolved;
            var root = ManifestLoader.Load(out resolved, manifestPath, "local");
            Assert.AreEqual(expectedOverlay, resolved, true, "Resolved manifest path should be local overlay when present");
            Assert.IsNotNull(root.resources, "resources should not be null");
            Assert.IsTrue(root.resources.ContainsKey("firecrawl-api"), "firecrawl-api should exist");
            var api = root.resources["firecrawl-api"];
            Assert.IsNotNull(api.properties, "firecrawl-api properties should not be null");
            Assert.IsNotNull(api.properties.environment, "firecrawl-api environment should not be null");
            Assert.AreEqual("3002", api.properties.environment["PORT"], "firecrawl-api PORT should be 3002");
        }

        [TestMethod]
        public void RoundTripStructuralEqualityMatches()
        {
            var manifestPath = ResolveBaseManifestPath();
            var originalText = File.ReadAllText(manifestPath);

            string resolved;
            var root = ManifestLoader.Load(out resolved, manifestPath, null);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
            };

            var serialized = JsonSerializer.Serialize(root, jsonOptions);
            using var docOriginal = JsonDocument.Parse(originalText);
            using var docRoundtrip = JsonDocument.Parse(serialized);

            Assert.IsTrue(JsonEquals(docOriginal.RootElement, docRoundtrip.RootElement), "Structural JSON equality should hold for round-trip");
        }

        private static bool JsonEquals(JsonElement a, JsonElement b)
        {
            if (a.ValueKind != b.ValueKind) return false;
            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    var aProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in a.EnumerateObject()) aProps[p.Name] = p.Value;
                    var bProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in b.EnumerateObject()) bProps[p.Name] = p.Value;
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
                    if (a.TryGetInt64(out var ai) && b.TryGetInt64(out var bi)) return ai == bi;
                    if (a.TryGetDecimal(out var ad) && b.TryGetDecimal(out var bd)) return ad == bd;
                    return a.GetRawText() == b.GetRawText();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return a.GetBoolean() == b.GetBoolean();
                case JsonValueKind.Null:
                    return true;
                default:
                    return a.GetRawText() == b.GetRawText();
            }
        }
    }
}