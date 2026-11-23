using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ChatApp_Backend.RealTime.Sockets
{
    public sealed class WebSocketUpgradeRequest
    {
        public string Method { get; init; } = string.Empty;
        public string Target { get; init; } = "/";
        public string Path { get; init; } = "/";
        public string Query { get; init; } = string.Empty;
        public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetHeader(string name) =>
            Headers.TryGetValue(name, out var value) ? value : null;

        public string? SecWebSocketKey => GetHeader("Sec-WebSocket-Key");
    }

    public static class WebSocketHandshake
    {
        public const string MagicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static bool TryParseRequest(
            ReadOnlySpan<byte> buffer,
            out WebSocketUpgradeRequest? request,
            out int consumedBytes,
            out string? error)
        {
            request = null;
            consumedBytes = 0;
            error = null;

            int terminator = IndexOfTerminator(buffer);
            if (terminator < 0)
            {
                if (buffer.Length >= 8192)
                {
                    error = "Request headers too large";
                    return false;
                }
                return false; // need more data
            }

            consumedBytes = terminator + 4;
            string headerText = Encoding.ASCII.GetString(buffer[..terminator]);

            string[] lines = headerText.Split("\r\n", StringSplitOptions.None);
            if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            {
                error = "Missing request line";
                return false;
            }

            string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2)
            {
                error = "Malformed request line";
                return false;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string name = line[..colon].Trim();
                string value = line[(colon + 1)..].Trim();
                headers[name] = value;
            }

            string target = requestLine[1];
            int queryIndex = target.IndexOf('?');
            string path = queryIndex >= 0 ? target[..queryIndex] : target;
            string query = queryIndex >= 0 ? target[(queryIndex + 1)..] : string.Empty;

            request = new WebSocketUpgradeRequest
            {
                Method = requestLine[0],
                Target = target,
                Path = path,
                Query = query,
                Headers = headers
            };

            return true;
        }

        public static bool ValidateProtocol(WebSocketUpgradeRequest request, out string? error)
        {
            error = null;

            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                error = "Handshake must use GET";
                return false;
            }

            string? upgrade = request.GetHeader("Upgrade");
            if (!string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                error = "Missing or invalid Upgrade header";
                return false;
            }

            string? connection = request.GetHeader("Connection");
            if (connection == null || !connection.Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                error = "Missing or invalid Connection header";
                return false;
            }

            string? version = request.GetHeader("Sec-WebSocket-Version");
            if (!string.Equals(version, "13", StringComparison.Ordinal))
            {
                error = "Unsupported WebSocket version";
                return false;
            }

            string? key = request.SecWebSocketKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Missing Sec-WebSocket-Key";
                return false;
            }

            return true;
        }

        public static string ComputeAcceptKey(string clientKey)
        {
            using SHA1 sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(clientKey + MagicGuid));
            return Convert.ToBase64String(hash);
        }

        public static byte[] BuildSuccessResponse(string acceptKey)
        {
            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                "\r\n";
            return Encoding.ASCII.GetBytes(response);
        }

        public static byte[] BuildErrorResponse(int statusCode, string reason)
        {
            string statusText = statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                _ => "Error"
            };
            string response =
                $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "\r\n";
            return Encoding.ASCII.GetBytes(response);
        }

        private static int IndexOfTerminator(ReadOnlySpan<byte> buffer)
        {
            for (int i = 0; i + 3 < buffer.Length; i++)
            {
                if (buffer[i] == (byte)'\r' && buffer[i + 1] == (byte)'\n' &&
                    buffer[i + 2] == (byte)'\r' && buffer[i + 3] == (byte)'\n')
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
