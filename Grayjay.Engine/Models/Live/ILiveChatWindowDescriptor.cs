using System.Collections.Generic;

namespace Grayjay.Engine.Models.Live;

public interface ILiveChatWindowDescriptor
{
    string Url { get; }
    List<string> RemoveElements { get; }
    List<string> RemoveElementsInterval { get; }
}