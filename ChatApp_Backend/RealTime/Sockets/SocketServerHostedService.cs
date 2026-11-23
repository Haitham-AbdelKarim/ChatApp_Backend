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
                var buffer = new byte[4096];
                while (!stoppingToken.IsCancellationRequested)
                {
                    int read = await client.ReceiveAsync(buffer, SocketFlags.None, stoppingToken);
                    if (read == 0)
                    {
                        break;
                    }

                    // Phase 1 milestone: echo the received bytes back.
                    await client.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, stoppingToken);
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

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try { listener?.Close(); } catch { }
            await base.StopAsync(cancellationToken);
        }
    }
}
