using HttpApiClient.Models;

namespace HttpApiClient.ErrorParsers
{
    public interface IKnownErrorParser<TClient> where TClient : class
    {
        bool ParseKnownErrors(ApiResponse response);
    }
}
