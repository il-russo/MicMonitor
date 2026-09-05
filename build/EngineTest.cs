// Headless smoke test: builds the same capture -> render pipeline as the app,
// runs it muted for a few seconds, and reports how much audio actually moved.

using System;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

internal sealed class CountingProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    public long SamplesRendered;
    public float Peak;

    public CountingProvider(ISampleProvider source) { this.source = source; }
    public WaveFormat WaveFormat { get { return source.WaveFormat; } }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        SamplesRendered += read;
        for (int i = 0; i < read; i++)
        {
            float magnitude = Math.Abs(buffer[offset + i]);
            if (magnitude > Peak) Peak = magnitude;
        }
        // Silence the output so the test cannot cause acoustic feedback.
        for (int i = 0; i < read; i++) buffer[offset + i] = 0f;
        return read;
    }
}

internal static class EngineTest
{
    private static void Main()
    {
        MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

        Console.WriteLine("-- capture devices --");
        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            Console.WriteLine("  " + device.FriendlyName);
        Console.WriteLine("-- render devices --");
        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            Console.WriteLine("  " + device.FriendlyName);

        if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications) ||
            !enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
        {
            Console.WriteLine("RESULT: no default endpoints, skipping pipeline test");
            return;
        }

        MMDevice input = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        MMDevice output = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        Console.WriteLine("input : " + input.FriendlyName);
        Console.WriteLine("output: " + output.FriendlyName);

        const int latency = 25;
        WasapiCapture capture = new WasapiCapture(input, true, latency);
        WaveFormat captureFormat = capture.WaveFormat;
        WaveFormat renderFormat = output.AudioClient.MixFormat;
        Console.WriteLine("capture format: " + captureFormat);
        Console.WriteLine("render  format: " + renderFormat);

        BufferedWaveProvider queue = new BufferedWaveProvider(captureFormat);
        queue.BufferDuration = TimeSpan.FromMilliseconds(400);
        queue.DiscardOnBufferOverflow = true;
        queue.ReadFully = true;

        long captured = 0;
        capture.DataAvailable += delegate(object s, WaveInEventArgs e)
        {
            captured += e.BytesRecorded;
            queue.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        ISampleProvider chain = queue.ToSampleProvider();
        if (chain.WaveFormat.Channels > 1) chain = new MonoDownmix(chain);
        if (chain.WaveFormat.SampleRate != renderFormat.SampleRate)
            chain = new WdlResamplingSampleProvider(chain, renderFormat.SampleRate);
        chain = new MonoSpread(chain, renderFormat.Channels);
        CountingProvider counter = new CountingProvider(chain);

        WasapiOut player = new WasapiOut(output, AudioClientShareMode.Shared, true, latency);
        player.Init(counter);
        player.Play();
        capture.StartRecording();

        Thread.Sleep(3000);

        capture.StopRecording();
        player.Stop();
        Thread.Sleep(200);
        player.Dispose();
        capture.Dispose();

        Console.WriteLine("captured bytes  : " + captured);
        Console.WriteLine("rendered samples: " + counter.SamplesRendered);
        Console.WriteLine("input peak      : " + counter.Peak.ToString("0.0000"));
        Console.WriteLine(captured > 0 && counter.SamplesRendered > 0 ? "RESULT: OK" : "RESULT: FAIL");
    }
}

internal sealed class MonoDownmix : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly WaveFormat format;
    private float[] scratch;

    public MonoDownmix(ISampleProvider source)
    {
        this.source = source;
        channels = source.WaveFormat.Channels;
        format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat { get { return format; } }

    public int Read(float[] buffer, int offset, int count)
    {
        int needed = count * channels;
        if (scratch == null || scratch.Length < needed) scratch = new float[needed];
        int read = source.Read(scratch, 0, needed);
        int frames = read / channels;
        for (int frame = 0; frame < frames; frame++)
        {
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++) sum += scratch[frame * channels + channel];
            buffer[offset + frame] = sum / channels;
        }
        return frames;
    }
}

internal sealed class MonoSpread : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;
    private readonly WaveFormat format;
    private float[] scratch;

    public MonoSpread(ISampleProvider source, int channels)
    {
        this.source = source;
        this.channels = channels;
        format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, channels);
    }

    public WaveFormat WaveFormat { get { return format; } }

    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / channels;
        if (scratch == null || scratch.Length < frames) scratch = new float[frames];
        int read = source.Read(scratch, 0, frames);
        for (int frame = 0; frame < read; frame++)
        {
            int baseIndex = offset + frame * channels;
            buffer[baseIndex] = scratch[frame];
            if (channels > 1) buffer[baseIndex + 1] = scratch[frame];
            for (int channel = 2; channel < channels; channel++) buffer[baseIndex + channel] = 0f;
        }
        return read * channels;
    }
}
