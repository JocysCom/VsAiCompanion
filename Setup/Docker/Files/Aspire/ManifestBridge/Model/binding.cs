namespace Aspire.ManifestBridge.Model
{
    public class binding
    {
        public string? name { get; set; }
        public string? protocol { get; set; }
        public int containerPort { get; set; }
        public int hostPort { get; set; }
    }
}