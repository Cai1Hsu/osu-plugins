using System.Reflection;
using osu.Game.Online.Chat;

namespace osu.Plugin.Referee;

public class RefereeChannel : Channel
{
    private readonly RefereeCommandHandler refereeCommandHandler;

    public RefereeChannel(RefereeCommandHandler rch) : base()
    {
        Type = ChannelType.System;
        Name = "RefereeHub";
        Id = -1;

        Joined.Value = true; // bypass initial fetch

        Joined.BindValueChanged(_ =>
        {
            if (!Joined.Value)
                MessagesLoaded = true;

            Joined.Value = true;
        }, true);

        refereeCommandHandler = rch;

        NewMessagesArrived += newMessageArrived;
        MessageRemoved += messageRemoved;
        PendingMessageResolved += pendingMessageResolved;
    }

    private static FieldInfo? MessageRemoved_BackingField = typeof(Channel)
        .GetField(nameof(MessageRemoved), BindingFlags.Instance | BindingFlags.NonPublic);

    public void RemoveMessage(Message message)
    {
        var handler = MessageRemoved_BackingField?.GetValue(this) as Action<Message>;

        Messages.Remove(message);
        handler?.Invoke(message);
    }

    private void pendingMessageResolved(LocalEchoMessage echo, Message resolved)
    {
    }

    private void messageRemoved(Message message)
    {
    }

    private void newMessageArrived(IEnumerable<Message> enumerable)
    {
        if (enumerable is Message[] messages && messages.Length is 1)
        {
            var message = messages[0];

            if (message is LocalEchoMessage echo)
            {
                AddNewMessages(new Message()
                {
                    Content = echo.Content,
                    DisplayContent = echo.DisplayContent,
                    Sender = echo.Sender,
                    Timestamp = echo.Timestamp,
                });

                refereeCommandHandler.HandleCommand(message.Content);
            }
        }
    }
}
