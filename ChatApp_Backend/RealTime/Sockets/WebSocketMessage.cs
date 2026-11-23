namespace ChatApp_Backend.RealTime.Sockets
{
    public sealed class WebSocketMessage
    {
        public WebSocketOpcode Opcode { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public ushort CloseStatus { get; init; } = 1000;
        public string? CloseReason { get; init; }
    }
}
