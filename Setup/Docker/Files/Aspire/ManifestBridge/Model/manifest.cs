using System.Collections.Generic;

namespace Aspire.ManifestBridge.Model
{
    public class manifest
    {
        public string? schemaVersion { get; set; }
        public Dictionary<string, resource>? resources { get; set; }
    }
}