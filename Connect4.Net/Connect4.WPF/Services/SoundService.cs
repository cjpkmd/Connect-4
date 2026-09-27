using System.IO;
using System.Media;
using System.Threading.Channels;
using Connect4.App.Services;

namespace Connect4.WPF.Services;

/// <summary>Plays the generated sounds one after the other on a background thread, so a drop is not cut off by the win sound.</summary>
internal sealed class SoundService : ISoundService
{
    private readonly Channel<Sound> _queue = Channel.CreateUnbounded<Sound>(new UnboundedChannelOptions { SingleReader = true });

    public SoundService() => _ = Task.Run(PlayQueueAsync);

    public void Play(Sound sound) => _queue.Writer.TryWrite(sound);

    private async Task PlayQueueAsync()
    {
        await foreach (Sound sound in _queue.Reader.ReadAllAsync())
        {
            try
            {
                using var player = new SoundPlayer(new MemoryStream(SoundWaves.Create(sound)));
                player.PlaySync();
            }
            catch (InvalidOperationException)
            {
                // No sound device: the game works without sound.
            }
        }
    }
}
