using GagspeakAPI.Data;

namespace GagSpeak.Services.Events;

/// <summary>
/// Interaction Event sent whenever an interaction is made to your client from others.
/// </summary>
public record InteractionEvent
{
    public DateTime EventTime { get; } // may not need.
    public string SenderUID { get; } // the pair who sent the update
    public InteractionType InteractionType { get; }
    public string InteractionContent { get; }

    public InteractionEvent(UserData senderUser, InteractionType interaction, string content)
    {
        EventTime = DateTime.Now;
        SenderUID = senderUser.AliasOrUID;
        InteractionType = interaction;
        InteractionContent = content;
    }

    public override string ToString()
    {
        return $"{EventTime:HH:mm:ss.fff}\t[{SenderUID}]{{{InteractionType}}}\t{InteractionContent}";
    }
}
