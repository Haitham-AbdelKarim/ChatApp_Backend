using ChatApp_Backend.RealTime.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ChatApp_Backend.RealTime.Sockets
{
    public sealed class SocketServerHostedService : BackgroundService
    {
        private readonly SocketServerOptions options;
        private readonly ILogger<SocketServerHostedService> logger;
        private readonly TokenValidationParameters tokenValidationParameters;
        private Socket? listener;

        public SocketServerHostedService(IOptions<SocketServerOptions> options, ILogger<SocketServerHostedService> logger, TokenValidationParameters tokenValidationParameters)
        {
            this.options = options.Value;
            this.logger = logger;
            this.tokenValidationParameters = tokenValidationParameters;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, options.Port));
            listener.Listen(128);

            logger.LogInformation("Raw socket server listening on 127.0.0.1:{Port}", options.Port);

            while (!stoppingToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await listener.AcceptAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Accept failed");
                    continue;
                }

                _ = HandleClientAsync(client, stoppingToken);
            }
        }

        private async Task HandleClientAsync(Socket client, CancellationToken stoppingToken)
        {
            try
            {
                var handshake = await TryHandshakeAsync(client, stoppingToken);
                if (!handshake.Success)
                {
                    return;
                }

                string userId = handshake.UserId;
                logger.LogInformation("WebSocket client authenticated: {UserId}", userId);

                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                CancellationToken connectionToken = connectionCts.Token;

                var reader = new WebSocketFrameReader(options.MaxMessageSize);
                var buffer = new byte[4096];

                object pongLock = new();
                DateTime lastPong = DateTime.UtcNow;

                Task heartbeatTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!connectionToken.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(options.HeartbeatIntervalSeconds), connectionToken);

                            lock (pongLock)
                            {
                                if (DateTime.UtcNow - lastPong > TimeSpan.FromSeconds(options.HeartbeatTimeoutSeconds))
                                {
                                    connectionCts.Cancel();
                                    return;
                                }
                            }

                            try
                            {
                                await client.SendAsync(WebSocketFrameWriter.CreatePingFrame(), SocketFlags.None, connectionToken);
                            }
                            catch
                            {
                                connectionCts.Cancel();
                                return;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // expected on shutdown/timeout
                    }
                }, connectionToken);

                try
                {
                    while (!connectionToken.IsCancellationRequested)
                    {
                        int read = await client.ReceiveAsync(buffer, SocketFlags.None, connectionToken);
                        if (read == 0)
                        {
                            break;
                        }

                        reader.Feed(buffer.AsSpan(0, read));

                        while (true)
                        {
                            bool ok = reader.TryReadMessage(out WebSocketMessage? message, out string? error);
                            if (!ok)
                            {
                                if (error != null)
                                {
                                    await client.SendAsync(WebSocketFrameWriter.CreateCloseFrame(1002, error), SocketFlags.None, connectionToken);
                                }
                                break;
                            }

                            if (message == null)
                            {
                                break;
                            }

                            if (message.Opcode == WebSocketOpcode.Close)
                            {
                                await client.SendAsync(WebSocketFrameWriter.CreateCloseFrame(message.CloseStatus, message.CloseReason), SocketFlags.None, connectionToken);
                                return;
                            }

                            if (message.Opcode == WebSocketOpcode.Ping)
                            {
                                await client.SendAsync(WebSocketFrameWriter.CreatePongFrame(message.Payload), SocketFlags.None, connectionToken);
                                continue;
                            }

                            if (message.Opcode == WebSocketOpcode.Pong)
                            {
                                lock (pongLock)
                                {
                                    lastPong = DateTime.UtcNow;
                                }
                                continue;
                            }

                            if (message.Opcode == WebSocketOpcode.Text)
                            {
                                string text = System.Text.Encoding.UTF8.GetString(message.Payload);
                                await client.SendAsync(WebSocketFrameWriter.CreateTextFrame("echo: " + text), SocketFlags.None, connectionToken);
                            }
                        }
                    }
                }
                finally
                {
                    connectionCts.Cancel();
                    try { await heartbeatTask; } catch (OperationCanceledException) { }
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (SocketException)
            {
                // client disconnected abruptly
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Client handling failed");
            }
            finally
            {
                try { client.Shutdown(SocketShutdown.Both); } catch { }
                client.Close();
            }
        }

        private async Task<(bool Success, string UserId)> TryHandshakeAsync(Socket client, CancellationToken stoppingToken)
        {
            string userId = string.Empty;
            var accumulated = new List<byte>(1024);
            var readBuffer = new byte[1024];

            while (!stoppingToken.IsCancellationRequested)
            {
                int read = await client.ReceiveAsync(readBuffer, SocketFlags.None, stoppingToken);
                if (read == 0)
                {
                    return (false, string.Empty);
                }

                accumulated.AddRange(readBuffer.AsSpan(0, read).ToArray());

                if (accumulated.Count > 8192)
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, "Request headers too large"), SocketFlags.None, stoppingToken);
                    return (false, string.Empty);
                }

                bool parsed = WebSocketHandshake.TryParseRequest(
                    accumulated.ToArray(),
                    out WebSocketUpgradeRequest? request,
                    out int consumedBytes,
                    out string? error);

                if (error != null)
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, error), SocketFlags.None, stoppingToken);
                    return (false, string.Empty);
                }

                if (request == null)
                {
                    continue; // need more data
                }

                if (!WebSocketHandshake.ValidateProtocol(request, out error))
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, error ?? "Invalid handshake"), SocketFlags.None, stoppingToken);
                    return (false, string.Empty);
                }

                string token = ExtractToken(request.Query);
                if (string.IsNullOrWhiteSpace(token))
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(401, "Missing access token"), SocketFlags.None, stoppingToken);
                    return (false, string.Empty);
                }

                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var principal = handler.ValidateToken(token, tokenValidationParameters, out _);
                    userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        await client.SendAsync(WebSocketHandshake.BuildErrorResponse(401, "Invalid access token"), SocketFlags.None, stoppingToken);
                        return (false, string.Empty);
                    }
                }
                catch
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(401, "Invalid or expired access token"), SocketFlags.None, stoppingToken);
                    return (false, string.Empty);
                }

                string acceptKey = WebSocketHandshake.ComputeAcceptKey(request.SecWebSocketKey!);
                await client.SendAsync(WebSocketHandshake.BuildSuccessResponse(acceptKey), SocketFlags.None, stoppingToken);
                return (true, userId);
            }

            return (false, string.Empty);
        }

        private static string ExtractToken(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;
            foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                string key = Uri.UnescapeDataString(kv[0]);
                if (key.Equals("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("access_token", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            return string.Empty;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try { listener?.Close(); } catch { }
            await base.StopAsync(cancellationToken);
        }
    }
}
