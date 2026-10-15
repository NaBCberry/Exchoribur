using Avalonia.Threading;
using Exchoribur.Application.Abstractions;

namespace Exchoribur.App.Services;

internal sealed class UiCallbackDispatcher : ICallbackDispatcher
{
    public void Post(Action callback)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            callback();
        }
        else
        {
            Dispatcher.UIThread.Post(callback);
        }
    }
}
