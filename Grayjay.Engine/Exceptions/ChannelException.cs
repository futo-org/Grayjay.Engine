using Grayjay.Engine.Models.Channel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Exceptions
{
    public class ChannelException: Exception
    {
        public string Url { get; set; }
        public PlatformChannel Channel { get; set; }

        public ChannelException(string url, Exception ex): base($"Channel: ${url} failed", ex) 
        {
            Url = url;
        }
        public ChannelException(PlatformChannel channel, Exception ex) : base($"Channel: ${channel.Name} failed", ex)
        {
            Url = channel.Url;
            Channel = channel;
        }

    }
}
