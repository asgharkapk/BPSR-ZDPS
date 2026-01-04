using Zproto;

namespace BPSR_ZDPS.DataTypes.Chat
{
    public class ChatMessage(ChatMsgInfo msg, ChitChatChannelType channel, long senderId, long timestamp)
    {
        public ChatMsgInfo Msg = msg;
        public ChitChatChannelType Channel = channel;
        public long SenderId = senderId;
        public DateTime TimeStamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
    }
}
