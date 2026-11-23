using System.Text;

namespace ChatApp_Backend.RealTime.Sockets
{
    public static class WebSocketFrameWriter
    {
        public static byte[] CreateFrame(WebSocketOpcode opcode, ReadOnlySpan<byte> payload, bool fin = true)
        {
            using var ms = new MemoryStream();
            ms.WriteByte((byte)((fin ? 0x80 : 0x00) | (byte)opcode));

            int length = payload.Length;
            if (length <= 125)
            {
                ms.WriteByte((byte)length);
            }
            else if (length <= ushort.MaxValue)
            {
                ms.WriteByte(126);
                ms.WriteByte((byte)(length >> 8));
                ms.WriteByte((byte)(length & 0xFF));
            }
            else
            {
                ms.WriteByte(127);
                for (int i = 7; i >= 0; i--)
                {
                    ms.WriteByte((byte)(length >> (i * 8)));
                }
            }

            if (length > 0)
            {
                ms.Write(payload);
            }

            return ms.ToArray();
        }

        public static byte[] CreateTextFrame(string text) =>
            CreateFrame(WebSocketOpcode.Text, Encoding.UTF8.GetBytes(text ?? string.Empty));

        public static byte[] CreatePingFrame(ReadOnlySpan<byte> payload = default) =>
            CreateFrame(WebSocketOpcode.Ping, payload);

        public static byte[] CreatePongFrame(ReadOnlySpan<byte> payload = default) =>
            CreateFrame(WebSocketOpcode.Pong, payload);

        public static byte[] CreateCloseFrame(ushort statusCode, string? reason = null)
        {
            byte[] reasonBytes = string.IsNullOrEmpty(reason) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(reason);
            byte[] payload = new byte[2 + reasonBytes.Length];
            payload[0] = (byte)(statusCode >> 8);
            payload[1] = (byte)(statusCode & 0xFF);
            if (reasonBytes.Length > 0)
            {
                Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            }
            return CreateFrame(WebSocketOpcode.Close, payload);
        }
    }
}
