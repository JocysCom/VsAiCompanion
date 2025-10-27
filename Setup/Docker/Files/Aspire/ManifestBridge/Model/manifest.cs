using System.Collections.Generic;

namespace ManifestBridge.Model
{
    public class @manifest
    {
        public string? schemaVersion { get; set; }
        public Dictionary<string, resource>? resources { get; set; }
    }
}