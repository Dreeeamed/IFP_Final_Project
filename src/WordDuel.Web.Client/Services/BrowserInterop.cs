using Microsoft.JSInterop;

namespace WordDuel.Web.Client.Services;

/// <summary>Typed wrapper around wwwroot/js/wordduel.js. Failures in the browser never crash the app.</summary>
public sealed class BrowserInterop(IJSRuntime js)
{
    public ValueTask RemoveSplashAsync() => Ignore(js.InvokeVoidAsync("wordDuel.removeSplash"));

    public async ValueTask<string?> GetAsync(StorageKind kind, string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("wordDuel.storageGet", Kind(kind), key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public ValueTask SetAsync(StorageKind kind, string key, string value) =>
        Ignore(js.InvokeVoidAsync("wordDuel.storageSet", Kind(kind), key, value));

    public ValueTask RemoveAsync(StorageKind kind, string key) =>
        Ignore(js.InvokeVoidAsync("wordDuel.storageRemove", Kind(kind), key));

    public async ValueTask<bool> CopyAsync(string text)
    {
        try
        {
            return await js.InvokeAsync<bool>("wordDuel.copy", text);
        }
        catch (JSException)
        {
            return false;
        }
    }

    public ValueTask AttachKeyboardAsync<T>(DotNetObjectReference<T> target) where T : class =>
        Ignore(js.InvokeVoidAsync("wordDuel.attachKeyboard", target));

    public ValueTask DetachKeyboardAsync() => Ignore(js.InvokeVoidAsync("wordDuel.detachKeyboard"));

    public ValueTask PlayAsync(string sound) => Ignore(js.InvokeVoidAsync("wordDuel.play", sound));

    public ValueTask VibrateAsync(int milliseconds) => Ignore(js.InvokeVoidAsync("wordDuel.vibrate", milliseconds));

    private static string Kind(StorageKind kind) => kind == StorageKind.Session ? "session" : "local";

    private static async ValueTask Ignore(ValueTask call)
    {
        try
        {
            await call;
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>sessionStorage is per tab (two tabs = two players); localStorage is shared preferences.</summary>
public enum StorageKind
{
    Session,
    Local
}
