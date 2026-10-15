namespace Exchoribur.Application.Abstractions;

/// <summary>将状态通知交给调用方所属的执行线程,应用层不依赖界面框架。</summary>
public interface ICallbackDispatcher
{
    void Post(Action callback);
}
