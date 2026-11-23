using System.Text;

namespace ChatApp_Backend.RealTime.Sockets
{
    public sealed class WebSocketFrameReader
    {
        private readonly int maxMessageSize;
        private readonly List<byte> buffer = new();
        private readonly List<byte> fragmentBuffer = new();
        private WebSocketOpcode? fragmentOpcode;
        private long fragmentLength;

        public WebSocketFrameReader(int maxMessageSize = 65536)
        {
            this.maxMessageSize = maxMessageSize;
        }

        public void Feed(ReadOnlySpan<byte> data)
        {
            if (data.Length > 0)
            {
                buffer.AddRange(data.ToArray());
            }
        }

        public bool TryReadMessage(out WebSocketMessage? message, out string? error)
        {
            message = null;
            error = null;

            while (true)
            {
                if (buffer.Count < 2)
                {
                    return false;
                }

                byte first = buffer[0];
                byte second = buffer[1];

                bool fin = (first & 0x80) != 0;
                byte rsv = (byte)((first & 0x70) >> 4);
                var opcode = (WebSocketOpcode)(first & 0x0F);
                bool masked = (second & 0x80) != 0;

                long payloadLength = second & 0x7F;
                int index = 2;

                if (payloadLength == 126)
                {
                    if (buffer.Count < index + 2) return false;
                    payloadLength = (buffer[index] << 8) | buffer[index + 1];
                    index += 2;
                }
                else if (payloadLength == 127)
                {
                    if (buffer.Count < index + 8) return false;
                    payloadLength = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        payloadLength = (payloadLength << 8) | buffer[index + i];
                    }
                    if (payloadLength < 0)
                    {
                        error = "Invalid payload length";
                        return false;
                    }
                    index += 8;
                }

                byte[] mask = new byte[4];
                if (masked)
                {
                    if (buffer.Count < index + 4) return false;
                    for (int i = 0; i < 4; i++)
                    {
                        mask[i] = buffer[index + i];
                    }
                    index += 4;
                }

                if (payloadLength > int.MaxValue)
                {
                    error = "Payload too large";
                    return false;
                }

                if (buffer.Count < index + (int)payloadLength)
                {
                    return false;
                }

                byte[] payload = new byte[(int)payloadLength];
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = buffer[index + i];
                }

                if (masked)
                {
                    for (int i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= mask[i % 4];
                    }
                }

                int consumed = index + (int)payloadLength;
                buffer.RemoveRange(0, consumed);

                if (rsv != 0)
                {
                    ResetFragment();
                    error = "RSV bits must be 0";
                    return false;
                }

                if (!IsValidOpcode(opcode))
                {
                    ResetFragment();
                    error = "Unknown opcode";
                    return false;
                }

                if (!masked)
                {
                    ResetFragment();
                    error = "Client frame must be masked";
                    return false;
                }

                bool isControl = opcode is WebSocketOpcode.Close or WebSocketOpcode.Ping or WebSocketOpcode.Pong;

                if (isControl)
                {
                    if (!fin)
                    {
                        ResetFragment();
                        error = "Control frame must not be fragmented";
                        return false;
                    }
                    if (payload.Length > 125)
                    {
                        ResetFragment();
                        error = "Control frame payload too large";
                        return false;
                    }

                    if (opcode == WebSocketOpcode.Close)
                    {
                        ushort status = 1000;
                        string? reason = null;
                        if (payload.Length >= 2)
                        {
                            status = (ushort)((payload[0] << 8) | payload[1]);
                            if (payload.Length > 2)
                            {
                                try
                                {
                                    reason = new UTF8Encoding(false, true).GetString(payload, 2, payload.Length - 2);
                                }
                                catch
                                {
                                    error = "Invalid close reason UTF-8";
                                    return false;
                                }
                            }
                        }

                        message = new WebSocketMessage
                        {
                            Opcode = WebSocketOpcode.Close,
                            Payload = payload,
                            CloseStatus = status,
                            CloseReason = reason
                        };
                        return true;
                    }

                    if (opcode == WebSocketOpcode.Ping)
                    {
                        message = new WebSocketMessage { Opcode = WebSocketOpcode.Ping, Payload = payload };
                        return true;
                    }

                    message = new WebSocketMessage { Opcode = WebSocketOpcode.Pong, Payload = payload };
                    return true;
                }

                if (opcode == WebSocketOpcode.Continuation)
                {
                    if (fragmentOpcode == null)
                    {
                        ResetFragment();
                        error = "Unexpected continuation frame";
                        return false;
                    }
                }
                else
                {
                    if (fragmentOpcode != null)
                    {
                        ResetFragment();
                        error = "New message started before previous finished";
                        return false;
                    }
                    fragmentOpcode = opcode;
                    fragmentBuffer.Clear();
                    fragmentLength = 0;
                }

                if (fragmentLength + payload.Length > maxMessageSize)
                {
                    ResetFragment();
                    error = "Message too large";
                    return false;
                }

                fragmentBuffer.AddRange(payload);
                fragmentLength += payload.Length;

                if (fin)
                {
                    var finalOpcode = fragmentOpcode ?? opcode;
                    byte[] finalPayload = fragmentBuffer.ToArray();

                    ResetFragment();

                    if (finalOpcode == WebSocketOpcode.Text)
                    {
                        try
                        {
                            _ = new UTF8Encoding(false, true).GetString(finalPayload);
                        }
                        catch
                        {
                            error = "Invalid UTF-8 in text frame";
                            return false;
                        }
                    }

                    message = new WebSocketMessage
                    {
                        Opcode = finalOpcode,
                        Payload = finalPayload
                    };
                    return true;
                }

                // fragmented, continue parsing next frame
            }
        }

        private static bool IsValidOpcode(WebSocketOpcode opcode) =>
            opcode is WebSocketOpcode.Continuation or WebSocketOpcode.Text or WebSocketOpcode.Binary
                or WebSocketOpcode.Close or WebSocketOpcode.Ping or WebSocketOpcode.Pong;

        private void ResetFragment()
        {
            fragmentOpcode = null;
            fragmentBuffer.Clear();
            fragmentLength = 0;
        }
    }
}
