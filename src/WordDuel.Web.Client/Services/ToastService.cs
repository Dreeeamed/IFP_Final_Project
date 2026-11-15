using System.Collections.Immutable;

namespace WordDuel.Web.Client.Services;

public enum ToastKind
{
    Info,
    Success,
    Error
}

public sealed record Toast(Guid Id, string Message, ToastKind Kind);

/// <summary>
/// A tiny state container for transient messages. The list is immutable: every change publishes a
/// new list and raises <see cref="Changed"/>, and the ToastHost component re-renders.
/// </summary>
public sealed class ToastService
{
    public ImmutableList<Toast> Toasts { get; private set; } = ImmutableList<Toast>.Empty;

    public event Action? Changed;

    public void Show(string message, ToastKind kind = ToastKind.Info, int milliseconds = 2600)
    {
        var toast = new Toast(Guid.NewGuid(), message, kind);
        Toasts = Toasts.Add(toast).TakeLast(3).ToImmutableList();
        Changed?.Invoke();
        _ = DismissLaterAsync(toast.Id, milliseconds);
    }

    public void Dismiss(Guid id)
    {
        Toasts = Toasts.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
    }

    private async Task DismissLaterAsync(Guid id, int milliseconds)
    {
        await Task.Delay(milliseconds);
        Dismiss(id);
    }
}
