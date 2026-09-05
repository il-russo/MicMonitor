// Mic Flow - real-time microphone monitoring for Windows.
//
// Routes the selected capture device straight to the selected playback device
// using WASAPI shared mode. No virtual audio driver is installed, so other
// applications (Discord, OBS, games) keep receiving the untouched microphone
// signal exactly as they did before this app was running.
//
// The interface is an embedded HTML page rendered by WebView2; this file is
// the host: window chrome, audio engine and the JSON bridge between them.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MicMonitor
{
    internal static class Program
    {
        internal static readonly uint ShowMessage = RegisterWindowMessage("MicMonitor.Show.6F1C");

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        private static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

            bool isFirstInstance;
            using (Mutex instanceLock = new Mutex(true, "MicMonitor.SingleInstance.6F1C", out isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    PostMessage(HwndBroadcast, ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (!PrepareNativeLoader()) return;

                bool startHidden = false;
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase)) startHidden = true;
                }

                RunApplication(startHidden);
                GC.KeepAlive(instanceLock);
            }
        }

        /// <summary>
        /// WebView2's managed layer P/Invokes WebView2Loader.dll. The DLL travels
        /// inside this executable, so it is unpacked next to the user profile and
        /// pre-loaded before any WebView2 type is touched.
        /// </summary>
        private static bool PrepareNativeLoader()
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MicMonitor", "runtime");
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, "WebView2Loader.dll");

                using (Stream stream = Assembly.GetExecutingAssembly()
                           .GetManifestResourceStream("WebView2Loader.dll"))
                {
                    if (stream == null) throw new FileNotFoundException("WebView2Loader.dll mancante nelle risorse");

                    byte[] payload = new byte[stream.Length];
                    int read = 0;
                    while (read < payload.Length)
                    {
                        int chunk = stream.Read(payload, read, payload.Length - read);
                        if (chunk <= 0) break;
                        read += chunk;
                    }

                    bool needsWrite = true;
                    if (File.Exists(target))
                    {
                        try { needsWrite = new FileInfo(target).Length != payload.Length; }
                        catch (Exception) { needsWrite = true; }
                    }
                    if (needsWrite) File.WriteAllBytes(target, payload);
                }

                if (LoadLibrary(target) == IntPtr.Zero)
                {
                    throw new Exception("LoadLibrary ha restituito 0 (errore " +
                        Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ")");
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Impossibile preparare il componente WebView2.\r\n\r\n" + ex.Message,
                    "Mic Flow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Kept out of Main so the JIT does not need to load the referenced
        // assemblies before the resolver above is installed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication(bool startHidden)
        {
            Application.Run(new MainForm(startHidden));
        }

        private static readonly string[] EmbeddedAssemblies =
        {
            "NAudio",
            "Microsoft.Web.WebView2.Core",
            "Microsoft.Web.WebView2.WinForms"
        };

        private static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            string simpleName = new AssemblyName(e.Name).Name;
            bool known = false;
            foreach (string candidate in EmbeddedAssemblies)
            {
                if (candidate == simpleName) { known = true; break; }
            }
            if (!known) return null;

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(simpleName + ".dll"))
            {
                if (stream == null) return null;
                byte[] raw = new byte[stream.Length];
                int read = 0;
                while (read < raw.Length)
                {
                    int chunk = stream.Read(raw, read, raw.Length - read);
                    if (chunk <= 0) break;
                    read += chunk;
                }
                return Assembly.Load(raw);
            }
        }
    }

    #region Signal analysis

    /// <summary>Iterative radix-2 FFT over a power-of-two buffer.</summary>
    internal static class Fft
    {
        public static void Forward(double[] real, double[] imaginary)
        {
            int n = real.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double t = real[i]; real[i] = real[j]; real[j] = t;
                    t = imaginary[i]; imaginary[i] = imaginary[j]; imaginary[j] = t;
                }
            }

            for (int length = 2; length <= n; length <<= 1)
            {
                double angle = -2.0 * Math.PI / length;
                double wReal = Math.Cos(angle);
                double wImaginary = Math.Sin(angle);

                for (int i = 0; i < n; i += length)
                {
                    double curReal = 1.0, curImaginary = 0.0;
                    for (int k = 0; k < length / 2; k++)
                    {
                        int a = i + k, b = i + k + length / 2;
                        double xReal = real[b] * curReal - imaginary[b] * curImaginary;
                        double xImaginary = real[b] * curImaginary + imaginary[b] * curReal;

                        real[b] = real[a] - xReal;
                        imaginary[b] = imaginary[a] - xImaginary;
                        real[a] += xReal;
                        imaginary[a] += xImaginary;

                        double nextReal = curReal * wReal - curImaginary * wImaginary;
                        curImaginary = curReal * wImaginary + curImaginary * wReal;
                        curReal = nextReal;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rolling window of mono samples turned into log-spaced spectrum bands.
    /// Filled from the audio thread, read from the UI thread.
    /// </summary>
    internal sealed class SpectrumAnalyzer
    {
        public const int WindowSize = 1024;
        public const int BandCount = 72;

        private readonly float[] ring = new float[WindowSize];
        private readonly double[] window = new double[WindowSize];
        private readonly double[] real = new double[WindowSize];
        private readonly double[] imaginary = new double[WindowSize];
        private readonly float[] bands = new float[BandCount];
        private readonly object gate = new object();

        private int writeIndex;
        private int sampleRate = 48000;

        public SpectrumAnalyzer()
        {
            for (int i = 0; i < WindowSize; i++)
            {
                window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (WindowSize - 1)));
            }
        }

        public float DominantHz { get; private set; }

        public void SetSampleRate(int rate)
        {
            sampleRate = rate <= 0 ? 48000 : rate;
        }

        /// <summary>Called from the audio thread with one channel of samples.</summary>
        public void Push(float[] samples, int count)
        {
            lock (gate)
            {
                for (int i = 0; i < count; i++)
                {
                    ring[writeIndex] = samples[i];
                    writeIndex = (writeIndex + 1) & (WindowSize - 1);
                }
            }
        }

        /// <summary>Computes the current spectrum. Called from the UI thread.</summary>
        public float[] Analyze()
        {
            lock (gate)
            {
                int start = writeIndex;
                for (int i = 0; i < WindowSize; i++)
                {
                    real[i] = ring[(start + i) & (WindowSize - 1)] * window[i];
                    imaginary[i] = 0.0;
                }
            }

            Fft.Forward(real, imaginary);

            int bins = WindowSize / 2;
            double binHz = (double)sampleRate / WindowSize;
            const double minHz = 40.0;
            double maxHz = Math.Min(20000.0, sampleRate / 2.0);
            double logMin = Math.Log10(minHz), logMax = Math.Log10(maxHz);

            double loudest = 0.0;
            int loudestBin = 0;

            for (int band = 0; band < BandCount; band++)
            {
                double lowHz = Math.Pow(10, logMin + (logMax - logMin) * band / BandCount);
                double highHz = Math.Pow(10, logMin + (logMax - logMin) * (band + 1) / BandCount);

                int lowBin = Math.Max(1, (int)Math.Floor(lowHz / binHz));
                int highBin = Math.Min(bins - 1, Math.Max(lowBin, (int)Math.Ceiling(highHz / binHz)));

                double peak = 0.0;
                for (int bin = lowBin; bin <= highBin; bin++)
                {
                    double magnitude = Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]);
                    if (magnitude > peak) peak = magnitude;
                    if (magnitude > loudest) { loudest = magnitude; loudestBin = bin; }
                }

                // Normalised magnitude mapped onto a -78..0 dB display range.
                double normalized = peak / (WindowSize / 4.0);
                double db = normalized <= 1e-7 ? -100.0 : 20.0 * Math.Log10(normalized);
                double value = (db + 78.0) / 78.0;
                bands[band] = (float)Math.Max(0.0, Math.Min(1.0, value));
            }

            DominantHz = loudest < 0.02 ? 0f : (float)(loudestBin * binHz);
            return bands;
        }
    }

    /// <summary>Per-channel peak/RMS plus stereo correlation for one capture block.</summary>
    internal sealed class InputMeter
    {
        private float peakLeft, peakRight;
        private double sumLeft, sumRight, sumProduct;
        private long frames;
        private double noiseFloor = 1.0;

        private readonly object gate = new object();

        public void Add(float left, float right)
        {
            float absLeft = left < 0 ? -left : left;
            float absRight = right < 0 ? -right : right;
            if (absLeft > peakLeft) peakLeft = absLeft;
            if (absRight > peakRight) peakRight = absRight;
            sumLeft += left * left;
            sumRight += right * right;
            sumProduct += left * right;
            frames++;
        }

        public void Commit()
        {
            lock (gate)
            {
                if (frames <= 0) return;
                double rms = Math.Sqrt((sumLeft + sumRight) / (2.0 * frames));

                lastPeakLeft = peakLeft;
                lastPeakRight = peakRight;
                lastRms = rms;

                double denominator = Math.Sqrt(sumLeft * sumRight);
                lastCorrelation = denominator < 1e-12 ? 1.0 : sumProduct / denominator;

                // Slow tracker that settles on the quietest recent block.
                if (rms > 1e-9)
                {
                    noiseFloor = rms < noiseFloor ? noiseFloor * 0.85 + rms * 0.15 : noiseFloor * 0.9995 + rms * 0.0005;
                }

                peakLeft = 0f; peakRight = 0f;
                sumLeft = 0.0; sumRight = 0.0; sumProduct = 0.0; frames = 0;
            }
        }

        private double lastPeakLeft, lastPeakRight, lastRms, lastCorrelation = 1.0;

        public void Read(out double peakLeftDb, out double peakRightDb, out double rmsDb,
                         out double correlation, out double floorDb)
        {
            lock (gate)
            {
                peakLeftDb = ToDb(lastPeakLeft);
                peakRightDb = ToDb(lastPeakRight);
                rmsDb = ToDb(lastRms);
                correlation = lastCorrelation;
                floorDb = ToDb(noiseFloor);
            }
        }

        public void ResetFloor()
        {
            lock (gate) { noiseFloor = 1.0; }
        }

        private static double ToDb(double linear)
        {
            return linear <= 1e-6 ? -90.0 : 20.0 * Math.Log10(linear);
        }
    }

    #endregion

    #region Audio processing

    /// <summary>Mixes an interleaved multi-channel source down to a single channel.</summary>
    internal sealed class MonoDownmixProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int sourceChannels;
        private readonly WaveFormat format;
        private float[] scratch;

        public MonoDownmixProvider(ISampleProvider source)
        {
            this.source = source;
            sourceChannels = source.WaveFormat.Channels;
            format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }

        public WaveFormat WaveFormat { get { return format; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int needed = count * sourceChannels;
            if (scratch == null || scratch.Length < needed) scratch = new float[needed];

            int read = source.Read(scratch, 0, needed);
            int frames = read / sourceChannels;
            float scale = 1f / sourceChannels;

            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0f;
                int baseIndex = frame * sourceChannels;
                for (int channel = 0; channel < sourceChannels; channel++) sum += scratch[baseIndex + channel];
                buffer[offset + frame] = sum * scale;
            }
            return frames;
        }
    }

    /// <summary>Gain, noise gate and optional soft limiter on the monitor path.</summary>
    internal sealed class MonitorProcessor : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;
        private readonly float envelopeCoefficient;
        private readonly float limiterRelease;

        private float envelope;
        private float gateGain = 1f;
        private float limiterGain = 1f;
        private float gateReductionDb;

        public MonitorProcessor(ISampleProvider source)
        {
            this.source = source;
            int rate = source.WaveFormat.SampleRate;
            attackCoefficient = CoefficientFor(0.003, rate);
            releaseCoefficient = CoefficientFor(0.120, rate);
            envelopeCoefficient = CoefficientFor(0.015, rate);
            limiterRelease = CoefficientFor(0.080, rate);
        }

        private static float CoefficientFor(double seconds, int sampleRate)
        {
            return (float)Math.Exp(-1.0 / (seconds * sampleRate));
        }

        /// <summary>Linear output gain. 1.0 leaves the signal untouched.</summary>
        public volatile float Gain = 1f;

        /// <summary>Linear gate threshold. Zero disables the gate entirely.</summary>
        public volatile float GateThreshold = 0f;

        public volatile bool Muted;

        /// <summary>Soft brickwall instead of hard clipping.</summary>
        public volatile bool Limiter = true;

        public WaveFormat WaveFormat { get { return source.WaveFormat; } }

        /// <summary>How much the gate is currently attenuating, in dB.</summary>
        public float GateReductionDb { get { return gateReductionDb; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);

            float gain = Gain;
            float threshold = GateThreshold;
            bool muted = Muted;
            bool limiter = Limiter;

            for (int i = 0; i < read; i++)
            {
                float sample = buffer[offset + i];

                float magnitude = sample < 0f ? -sample : sample;
                envelope = magnitude + envelopeCoefficient * (envelope - magnitude);

                if (threshold > 0f)
                {
                    float target = envelope >= threshold ? 1f : 0f;
                    float coefficient = target > gateGain ? attackCoefficient : releaseCoefficient;
                    gateGain = target + coefficient * (gateGain - target);
                    sample *= gateGain;
                }
                else
                {
                    gateGain = 1f;
                }

                sample *= gain;

                if (limiter)
                {
                    // Peak-driven gain reduction with a smooth release.
                    float peak = sample < 0f ? -sample : sample;
                    float needed = peak > 0.98f ? 0.98f / peak : 1f;
                    limiterGain = needed < limiterGain ? needed : needed + limiterRelease * (limiterGain - needed);
                    sample *= limiterGain;
                }

                if (sample > 1f) sample = 1f;
                else if (sample < -1f) sample = -1f;

                buffer[offset + i] = muted ? 0f : sample;
            }

            gateReductionDb = gateGain >= 0.999f ? 0f : (float)(-20.0 * Math.Log10(Math.Max(0.0001, gateGain)));
            return read;
        }
    }

    /// <summary>Spreads a mono source across the first two channels of a wider output.</summary>
    internal sealed class MonoSpreadProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int outputChannels;
        private readonly WaveFormat format;
        private float[] scratch;

        public MonoSpreadProvider(ISampleProvider source, int outputChannels)
        {
            this.source = source;
            this.outputChannels = outputChannels;
            format = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, outputChannels);
        }

        public WaveFormat WaveFormat { get { return format; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / outputChannels;
            if (scratch == null || scratch.Length < frames) scratch = new float[frames];

            int read = source.Read(scratch, 0, frames);
            for (int frame = 0; frame < read; frame++)
            {
                int baseIndex = offset + frame * outputChannels;
                float value = scratch[frame];
                buffer[baseIndex] = value;
                if (outputChannels > 1) buffer[baseIndex + 1] = value;
                for (int channel = 2; channel < outputChannels; channel++) buffer[baseIndex + channel] = 0f;
            }
            return read * outputChannels;
        }
    }

    internal sealed class AudioEngineStoppedEventArgs : EventArgs
    {
        public AudioEngineStoppedEventArgs(Exception error) { Error = error; }
        public Exception Error { get; private set; }
    }

    /// <summary>Owns the capture/render pair and the sample pipeline between them.</summary>
    internal sealed class AudioEngine : IDisposable
    {
        private WasapiCapture capture;
        private WasapiOut output;
        private BufferedWaveProvider queue;
        private MonitorProcessor processor;
        private int maxQueuedBytes;
        private volatile bool running;

        private float[] monoScratch = new float[4096];
        private long processingTicks;
        private long wallTicks;
        private int dropouts;

        public event EventHandler<AudioEngineStoppedEventArgs> Stopped;

        public readonly SpectrumAnalyzer Spectrum = new SpectrumAnalyzer();
        public readonly InputMeter Meter = new InputMeter();

        public bool IsRunning { get { return running; } }
        public int LatencyMilliseconds { get; private set; }
        public int InputSampleRate { get; private set; }
        public int InputChannels { get; private set; }
        public int InputBits { get; private set; }
        public int OutputSampleRate { get; private set; }
        public int OutputChannels { get; private set; }
        public int Dropouts { get { return dropouts; } }

        public float Gain
        {
            get { return processor == null ? 1f : processor.Gain; }
            set { if (processor != null) processor.Gain = value; }
        }

        public float GateThreshold
        {
            get { return processor == null ? 0f : processor.GateThreshold; }
            set { if (processor != null) processor.GateThreshold = value; }
        }

        public bool Muted
        {
            get { return processor != null && processor.Muted; }
            set { if (processor != null) processor.Muted = value; }
        }

        private bool limiter = true;

        public bool Limiter
        {
            get { return limiter; }
            set { limiter = value; if (processor != null) processor.Limiter = value; }
        }

        public float GateReductionDb
        {
            get { return processor == null ? 0f : processor.GateReductionDb; }
        }

        /// <summary>Share of the buffer period spent inside the capture callback.</summary>
        public double LoadPercent
        {
            get
            {
                long wall = Interlocked.Read(ref wallTicks);
                if (wall <= 0) return 0.0;
                double value = 100.0 * Interlocked.Read(ref processingTicks) / wall;
                return Math.Max(0.0, Math.Min(100.0, value));
            }
        }

        public void ResetCounters()
        {
            Interlocked.Exchange(ref processingTicks, 0);
            Interlocked.Exchange(ref wallTicks, 0);
            Interlocked.Exchange(ref dropouts, 0);
            Meter.ResetFloor();
        }

        public void Start(string inputDeviceId, string outputDeviceId, int latencyMilliseconds,
                          float gain, float gateThreshold, bool muted, bool useLimiter)
        {
            Stop();

            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDevice inputDevice = enumerator.GetDevice(inputDeviceId);
            MMDevice outputDevice = enumerator.GetDevice(outputDeviceId);

            LatencyMilliseconds = latencyMilliseconds;
            limiter = useLimiter;

            capture = new WasapiCapture(inputDevice, true, latencyMilliseconds);
            WaveFormat captureFormat = capture.WaveFormat;
            WaveFormat renderFormat = outputDevice.AudioClient.MixFormat;

            InputSampleRate = captureFormat.SampleRate;
            InputChannels = captureFormat.Channels;
            InputBits = captureFormat.BitsPerSample;
            OutputSampleRate = renderFormat.SampleRate;
            OutputChannels = renderFormat.Channels;
            Spectrum.SetSampleRate(captureFormat.SampleRate);

            queue = new BufferedWaveProvider(captureFormat);
            queue.BufferDuration = TimeSpan.FromMilliseconds(Math.Max(400, latencyMilliseconds * 8));
            queue.DiscardOnBufferOverflow = true;
            queue.ReadFully = true;

            // Anything beyond this means the two device clocks have drifted apart
            // (or the UI thread stalled); the queue is dropped rather than letting
            // the monitoring delay grow without bound.
            maxQueuedBytes = (int)(captureFormat.AverageBytesPerSecond * (latencyMilliseconds * 4 + 60) / 1000.0);

            ISampleProvider chain = queue.ToSampleProvider();
            if (chain.WaveFormat.Channels > 1) chain = new MonoDownmixProvider(chain);

            processor = new MonitorProcessor(chain);
            processor.Gain = gain;
            processor.GateThreshold = gateThreshold;
            processor.Muted = muted;
            processor.Limiter = useLimiter;
            chain = processor;

            if (chain.WaveFormat.SampleRate != renderFormat.SampleRate)
            {
                chain = new WdlResamplingSampleProvider(chain, renderFormat.SampleRate);
            }
            chain = new MonoSpreadProvider(chain, renderFormat.Channels);

            ResetCounters();

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;

            output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, true, latencyMilliseconds);
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(chain);

            running = true;
            output.Play();
            capture.StartRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            BufferedWaveProvider target = queue;
            if (target == null || e.BytesRecorded <= 0) return;

            long started = Stopwatch.GetTimestamp();

            WaveFormat format = capture.WaveFormat;
            int channels = format.Channels;
            int frames = e.BytesRecorded / (format.BitsPerSample / 8) / channels;

            if (monoScratch.Length < frames) monoScratch = new float[frames];

            bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
                           (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32);

            for (int frame = 0; frame < frames; frame++)
            {
                float left, right;
                if (isFloat)
                {
                    int offset = (frame * channels) * 4;
                    left = BitConverter.ToSingle(e.Buffer, offset);
                    right = channels > 1 ? BitConverter.ToSingle(e.Buffer, offset + 4) : left;
                }
                else
                {
                    int offset = (frame * channels) * 2;
                    left = BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                    right = channels > 1 ? BitConverter.ToInt16(e.Buffer, offset + 2) / 32768f : left;
                }

                Meter.Add(left, right);
                monoScratch[frame] = channels > 1 ? (left + right) * 0.5f : left;
            }

            Meter.Commit();
            Spectrum.Push(monoScratch, frames);

            if (target.BufferedBytes > maxQueuedBytes)
            {
                target.ClearBuffer();
                Interlocked.Increment(ref dropouts);
            }
            target.AddSamples(e.Buffer, 0, e.BytesRecorded);

            long elapsed = Stopwatch.GetTimestamp() - started;
            Interlocked.Add(ref processingTicks, elapsed);
            Interlocked.Add(ref wallTicks, (long)(Stopwatch.Frequency * frames / (double)format.SampleRate));
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (running && e.Exception != null) RaiseStopped(e.Exception);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (running && e.Exception != null) RaiseStopped(e.Exception);
        }

        private void RaiseStopped(Exception error)
        {
            running = false;
            EventHandler<AudioEngineStoppedEventArgs> handler = Stopped;
            if (handler != null) handler(this, new AudioEngineStoppedEventArgs(error));
        }

        public void Stop()
        {
            running = false;

            if (capture != null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                try { capture.StopRecording(); }
                catch (Exception) { }
                try { capture.Dispose(); }
                catch (Exception) { }
                capture = null;
            }

            if (output != null)
            {
                output.PlaybackStopped -= OnPlaybackStopped;
                try { output.Stop(); }
                catch (Exception) { }
                try { output.Dispose(); }
                catch (Exception) { }
                output = null;
            }

            queue = null;
            processor = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>Minimal stopwatch helper (System.Diagnostics is not imported here).</summary>
    internal static class Stopwatch
    {
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long value);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long value);

        public static readonly long Frequency = GetFrequency();

        private static long GetFrequency()
        {
            long value;
            QueryPerformanceFrequency(out value);
            return value <= 0 ? 1 : value;
        }

        public static long GetTimestamp()
        {
            long value;
            QueryPerformanceCounter(out value);
            return value;
        }
    }

    #endregion

    #region Settings

    internal sealed class Settings
    {
        public string InputDeviceId = "";
        public string OutputDeviceId = "";
        public int Volume = 100;
        public int Latency = 25;
        public int Gate = 0;
        public int GainDb = 0;
        public bool Limiter = true;
        public bool AutoStartMonitoring = false;
        public bool MinimizeToTray = true;
        public int WindowWidth = 1180;
        public int WindowHeight = 820;
        public int WindowX = -1;
        public int WindowY = -1;
        public bool Maximized = false;

        private static string FilePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicMonitor");
                return Path.Combine(folder, "settings.ini");
            }
        }

        public static Settings Load()
        {
            Settings settings = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return settings;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    switch (key)
                    {
                        case "input": settings.InputDeviceId = value; break;
                        case "output": settings.OutputDeviceId = value; break;
                        case "volume": settings.Volume = ParseInt(value, 100); break;
                        case "latency": settings.Latency = ParseInt(value, 25); break;
                        case "gate": settings.Gate = ParseInt(value, 0); break;
                        case "gain": settings.GainDb = ParseInt(value, 0); break;
                        case "limiter": settings.Limiter = value != "0"; break;
                        case "autostart": settings.AutoStartMonitoring = value == "1"; break;
                        case "tray": settings.MinimizeToTray = value != "0"; break;
                        case "w": settings.WindowWidth = ParseInt(value, 1180); break;
                        case "h": settings.WindowHeight = ParseInt(value, 820); break;
                        case "x": settings.WindowX = ParseInt(value, -1); break;
                        case "y": settings.WindowY = ParseInt(value, -1); break;
                        case "max": settings.Maximized = value == "1"; break;
                    }
                }
            }
            catch (Exception) { }
            return settings;
        }

        private static int ParseInt(string text, int fallback)
        {
            int result;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        public void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("input=" + InputDeviceId);
                builder.AppendLine("output=" + OutputDeviceId);
                builder.AppendLine("volume=" + Volume.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("latency=" + Latency.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("gate=" + Gate.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("gain=" + GainDb.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("limiter=" + (Limiter ? "1" : "0"));
                builder.AppendLine("autostart=" + (AutoStartMonitoring ? "1" : "0"));
                builder.AppendLine("tray=" + (MinimizeToTray ? "1" : "0"));
                builder.AppendLine("w=" + WindowWidth.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("h=" + WindowHeight.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("x=" + WindowX.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("y=" + WindowY.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("max=" + (Maximized ? "1" : "0"));

                File.WriteAllText(path, builder.ToString());
            }
            catch (Exception) { }
        }
    }

    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MicMonitor";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch (Exception) { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --tray");
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception) { }
        }
    }

    #endregion

    internal sealed class DeviceItem
    {
        public DeviceItem(string id, string name, bool isDefault)
        {
            Id = id; Name = name; IsDefault = isDefault;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool IsDefault { get; private set; }
    }

    internal sealed class MainForm : Form
    {
        #region Interop

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
            int x, int y, int cx, int cy, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;

        private const int WmNcLButtonDown = 0x00A1;
        private const int WmNcHitTest = 0x0084;
        private const int WmDpiChanged = 0x02E0;
        private const int HtCaption = 2;
        private const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14;
        private const int HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;

        private const int ResizeMargin = 6;
        private const int BaseMinimumWidth = 1000;
        private const int BaseMinimumHeight = 700;

        #endregion

        private readonly AudioEngine engine = new AudioEngine();
        private readonly Settings settings;
        private readonly WinFormsTimer meterTimer = new WinFormsTimer();
        private readonly WinFormsTimer startupTimer = new WinFormsTimer();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();

        private WebView2 webView;
        private NotifyIcon trayIcon;

        private List<DeviceItem> inputDevices = new List<DeviceItem>();
        private List<DeviceItem> outputDevices = new List<DeviceItem>();
        private string selectedInputId;
        private string selectedOutputId;
        private string statusMessage = "Pronto";

        private bool bridgeReady;
        private bool exitRequested;
        private bool autoStartAttempted;
        private readonly bool startHidden;

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            settings = Settings.Load();

            BuildWindow();
            BuildTrayIcon();
            RefreshDevices();

            engine.Stopped += OnEngineStopped;
            engine.Limiter = settings.Limiter;

            meterTimer.Interval = 33;
            meterTimer.Tick += OnMeterTick;

            startupTimer.Interval = 400;
            startupTimer.Tick += delegate
            {
                startupTimer.Stop();
                TryAutoStart();
            };
            startupTimer.Start();

            InitialiseWebView();
        }

        #region Window

        private void BuildWindow()
        {
            Text = "Mic Flow";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(0x0C, 0x0E, 0x12);
            MinimumSize = ScaledMinimum();
            StartPosition = FormStartPosition.Manual;
            Icon = IconFactory.CreateAppIcon();
            DoubleBuffered = true;
            Padding = new Padding(1);

            Size = new Size(Math.Max(1000, settings.WindowWidth), Math.Max(700, settings.WindowHeight));
            if (settings.WindowX >= 0 && settings.WindowY >= 0 &&
                IsOnAnyScreen(new Rectangle(settings.WindowX, settings.WindowY, Width, Height)))
            {
                Location = new Point(settings.WindowX, settings.WindowY);
            }
            else
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(work.X + (work.Width - Width) / 2, work.Y + (work.Height - Height) / 2);
            }
            if (settings.Maximized) WindowState = FormWindowState.Maximized;
        }

        private Size ScaledMinimum()
        {
            using (Graphics g = CreateGraphics())
            {
                return new Size(
                    (int)(BaseMinimumWidth * g.DpiX / 96f),
                    (int)(BaseMinimumHeight * g.DpiY / 96f));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // The 1px frame only makes sense on a floating window.
            Padding = new Padding(WindowState == FormWindowState.Maximized ? 0 : 1);
        }

        private static bool IsOnAnyScreen(Rectangle bounds)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds)) return true;
            }
            return false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        /// <summary>Turns the 1px frame around the browser into real resize grips.</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.ShowMessage)
            {
                ShowFromTray();
                return;
            }

            // Moving the window to a monitor with a different scaling factor.
            // Windows hands over the rectangle the window should occupy there;
            // without honouring it the frame and the page end up disagreeing.
            if (m.Msg == WmDpiChanged)
            {
                int dpi = m.WParam.ToInt32() & 0xFFFF;
                Rect suggested = (Rect)Marshal.PtrToStructure(m.LParam, typeof(Rect));

                Size restore = MinimumSize;
                MinimumSize = Size.Empty;
                SetWindowPos(Handle, IntPtr.Zero,
                    suggested.Left, suggested.Top,
                    suggested.Right - suggested.Left, suggested.Bottom - suggested.Top,
                    SwpNoZOrder | SwpNoActivate);
                MinimumSize = new Size(BaseMinimumWidth * dpi / 96, BaseMinimumHeight * dpi / 96);
                GC.KeepAlive(restore);

                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
            {
                Point screenPoint = new Point(m.LParam.ToInt32());
                Point p = PointToClient(screenPoint);

                bool left = p.X <= ResizeMargin;
                bool right = p.X >= ClientSize.Width - ResizeMargin;
                bool top = p.Y <= ResizeMargin;
                bool bottom = p.Y >= ClientSize.Height - ResizeMargin;

                if (left && top) { m.Result = new IntPtr(HtTopLeft); return; }
                if (right && top) { m.Result = new IntPtr(HtTopRight); return; }
                if (left && bottom) { m.Result = new IntPtr(HtBottomLeft); return; }
                if (right && bottom) { m.Result = new IntPtr(HtBottomRight); return; }
                if (left) { m.Result = new IntPtr(HtLeft); return; }
                if (right) { m.Result = new IntPtr(HtRight); return; }
                if (top) { m.Result = new IntPtr(HtTop); return; }
                if (bottom) { m.Result = new IntPtr(HtBottom); return; }
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        private void BuildTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = IconFactory.CreateAppIcon();
            trayIcon.Text = "Mic Flow";
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowFromTray(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Avvia / ferma ascolto");
            toggleItem.Click += delegate { TogglePower(); };
            ToolStripMenuItem showItem = new ToolStripMenuItem("Mostra finestra");
            showItem.Click += delegate { ShowFromTray(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Esci");
            exitItem.Click += delegate { exitRequested = true; Close(); };

            menu.Items.Add(toggleItem);
            menu.Items.Add(showItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = menu;
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = settings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }
            Activate();
            BringToFront();
        }

        #endregion

        #region WebView

        private async void InitialiseWebView()
        {
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(0x0C, 0x0E, 0x12);
            Controls.Add(webView);

            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MicMonitor", "webview");
                Directory.CreateDirectory(userData);

                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = "--disable-features=msWebOOUI,msPdfOOUI --autoplay-policy=no-user-gesture-required";

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData, options);
                await webView.EnsureCoreWebView2Async(environment);

                CoreWebView2Settings browser = webView.CoreWebView2.Settings;
                browser.AreDefaultContextMenusEnabled = false;
                browser.AreDevToolsEnabled = false;
                browser.IsStatusBarEnabled = false;
                browser.IsZoomControlEnabled = false;
                browser.AreBrowserAcceleratorKeysEnabled = false;

                webView.CoreWebView2.WebMessageReceived += OnWebMessage;
                webView.CoreWebView2.NavigateToString(LoadUi());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 non è disponibile su questo PC.\r\n\r\n" + ex.Message +
                    "\r\n\r\nInstalla \"Microsoft Edge WebView2 Runtime\" e riapri l'app.",
                    "Mic Flow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private static string LoadUi()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.html"))
            {
                if (stream == null) return "<html><body style='background:#0c0e12;color:#fff'>UI mancante</body></html>";
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private void Post(string payload)
        {
            if (!bridgeReady || webView == null || webView.CoreWebView2 == null) return;
            try { webView.CoreWebView2.PostWebMessageAsJson(payload); }
            catch (Exception) { }
        }

        private void Toast(string text, bool error)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"type\":\"toast\",\"text\":").Append(JsonString(text));
            builder.Append(",\"error\":").Append(error ? "true" : "false").Append('}');
            Post(builder.ToString());
        }

        #endregion

        #region Bridge - incoming

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> message;
            try
            {
                message = json.Deserialize<Dictionary<string, object>>(e.TryGetWebMessageAsString());
            }
            catch (Exception)
            {
                return;
            }
            if (message == null || !message.ContainsKey("cmd")) return;

            string command = Convert.ToString(message["cmd"], CultureInfo.InvariantCulture);

            switch (command)
            {
                case "ready":
                    bridgeReady = true;
                    PushState();
                    meterTimer.Start();
                    break;

                case "window":
                    HandleWindowCommand(Convert.ToString(message["action"], CultureInfo.InvariantCulture));
                    break;

                case "power":
                    TogglePower();
                    break;

                case "rescan":
                    if (engine.IsRunning)
                    {
                        Toast("Ferma l'ascolto prima di rileggere i dispositivi", true);
                    }
                    else
                    {
                        RefreshDevices();
                        statusMessage = "Elenco dispositivi aggiornato";
                        PushState();
                        Toast("Dispositivi aggiornati", false);
                    }
                    break;

                case "setInput":
                    selectedInputId = Convert.ToString(message["id"], CultureInfo.InvariantCulture);
                    Persist();
                    PushState();
                    break;

                case "setOutput":
                    selectedOutputId = Convert.ToString(message["id"], CultureInfo.InvariantCulture);
                    Persist();
                    PushState();
                    break;

                case "setVolume":
                    settings.Volume = Clamp(ToInt(message["value"]), 0, 200);
                    ApplyGain();
                    Persist();
                    break;

                case "nudgeGain":
                    settings.GainDb = Clamp(settings.GainDb + ToInt(message["delta"]), -20, 30);
                    ApplyGain();
                    Persist();
                    PushState();
                    break;

                case "setLatency":
                    settings.Latency = Clamp(ToInt(message["value"]), 5, 120);
                    Persist();
                    if (engine.IsRunning) StartMonitoring();
                    else PushState();
                    break;

                case "setGate":
                    settings.Gate = Clamp(ToInt(message["value"]), 0, 50);
                    engine.GateThreshold = GateThreshold();
                    Persist();
                    break;

                case "mute":
                    engine.Muted = !engine.Muted;
                    PushState();
                    break;

                case "resetPeak":
                    engine.ResetCounters();
                    break;

                case "toggle":
                    HandleToggle(Convert.ToString(message["name"], CultureInfo.InvariantCulture),
                                 Convert.ToBoolean(message["value"]));
                    break;
            }
        }

        private void HandleWindowCommand(string action)
        {
            switch (action)
            {
                case "drag":
                    if (WindowState == FormWindowState.Maximized) return;
                    ReleaseCapture();
                    SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
                    break;
                case "min":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "max":
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    settings.Maximized = WindowState == FormWindowState.Maximized;
                    Padding = new Padding(settings.Maximized ? 0 : 1);
                    Persist();
                    break;
                case "close":
                    Close();
                    break;
            }
        }

        private void HandleToggle(string name, bool value)
        {
            switch (name)
            {
                case "limiter":
                    settings.Limiter = value;
                    engine.Limiter = value;
                    break;
                case "startup":
                    WindowsStartup.SetEnabled(value);
                    Toast(value ? "Mic Flow partirà con Windows" : "Avvio automatico disattivato", false);
                    break;
                case "tray":
                    settings.MinimizeToTray = value;
                    break;
                case "autostart":
                    settings.AutoStartMonitoring = value;
                    break;
            }
            Persist();
            PushState();
        }

        private static int ToInt(object value)
        {
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }

        #endregion

        #region Bridge - outgoing

        private void PushState()
        {
            StringBuilder b = new StringBuilder(1024);
            b.Append("{\"type\":\"state\"");
            b.Append(",\"running\":").Append(engine.IsRunning ? "true" : "false");
            b.Append(",\"volume\":").Append(settings.Volume);
            b.Append(",\"latency\":").Append(settings.Latency);
            b.Append(",\"gate\":").Append(settings.Gate);
            b.Append(",\"gainDb\":").Append(settings.GainDb.ToString("0.0", CultureInfo.InvariantCulture));
            b.Append(",\"limiter\":").Append(settings.Limiter ? "true" : "false");
            b.Append(",\"tray\":").Append(settings.MinimizeToTray ? "true" : "false");
            b.Append(",\"autostart\":").Append(settings.AutoStartMonitoring ? "true" : "false");
            b.Append(",\"startup\":").Append(WindowsStartup.IsEnabled() ? "true" : "false");
            b.Append(",\"muted\":").Append(engine.Muted ? "true" : "false");
            b.Append(",\"dropouts\":").Append(engine.Dropouts);

            b.Append(",\"inRate\":").Append(engine.IsRunning ? engine.InputSampleRate : 0);
            b.Append(",\"inChannels\":").Append(engine.IsRunning ? engine.InputChannels : 0);
            b.Append(",\"inBits\":").Append(engine.IsRunning ? engine.InputBits : 0);
            b.Append(",\"outRate\":").Append(engine.IsRunning ? engine.OutputSampleRate : 0);
            b.Append(",\"outChannels\":").Append(engine.IsRunning ? engine.OutputChannels : 0);
            b.Append(",\"inDefault\":").Append(IsDefaultDevice(inputDevices, selectedInputId) ? "true" : "false");
            b.Append(",\"outDefault\":").Append(IsDefaultDevice(outputDevices, selectedOutputId) ? "true" : "false");

            b.Append(",\"inputId\":").Append(JsonString(selectedInputId));
            b.Append(",\"outputId\":").Append(JsonString(selectedOutputId));
            b.Append(",\"message\":").Append(JsonString(statusMessage));

            b.Append(",\"devices\":{\"inputs\":");
            AppendDevices(b, inputDevices);
            b.Append(",\"outputs\":");
            AppendDevices(b, outputDevices);
            b.Append("}}");

            Post(b.ToString());
        }

        private static void AppendDevices(StringBuilder b, List<DeviceItem> devices)
        {
            b.Append('[');
            for (int i = 0; i < devices.Count; i++)
            {
                if (i > 0) b.Append(',');
                b.Append("{\"id\":").Append(JsonString(devices[i].Id));
                b.Append(",\"name\":").Append(JsonString(devices[i].Name));
                b.Append(",\"def\":").Append(devices[i].IsDefault ? "true" : "false").Append('}');
            }
            b.Append(']');
        }

        private static bool IsDefaultDevice(List<DeviceItem> devices, string id)
        {
            foreach (DeviceItem device in devices)
            {
                if (device.Id == id) return device.IsDefault;
            }
            return false;
        }

        private void OnMeterTick(object sender, EventArgs e)
        {
            if (!bridgeReady) return;

            double peakLeft, peakRight, rms, correlation, floor;
            engine.Meter.Read(out peakLeft, out peakRight, out rms, out correlation, out floor);

            if (!engine.IsRunning)
            {
                peakLeft = -90; peakRight = -90; rms = -90;
            }

            float[] bands = engine.IsRunning ? engine.Spectrum.Analyze() : null;

            StringBuilder b = new StringBuilder(1200);
            b.Append("{\"type\":\"meters\"");
            b.Append(",\"peakL\":").Append(Num(peakLeft));
            b.Append(",\"peakR\":").Append(Num(peakRight));
            b.Append(",\"rms\":").Append(Num(rms));
            b.Append(",\"corr\":").Append(Num(correlation));
            b.Append(",\"floor\":").Append(Num(floor));
            b.Append(",\"gr\":").Append(Num(engine.GateReductionDb));
            b.Append(",\"load\":").Append(Num(engine.LoadPercent));
            b.Append(",\"drops\":").Append(engine.Dropouts);
            b.Append(",\"dominant\":").Append(Num(engine.IsRunning ? engine.Spectrum.DominantHz : 0f));
            b.Append(",\"bands\":[");
            for (int i = 0; i < SpectrumAnalyzer.BandCount; i++)
            {
                if (i > 0) b.Append(',');
                b.Append(Num(bands == null ? 0f : bands[i]));
            }
            b.Append("]}");

            Post(b.ToString());
        }

        private static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            StringBuilder b = new StringBuilder(value.Length + 8);
            b.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < ' ') b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
            return b.ToString();
        }

        #endregion

        #region Devices and monitoring

        private void RefreshDevices()
        {
            List<DeviceItem> inputs = new List<DeviceItem>();
            List<DeviceItem> outputs = new List<DeviceItem>();
            string defaultInputId = null, defaultOutputId = null;

            try
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

                if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
                {
                    defaultInputId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID;
                }
                if (enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    defaultOutputId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
                }

                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    inputs.Add(new DeviceItem(device.ID, device.FriendlyName, device.ID == defaultInputId));
                }
                foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    outputs.Add(new DeviceItem(device.ID, device.FriendlyName, device.ID == defaultOutputId));
                }
            }
            catch (Exception ex)
            {
                statusMessage = "Dispositivi non leggibili: " + ex.Message;
            }

            inputDevices = inputs;
            outputDevices = outputs;

            selectedInputId = PickDevice(inputs, selectedInputId ?? settings.InputDeviceId, defaultInputId);
            selectedOutputId = PickDevice(outputs, selectedOutputId ?? settings.OutputDeviceId, defaultOutputId);
        }

        private static string PickDevice(List<DeviceItem> devices, string preferred, string fallback)
        {
            if (devices.Count == 0) return null;
            if (!string.IsNullOrEmpty(preferred))
            {
                foreach (DeviceItem device in devices)
                {
                    if (device.Id == preferred) return preferred;
                }
            }
            if (!string.IsNullOrEmpty(fallback))
            {
                foreach (DeviceItem device in devices)
                {
                    if (device.Id == fallback) return fallback;
                }
            }
            return devices[0].Id;
        }

        private void TogglePower()
        {
            if (engine.IsRunning) StopMonitoring("Ascolto fermato");
            else StartMonitoring();
        }

        private void StartMonitoring()
        {
            if (string.IsNullOrEmpty(selectedInputId) || string.IsNullOrEmpty(selectedOutputId))
            {
                Toast("Seleziona microfono e uscita", true);
                return;
            }

            try
            {
                engine.Start(selectedInputId, selectedOutputId, settings.Latency,
                    LinearGain(), GateThreshold(), engine.Muted, settings.Limiter);
            }
            catch (Exception ex)
            {
                engine.Stop();
                statusMessage = "Errore: " + ex.Message;
                PushState();
                Toast(ex.Message, true);
                return;
            }

            statusMessage = "In ascolto";
            Persist();
            PushState();
        }

        private void StopMonitoring(string message)
        {
            engine.Stop();
            statusMessage = message;
            PushState();
        }

        private void OnEngineStopped(object sender, AudioEngineStoppedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<AudioEngineStoppedEventArgs>(OnEngineStopped), sender, e);
                return;
            }
            StopMonitoring("Interrotto: " + (e.Error == null ? "dispositivo non disponibile" : e.Error.Message));
            Toast("Ascolto interrotto: dispositivo non disponibile", true);
        }

        private void TryAutoStart()
        {
            if (autoStartAttempted) return;
            autoStartAttempted = true;
            if (settings.AutoStartMonitoring && !engine.IsRunning) StartMonitoring();
        }

        private float LinearGain()
        {
            double preamp = Math.Pow(10.0, settings.GainDb / 20.0);
            return (float)(preamp * settings.Volume / 100.0);
        }

        private void ApplyGain()
        {
            engine.Gain = LinearGain();
        }

        /// <summary>Maps the gate slider (0 = off, 1..50) to a linear threshold.</summary>
        private float GateThreshold()
        {
            if (settings.Gate <= 0) return 0f;
            double db = -70.0 + (settings.Gate / 50.0) * 45.0;
            return (float)Math.Pow(10.0, db / 20.0);
        }

        private void Persist()
        {
            settings.InputDeviceId = selectedInputId ?? "";
            settings.OutputDeviceId = selectedOutputId ?? "";
            if (WindowState == FormWindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
                settings.WindowX = Location.X;
                settings.WindowY = Location.Y;
            }
            settings.Save();
        }

        #endregion

        #region Lifetime

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            Persist();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing &&
                settings.MinimizeToTray && engine.IsRunning)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                trayIcon.ShowBalloonTip(2000, "Mic Flow",
                    "L'ascolto continua in background. Clicca l'icona per riaprire.", ToolTipIcon.Info);
                return;
            }

            settings.Maximized = WindowState == FormWindowState.Maximized;
            Persist();
            meterTimer.Stop();
            engine.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnFormClosing(e);
        }

        #endregion
    }

    /// <summary>Builds the application icon at runtime so no external resources are needed.</summary>
    internal static class IconFactory
    {
        private static Icon cached;

        public static Icon CreateAppIcon()
        {
            if (cached != null) return cached;

            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Color accent = Color.FromArgb(0xFF, 0x6B, 0x00);
                    using (Pen pen = new Pen(accent, 2.6f))
                    {
                        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        // Waveform bars, matching the header mark in the UI.
                        g.DrawLine(pen, 5, 13, 5, 19);
                        g.DrawLine(pen, 11, 8, 11, 24);
                        g.DrawLine(pen, 16, 3, 16, 29);
                        g.DrawLine(pen, 21, 9, 21, 23);
                        g.DrawLine(pen, 27, 13, 27, 19);
                    }
                }

                cached = Icon.FromHandle(bitmap.GetHicon());
            }
            return cached;
        }
    }
}
