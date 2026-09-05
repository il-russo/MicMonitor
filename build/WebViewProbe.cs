// Smoke test: hosts WebView2 in a WinForms window, loads inline HTML and
// verifies the JavaScript -> C# bridge round trip. Writes the outcome to
// probe.log and exits.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class WebViewProbe
{
    private static readonly string LogPath = Path.Combine(
        Path.GetDirectoryName(Application.ExecutablePath), "probe.log");

    private static void Log(string line)
    {
        File.AppendAllText(LogPath, line + Environment.NewLine);
    }

    [STAThread]
    private static void Main()
    {
        File.WriteAllText(LogPath, "start " + DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine);
        Application.EnableVisualStyles();

        Form form = new Form();
        form.Text = "probe";
        form.ClientSize = new System.Drawing.Size(600, 400);

        WebView2 view = new WebView2();
        view.Dock = DockStyle.Fill;
        form.Controls.Add(view);

        form.Shown += async delegate
        {
            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MicMonitor", "webview");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData, null);
                await view.EnsureCoreWebView2Async(environment);

                Log("runtime version: " + environment.BrowserVersionString);

                view.CoreWebView2.WebMessageReceived += delegate(object s, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    Log("bridge message: " + e.TryGetWebMessageAsString());
                    Log("RESULT: OK");
                    form.Close();
                };

                view.CoreWebView2.NavigateToString(
                    "<html><body style='background:#0c0e12;color:#ff6b00;font-family:sans-serif'>" +
                    "<h1>probe</h1><script>" +
                    "window.chrome.webview.postMessage('hello-from-js:'+devicePixelRatio);" +
                    "</script></body></html>");
            }
            catch (Exception ex)
            {
                Log("RESULT: FAIL " + ex.GetType().Name + ": " + ex.Message);
                form.Close();
            }
        };

        // Safety net so the probe never hangs a build.
        Timer timeout = new Timer();
        timeout.Interval = 20000;
        timeout.Tick += delegate { Log("RESULT: TIMEOUT"); form.Close(); };
        timeout.Start();

        Application.Run(form);
    }
}
