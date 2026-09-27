using Microsoft.JSInterop;
using Connect4.App.Services;

namespace Connect4.Web.Services;

/// <summary>Plays the generated sounds in the browser; they are handed to the page once, as WAV files.</summary>
public sealed class BrowserSoundService(IJSInProcessRuntime js) : ISoundService
{
    private bool _registered;

    public void Play(Sound sound)
    {
        try
        {
            if (!_registered)
            {
                foreach (Sound each in Enum.GetValues<Sound>())
                {
                    js.InvokeVoid("connect4.registerSound", each.ToString(), SoundWaves.Create(each));
                }

                _registered = true;
            }

            js.InvokeVoid("connect4.playSound", sound.ToString());
        }
        catch (JSException)
        {
            // The game works without sound.
        }
    }
}
