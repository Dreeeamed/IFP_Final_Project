namespace WordDuel.Web.Client.Services;

/// <summary>
/// Per-browser preferences (sound, colour-blind palette, last nickname).
/// Provided to the whole component tree as a cascading value by MainLayout.
/// </summary>
public sealed class UserPreferences(BrowserInterop browser)
{
    private const string SoundKey = "wordduel.sound";
    private const string ContrastKey = "wordduel.contrast";
    private const string NicknameKey = "wordduel.nickname";

    public bool SoundOn { get; private set; } = true;

    public bool HighContrast { get; private set; }

    public string Nickname { get; private set; } = "";

    public event Action? Changed;

    public async Task LoadAsync()
    {
        SoundOn = await browser.GetAsync(StorageKind.Local, SoundKey) != "off";
        HighContrast = await browser.GetAsync(StorageKind.Local, ContrastKey) == "on";
        Nickname = await browser.GetAsync(StorageKind.Local, NicknameKey) ?? "";
        Changed?.Invoke();
    }

    public async Task ToggleSoundAsync()
    {
        SoundOn = !SoundOn;
        await browser.SetAsync(StorageKind.Local, SoundKey, SoundOn ? "on" : "off");
        Changed?.Invoke();
    }

    public async Task ToggleContrastAsync()
    {
        HighContrast = !HighContrast;
        await browser.SetAsync(StorageKind.Local, ContrastKey, HighContrast ? "on" : "off");
        Changed?.Invoke();
    }

    public async Task RememberNicknameAsync(string nickname)
    {
        Nickname = nickname;
        await browser.SetAsync(StorageKind.Local, NicknameKey, nickname);
    }

    public ValueTask PlayAsync(string sound) => SoundOn ? browser.PlayAsync(sound) : ValueTask.CompletedTask;
}
