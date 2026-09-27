namespace Connect4.App.Services;

/// <summary>The game sounds as small generated WAV files (16-bit mono PCM), so no sound files are needed.</summary>
public static class SoundWaves
{
    public const int SampleRate = 22_050;

    private static readonly Dictionary<Sound, byte[]> Cache = [];

    /// <summary>A complete WAV file for the sound.</summary>
    public static byte[] Create(Sound sound)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(sound, out byte[]? wav))
            {
                wav = ToWav(sound switch
                {
                    Sound.Drop => Sweep(420, 160, 0.09, 0.8),
                    Sound.Win => Notes([523.25, 659.25, 783.99, 1046.5], 0.12),
                    Sound.Loss => Notes([392.0, 329.63, 261.63], 0.18),
                    _ => Notes([329.63, 329.63], 0.15),
                });
                Cache[sound] = wav;
            }

            return wav;
        }
    }

    // A falling tone with a fast decay, like a disc hitting the board.
    private static short[] Sweep(double fromHz, double toHz, double seconds, double volume)
    {
        int count = (int)(SampleRate * seconds);
        var samples = new short[count];
        double phase = 0;
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            phase += 2 * Math.PI * (fromHz + (toHz - fromHz) * t) / SampleRate;
            samples[i] = (short)(short.MaxValue * volume * Math.Exp(-5 * t) * Math.Sin(phase));
        }

        return samples;
    }

    private static short[] Notes(double[] frequencies, double seconds)
    {
        int length = (int)(SampleRate * seconds);
        var samples = new short[length * frequencies.Length];
        for (int note = 0; note < frequencies.Length; note++)
        {
            for (int i = 0; i < length; i++)
            {
                // Short fade in and out so the notes do not click.
                double envelope = Math.Min(1, Math.Min(i, length - i) / (SampleRate * 0.01));
                double value = Math.Sin(2 * Math.PI * frequencies[note] * i / SampleRate);
                samples[note * length + i] = (short)(short.MaxValue * 0.5 * envelope * value);
            }
        }

        return samples;
    }

    private static byte[] ToWav(short[] samples)
    {
        int dataBytes = samples.Length * 2;
        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (short sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
