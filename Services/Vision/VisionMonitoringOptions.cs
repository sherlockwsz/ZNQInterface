using System;
using System.IO;
using System.Text.Json;

namespace ZNQInterface.Services.Vision
{
    /// <summary>
    /// Python TCP图像链路的唯一WPF配置入口。
    /// </summary>
    public sealed class VisionMonitoringOptions
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 50010;
        public int PreviewIntervalMs { get; set; } = 200;
        public int AdsPollIntervalMs { get; set; } = 100;
        public int DetectionDisplayDurationMs { get; set; } = 2000;
        public int PreviewStaleTimeoutMs { get; set; } = 1000;
        public int DetectionRetryCount { get; set; } = 10;
        public int DetectionRetryDelayMs { get; set; } = 100;
        public int[] ReconnectBackoffMs { get; set; } =
            new[] { 500, 1000, 2000, 5000 };

        public static VisionMonitoringOptions Load(string path)
        {
            VisionMonitoringOptions options = new VisionMonitoringOptions();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    options = JsonSerializer.Deserialize<VisionMonitoringOptions>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? options;
                }
            }
            catch
            {
                // 配置损坏时使用安全默认值，不能阻止HMI和ADS启动。
            }

            options.Host = string.IsNullOrWhiteSpace(options.Host)
                ? "127.0.0.1"
                : options.Host.Trim();
            options.Port = Math.Clamp(options.Port, 1, 65535);
            options.PreviewIntervalMs = Math.Max(50, options.PreviewIntervalMs);
            options.AdsPollIntervalMs = Math.Max(20, options.AdsPollIntervalMs);
            options.DetectionDisplayDurationMs =
                Math.Max(100, options.DetectionDisplayDurationMs);
            options.PreviewStaleTimeoutMs =
                Math.Max(options.PreviewIntervalMs * 2, options.PreviewStaleTimeoutMs);
            options.DetectionRetryCount = Math.Max(1, options.DetectionRetryCount);
            options.DetectionRetryDelayMs = Math.Max(20, options.DetectionRetryDelayMs);
            if (options.ReconnectBackoffMs == null ||
                options.ReconnectBackoffMs.Length == 0)
            {
                options.ReconnectBackoffMs = new[] { 500, 1000, 2000, 5000 };
            }
            for (int index = 0; index < options.ReconnectBackoffMs.Length; index++)
            {
                options.ReconnectBackoffMs[index] =
                    Math.Clamp(options.ReconnectBackoffMs[index], 100, 5000);
            }
            return options;
        }
    }
}
