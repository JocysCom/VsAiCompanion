using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Aspire.ManifestBridge;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            // Default manifest path when running from AppHost/bin/<cfg>/net8.0
            var defaultManifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\manifest.json"));

            // Optional: profile and resource selection
            // Arg0: profile (e.g., "local" or "prod")
            // Arg1: comma-separated resource filter (e.g., "n8n,firecrawl-api,qdrant")
            string? profile = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : Environment.GetEnvironmentVariable("ASPIRE_MANIFEST_PROFILE");

            string? resourceFilterArg = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
                ? args[1]
                : Environment.GetEnvironmentVariable("ASPIRE_RESOURCES");

            var resourceFilter = ParseCsvToSet(resourceFilterArg);

            // Load manifest via bridge
            string resolvedManifestPath;
            var root = ManifestLoader.Load(out resolvedManifestPath, defaultManifestPath, profile);
            Console.WriteLine($"Loading manifest: {resolvedManifestPath}");
            if (root.resources == null || root.resources.Count == 0)
            {
                Console.Error.WriteLine("Manifest contains no resources.");
                return 1;
            }

            // Default target resources for ACA alignment and local runs
            var defaultTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "n8n","firecrawl-redis","firecrawl-postgres","firecrawl-worker","firecrawl-api","playwright-service","qdrant"
            };

            // If a filter is provided, use it; otherwise use default targets
            var targets = (resourceFilter?.Count > 0) ? resourceFilter : defaultTargets;

            foreach (var kvp in root.resources)
            {
                var name = kvp.Key;
                if (!targets.Contains(name)) continue;

                var res = kvp.Value;
                var props = res?.properties;
                if (props == null) continue;

                Console.WriteLine($"--- {name} ---");
                Console.WriteLine($"type: {res?.type}");
                Console.WriteLine($"image: {props.image}");

                if (props.bindings != null)
                {
                    foreach (var b in props.bindings)
                    {
                        Console.WriteLine($"binding: {b.name} {b.protocol} host:{b.hostPort} -> container:{b.containerPort}");
                    }
                }

                if (props.volumes != null)
                {
                    foreach (var v in props.volumes)
                    {
                        Console.WriteLine($"volume: {v.name} -> {v.containerPath} ({v.type})");
                    }
                }

                if (props.environment != null)
                {
                    Console.WriteLine($"env vars: {props.environment.Count}");
                }

                if (props.dependencies != null && props.dependencies.Count > 0)
                {
                    Console.WriteLine($"depends on: {string.Join(", ", props.dependencies)}");
                }

                if (props.resources != null)
                {
                    Console.WriteLine($"limits: memory={props.resources.memory} swap={props.resources.memorySwap}");
                }

                if (props.additionalHosts != null && props.additionalHosts.Count > 0)
                {
                    Console.WriteLine($"additionalHosts: {string.Join(", ", props.additionalHosts.Keys)}");
                }

                if (!string.IsNullOrWhiteSpace(props.platform))
                {
                    Console.WriteLine($"platform: {props.platform}");
                }
            }

            Console.WriteLine("Manifest parsed successfully. Next: map bindings/env/volumes to Aspire container definitions and enable Azure ACA alignment.");
            Console.WriteLine("Tip: Set ASPIRE_RESOURCES=\"n8n,firecrawl-api,qdrant\" or pass as arg1 to focus local runs.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex);
            return 1;
        }
    }

    private static HashSet<string>? ParseCsvToSet(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return new HashSet<string>(
            csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase
        );
    }
}