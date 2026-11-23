namespace ChatApp_Backend.RealTime.Options
{
    public class SocketServerOptions
    {
        public int Port { get; set; } = 5001;
        public int MaxMessageSize { get; set; } = 65536;
        public int HeartbeatIntervalSeconds { get; set; } = 30;
        public int HeartbeatTimeoutSeconds { get; set; } = 60;
        public int OutboundQueueCapacity { get; set; } = 256;
    }
}
