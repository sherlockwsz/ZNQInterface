using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ZNQInterface.Services.Vision
{
    internal sealed record VisionImageFrame(
        bool Ok,
        string Channel,
        long FrameId,
        long? RequestId,
        double Timestamp,
        string SourceMode,
        string SourceName,
        string ImageStatus,
        BitmapImage? Image);

    /// <summary>
    /// 只负责Python TCP长度帧协议、连接重建和JPEG解码。
    /// </summary>
    internal sealed class VisionImageClient : IAsyncDisposable
    {
        private const int MaxMetadataBytes = 1024 * 1024;
        private const int MaxJpegBytes = 64 * 1024 * 1024;
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _client;
        private NetworkStream? _stream;

        public VisionImageClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task<VisionImageFrame> GetLatestAsync(
            string channel,
            CancellationToken cancellationToken)
        {
            if (channel != "screw" && channel != "coax" &&
                channel != "screw_preview" && channel != "coax_preview")
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            try
            {
                await EnsureConnectedAsync(cancellationToken)
                    .ConfigureAwait(false);
                NetworkStream stream = _stream
                    ?? throw new IOException("TCP图像连接不可用。");

                byte[] request = Encoding.UTF8.GetBytes($"GET {channel}\n");
                await stream.WriteAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                int metadataLength = await ReadInt32BigEndianAsync(
                    stream,
                    MaxMetadataBytes,
                    cancellationToken).ConfigureAwait(false);
                byte[] metadataBytes = await ReadExactAsync(
                    stream,
                    metadataLength,
                    cancellationToken).ConfigureAwait(false);
                int jpegLength = await ReadInt32BigEndianAsync(
                    stream,
                    MaxJpegBytes,
                    cancellationToken).ConfigureAwait(false);
                byte[] jpeg = await ReadExactAsync(
                    stream,
                    jpegLength,
                    cancellationToken).ConfigureAwait(false);

                using JsonDocument document = JsonDocument.Parse(metadataBytes);
                JsonElement root = document.RootElement;
                bool ok = GetBoolean(root, "ok", false);
                BitmapImage? bitmap = jpeg.Length == 0
                    ? null
                    : DecodeJpeg(jpeg);

                return new VisionImageFrame(
                    ok,
                    GetString(root, "channel"),
                    GetInt64(root, "frame_id", 0),
                    TryGetInt64(root, "request_id"),
                    GetDouble(root, "timestamp", 0.0),
                    GetString(root, "source_mode"),
                    GetString(root, "source_name"),
                    GetString(root, "image_status"),
                    bitmap);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await ResetConnectionAsync().ConfigureAwait(false);
                throw;
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client?.Connected == true && _stream != null)
            {
                return;
            }

            await ResetConnectionAsync().ConfigureAwait(false);
            TcpClient client = new TcpClient();
            try
            {
                await client.ConnectAsync(_host, _port, cancellationToken)
                    .ConfigureAwait(false);
                _client = client;
                _stream = client.GetStream();
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private async Task ResetConnectionAsync()
        {
            NetworkStream? stream = _stream;
            TcpClient? client = _client;
            _stream = null;
            _client = null;
            if (stream != null)
            {
                try { await stream.DisposeAsync().ConfigureAwait(false); }
                catch { }
            }
            client?.Dispose();
        }

        private static BitmapImage DecodeJpeg(byte[] jpeg)
        {
            BitmapImage bitmap = new BitmapImage();
            using MemoryStream memory = new MemoryStream(jpeg, writable: false);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static async Task<int> ReadInt32BigEndianAsync(
            Stream stream,
            int maximum,
            CancellationToken cancellationToken)
        {
            byte[] bytes = await ReadExactAsync(stream, 4, cancellationToken)
                .ConfigureAwait(false);
            int value = BinaryPrimitives.ReadInt32BigEndian(bytes);
            if (value < 0 || value > maximum)
            {
                throw new InvalidDataException($"非法TCP帧长度：{value}。");
            }
            return value;
        }

        private static async Task<byte[]> ReadExactAsync(
            Stream stream,
            int length,
            CancellationToken cancellationToken)
        {
            byte[] data = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int count = await stream.ReadAsync(
                    data.AsMemory(offset, length - offset),
                    cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("Python图像连接已关闭。");
                }
                offset += count;
            }
            return data;
        }

        private static string GetString(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static bool GetBoolean(
            JsonElement root, string name, bool fallback) =>
            root.TryGetProperty(name, out JsonElement value) &&
            (value.ValueKind == JsonValueKind.True ||
             value.ValueKind == JsonValueKind.False)
                ? value.GetBoolean()
                : fallback;

        private static long GetInt64(
            JsonElement root, string name, long fallback) =>
            TryGetInt64(root, name) ?? fallback;

        private static long? TryGetInt64(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number)
            {
                return null;
            }
            return value.TryGetInt64(out long result) ? result : null;
        }

        private static double GetDouble(
            JsonElement root, string name, double fallback)
        {
            if (!root.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind != JsonValueKind.Number)
            {
                return fallback;
            }
            return value.TryGetDouble(out double result) ? result : fallback;
        }

        public async ValueTask DisposeAsync()
        {
            await ResetConnectionAsync().ConfigureAwait(false);
        }
    }
}
