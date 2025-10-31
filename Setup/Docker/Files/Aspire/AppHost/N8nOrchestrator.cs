using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using ManifestBridge;

namespace AppHost.Orchestrator
{
    public static class N8nOrchestrator
    {
        public static async Task<int> Deploy(string? profile = null, string? preferredEngine = null)
        {
            try
            {
                var defaultManifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\manifest.json"));
                string resolvedPath;
                var root = ManifestLoader.Load(out resolvedPath, defaultManifestPath, profile);
                if (root.resources == null || !root.resources.ContainsKey("n8n"))
                {
                    Console.Error.WriteLine("n8n resource not found in manifest.");
                    return 1;
                }
                var res = root.resources["n8n"];
                var props = res.properties;
                if (props == null)
                {
                    Console.Error.WriteLine("n8n properties missing.");
                    return 1;
                }

                var engineName = DetectEngine(preferredEngine);
                var enginePath = GetEnginePath(engineName);

                var image = props.image;
                if (string.IsNullOrWhiteSpace(image))
                {
                    Console.Error.WriteLine("Image is not specified");
                    return 1;
                }
                var containerName = "n8n";
                var hostPort = props.bindings?.FirstOrDefault()?.hostPort ?? 5678;
                var containerPort = props.bindings?.FirstOrDefault()?.containerPort ?? 5678;
                var volName = props.volumes?.FirstOrDefault()?.name ?? "n8n-data";
                var volPath = props.volumes?.FirstOrDefault()?.containerPath ?? "/home/node/.n8n";
                var restart = string.IsNullOrWhiteSpace(props.restart) ? "always" : props.restart;
                var env = props.environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var mem = props.resources?.memory;
                var memSwap = props.resources?.memorySwap;
                var extraHosts = props.additionalHosts ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                Console.WriteLine($"Engine: {engineName} ({enginePath})");

                // Pull image
                var pull = Run(enginePath, "pull", image);
                if (pull.exitCode != 0)
                {
                    Console.Error.WriteLine("Image pull failed: " + pull.stderr);
                    return 1;
                }

                // Remove existing container
                var ps = Run(enginePath, "ps", "-a", "--filter", $"name=^{containerName}$", "--format", "{{.ID}}");
                if (!string.IsNullOrWhiteSpace(ps.stdout))
                {
                    Run(enginePath, "stop", containerName);
                    Run(enginePath, "rm", "--force", containerName);
                }

                // Build run options
                var args = new List<string>();
                args.AddRange(new[] { "run", "--detach", "--name", containerName });
                args.AddRange(new[] { "--publish", $"{hostPort}:{containerPort}" });
                args.AddRange(new[] { "--volume", $"{volName}:{volPath}" });
                args.AddRange(new[] { "--restart", restart });
                if (!string.IsNullOrWhiteSpace(mem)) { args.AddRange(new[] { "--memory", mem }); }
                if (!string.IsNullOrWhiteSpace(memSwap)) { args.AddRange(new[] { "--memory-swap", memSwap }); }
                foreach (var kv in env) { args.AddRange(new[] { "--env", $"{kv.Key}={kv.Value}" }); }

                // Additional hosts
                if (extraHosts.TryGetValue("host.local", out var hostVal))
                {
                    var addHostValue = hostVal;
                    if (engineName.Equals("podman", StringComparison.OrdinalIgnoreCase))
                    {
                        var ip = GetPodmanHostIp(enginePath);
                        if (!string.IsNullOrWhiteSpace(ip)) addHostValue = ip;
                    }
                    args.AddRange(new[] { "--add-host", $"host.local:{addHostValue}" });
                }

                args.Add(image);

                // Start container
                var run = Run(enginePath, args.ToArray());
                if (run.exitCode != 0)
                {
                    Console.Error.WriteLine("Container run failed: " + run.stderr);
                    return 1;
                }

                await Task.Delay(30000);

                var tcpOk = TestTcp("localhost", hostPort, 5000);
                var httpOk = await TestHttp($"http://localhost:{hostPort}/", 15000);
                Console.WriteLine($"TCP:{tcpOk} HTTP:{httpOk}");
                if (!tcpOk || !httpOk)
                {
                    Console.Error.WriteLine("Connectivity tests failed.");
                    return 2;
                }

                // Validate core config via inspect
                var ok = ValidateAgainstManifest(enginePath, containerName, image, hostPort, containerPort, volName, volPath, env, restart);
                Console.WriteLine($"Validation: {(ok ? "PASS" : "FAIL")}");
                return ok ? 0 : 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 10;
            }
        }

