using System;
using System.IO;
using System.Net.Sockets;
using hyperionnet;
using FlatBuffers;

namespace HyperionScreenCap.Networking
{
    class FbsClinet: HyperionClient
    {
        private bool prioritySet;

        public FbsClinet(string host, int port, int priority, int messageDuration) : base(host, port, priority, messageDuration)
        {
            prioritySet = false;
        }

        protected override void SendImageDataMessage(byte[] pixeldata, int width, int height)
        {
            ReadReplies();
            if (!prioritySet)
            {
                SendPriorityRegistrationMessage();
                SendPriorityRegistrationMessage(); // Sending twice just in case a message errors out. TODO: de-dupe
                prioritySet = true;
            }
            var builder = new FlatBufferBuilder(1024);
            var rawImageDataOffset = RawImage.CreateDataVector(builder, pixeldata);
            var rawImageOffset = RawImage.CreateRawImage(builder, rawImageDataOffset, width, height);
            var imageOffset = Image.CreateImage(builder, ImageType.RawImage, rawImageOffset.Value, _messageDuration);
            var requestOffset = Request.CreateRequest(builder, Command.Image, imageOffset.Value);
            builder.Finish(requestOffset.Value);
            SendFinishedMessage(builder);
        }

        private void ReadReplies()
        {
            var stream = (NetworkStream) _stream;
            while ( stream.DataAvailable )
            {
                byte[] header = ReadBytes(4);
                int size = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                if ( size <= 0 || size > 65536 )
                    throw new IOException($"Invalid reply size {size} from Hyperion");
                if ( Reply.GetRootAsReply(new ByteBuffer(ReadBytes(size))).Registered == -1 )
                    prioritySet = false;
            }
        }

        private byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            for ( int read = 0; read < count; )
            {
                int received = _stream.Read(buffer, read, count - read);
                if ( received == 0 )
                    throw new IOException("Hyperion closed the connection");
                read += received;
            }
            return buffer;
        }

        protected override void SendClearPriorityMessage()
        {
            var builder = new FlatBufferBuilder(64);
            var clearOffset = Clear.CreateClear(builder, _priority);
            var requestOffset = Request.CreateRequest(builder, Command.Clear, clearOffset.Value);
            builder.Finish(requestOffset.Value);
            SendFinishedMessage(builder);
        }

        private void SendPriorityRegistrationMessage()
        {
            var builder = new FlatBufferBuilder(64);
            var originOffset = builder.CreateString("HyperionScreenCap");
            var registerOffset = Register.CreateRegister(builder, originOffset, _priority);
            var requestOffset = Request.CreateRequest(builder, Command.Register, registerOffset.Value);
            builder.Finish(requestOffset.Value);
            SendFinishedMessage(builder);
        }

        private void SendFinishedMessage(FlatBufferBuilder finaliedBuilder)
        {
            var messageToSend = finaliedBuilder.DataBuffer.ToSizedArray();
            var messageSize = messageToSend.Length;
            sendMessageSize(messageSize);
            _stream.Write(messageToSend, 0, messageSize);
        }

        public override void Dispose()
        {
            prioritySet = false;
            base.Dispose();
        }

        public override String ToString()
        {
            return $"FbsClinet[{_host}:{_port} ({_priority})]";
        }
    }
}
