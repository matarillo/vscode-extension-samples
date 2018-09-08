using LanguageServer;
using System.Threading;

namespace SampleServer
{
    public delegate Result<T, TError> RequestHandler<TEventArgs, T, TError>(object sender, TEventArgs args, CancellationToken token);
}