        public static bool ValidateAgainstManifest(
            string engine,
            string name,
            string image,
            int hostPort,
            int containerPort,
            string volName,
            string volPath,
            IDictionary<string, string> env,
            string restart)
        {
            var ins = Run(engine, "inspect", name);
            if (ins.exitCode != 0 || string.IsNullOrWhiteSpace(ins.stdout)) return false;
            using var doc = JsonDocument.Parse(ins.stdout);
            var root = doc.RootElement;
            var obj = root.ValueKind == JsonValueKind.Array ? root[0] : root;

            var cfgImg = obj.GetProperty("Config").GetProperty("Image").GetString();
            var pb = obj.GetProperty("HostConfig").GetProperty("PortBindings");
            var wantKey = $"{containerPort}/tcp";
            var gotHost = pb.TryGetProperty(wantKey, out var arr) && arr.GetArrayLength() > 0
                ? arr[0].GetProperty("HostPort").GetString()
                : null;
            var mounts = obj.GetProperty("Mounts");
            var volOk = false;
            foreach (var m in mounts.EnumerateArray())
            {
                if (m.GetProperty("Type").GetString() == "volume"
                    && m.GetProperty("Destination").GetString() == volPath
                    && m.GetProperty("Name").GetString() == volName)
                {
                    volOk = true;
                    break;
                }
            }
            var rc = obj.GetProperty("HostConfig").GetProperty("RestartPolicy").GetProperty("Name").GetString();

            var envList = obj.GetProperty("Config").GetProperty("Env");
            var envMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in envList.EnumerateArray())
            {
                var s = e.GetString() ?? "";
                var idx = s.IndexOf('=');
                if (idx > 0) envMap[s.Substring(0, idx)] = s.Substring(idx + 1);
            }
            var envOk = true;
            foreach (var kv in env)
            {
                if (!envMap.TryGetValue(kv.Key, out var v) || v != kv.Value) { envOk = false; break; }
            }

            return (cfgImg == image) && (gotHost == hostPort.ToString()) && volOk && (rc == restart) && envOk;
        }

        private static string DetectEngine(string? preferred)
        {
            var p = (preferred ?? "").ToLowerInvariant();
            if (p == "podman" || p == "docker") return p;
            var pod = GetEnginePathOrNull("podman");
            if (!string.IsNullOrEmpty(pod)) return "podman";
            var doc = GetEnginePathOrNull("docker");
            if (!string.IsNullOrEmpty(doc)) return "docker";
            throw new InvalidOperationException("No container engine found (podman or docker).");
        }

        private static string GetEnginePath(string name)
        {
            var path = GetEnginePathOrNull(name);
            if (string.IsNullOrEmpty(path)) throw new FileNotFoundException($"{name} executable not found.");
            return path;
        }

        private static string GetEnginePathOrNull(string name)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = Process.Start(psi);
                var s = p!.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                if (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(s))
                {
                    var first = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first)) return first;
                }
            }
            catch { }
            return null!;
        }

        private static (int exitCode, string stdout, string stderr) Run(string file, params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = string.Join(' ', args.Select(a => QuoteArg(a))),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            var so = p!.StandardOutput.ReadToEnd();
            var se = p!.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, so, se);
        }

        private static string QuoteArg(string a)
        {
            if (string.IsNullOrEmpty(a)) return "\"\"";
            if (a.IndexOfAny(new[] { ' ', '\t', '\n', '"', '\'' }) >= 0) return "\"" + a.Replace("\"", "\\\"") + "\"";
            return a;
        }

        private static string GetPodmanHostIp(string enginePath)
        {
            try
            {
                // Use a shell inside the VM to execute the pipeline reliably
                var res = Run(enginePath, "machine", "ssh", "sh", "-lc", "grep nameserver /etc/resolv.conf | cut -d ' ' -f2");
                var s = (res.stdout ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var line = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    return (line ?? "").Trim();
                }
            }
            catch { }
            return "host-gateway";
        }

        private static bool TestTcp(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                if (!task.Wait(timeoutMs)) return false;
                return client.Connected;
            }
            catch { return false; }
        }

        private static async Task<bool> TestHttp(string uri, int timeoutMs)
        {
            try
            {
                using var c = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
                using var r = await c.GetAsync(uri);
                return (int)r.StatusCode == 200;
            }
            catch { return false; }
        }

        public static int ExportRuntimeManifest(string engine, string name, string outputPath)
        {
            try
            {
                var ins = Run(engine, "inspect", name);
                if (ins.exitCode != 0) { Console.Error.WriteLine("inspect failed"); return 1; }
                File.WriteAllText(outputPath, ins.stdout, Encoding.UTF8);
                Console.WriteLine($"Wrote: {outputPath}");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 2; }
        }
    }
}