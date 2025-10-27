using System.Collections.Generic;

namespace ManifestBridge.Model
{
    public class @properties
    {
        public string? image { get; set; }
        public List<binding>? bindings { get; set; }
        public List<volume>? volumes { get; set; }
        public List<network>? networks { get; set; }
        public Dictionary<string, string>? environment { get; set; }
        public string? restart { get; set; }
        public resourceLimits? resources { get; set; }
        public List<string>? dependencies { get; set; }
        public List<string>? command { get; set; }
        public Dictionary<string, string>? additionalHosts { get; set; }
        public string? platform { get; set; }
    }
}