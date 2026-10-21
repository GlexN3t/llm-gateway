namespace LlmGateway.Core.Caching;

public interface IChatCache
{
    bool TryGet(string key, out ChatResponse? response);

    void Set(string key, ChatResponse response);
}
