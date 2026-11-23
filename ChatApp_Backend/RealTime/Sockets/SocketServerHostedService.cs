using ChatApp_Backend.RealTime.Options;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace ChatApp_Backend.RealTime.Sockets
{
    public sealed class SocketServerHostedService : BackgroundService
    {
        private readonly SocketServerOptions options;
        private readonly ILogger<SocketServerHostedService> logger;
        private Socket? listener;

        public SocketServerHostedService(IOptions<SocketServerOptions> options, ILogger<SocketServerHostedService> logger)
        {
            this.options = options.Value;
            this.logger = logger;
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
                if (!await TryHandshakeAsync(client, stoppingToken))
                {
                    return;
                }

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

        private async Task<bool> TryHandshakeAsync(Socket client, CancellationToken stoppingToken)
        {
            var accumulated = new List<byte>(1024);
            var readBuffer = new byte[1024];

            while (!stoppingToken.IsCancellationRequested)
            {
                int read = await client.ReceiveAsync(readBuffer, SocketFlags.None, stoppingToken);
                if (read == 0)
                {
                    return false;
                }

                accumulated.AddRange(readBuffer.AsSpan(0, read).ToArray());

                if (accumulated.Count > 8192)
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, "Request headers too large"), SocketFlags.None, stoppingToken);
                    return false;
                }

                bool parsed = WebSocketHandshake.TryParseRequest(
                    accumulated.ToArray(),
                    out WebSocketUpgradeRequest? request,
                    out int consumedBytes,
                    out string? error);

                if (error != null)
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, error), SocketFlags.None, stoppingToken);
                    return false;
                }

                if (request == null)
                {
                    continue; // need more data
                }

                if (!WebSocketHandshake.ValidateProtocol(request, out error))
                {
                    await client.SendAsync(WebSocketHandshake.BuildErrorResponse(400, error ?? "Invalid handshake"), SocketFlags.None, stoppingToken);
                    return false;
                }

                string acceptKey = WebSocketHandshake.ComputeAcceptKey(request.SecWebSocketKey!);
                await client.SendAsync(WebSocketHandshake.BuildSuccessResponse(acceptKey), SocketFlags.None, stoppingToken);
                return true;
            }

            return false;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try { listener?.Close(); } catch { }
            await base.StopAsync(cancellationToken);
        }
    }
}
