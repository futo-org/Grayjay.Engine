namespace Grayjay.Engine.Models.Live;

public interface ILiveEventChatMessage
{
    string Name { get; }
    string Thumbnail { get; }
    string Message { get; }
}