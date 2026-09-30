// Keeper's Codex — a single-file desktop wrapper around the codex web page.
//
// Everything the page needs (index.html, db.js, ru.js, guides.js, atlas.png, img/, fonts/)
// is zipped into the exe as one resource and served from memory. The page runs in WebView2
// (the Chromium engine Windows ships with Edge); if WebView2 is missing, the codex opens in
// the default browser through a localhost server instead.
//
// Written for the C# 5 compiler that ships with .NET Framework 4.x (csc.exe): no string
// interpolation, no ?. operator, no expression-bodied members.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: AssemblyTitle("Keeper's Codex")]
[assembly: AssemblyDescription("Graveyard Keeper 2 database and craft calculator")]
[assembly: AssemblyProduct("Keeper's Codex")]
[assembly: AssemblyCopyright("Fan tool. Game data and art belong to Lazy Bear Games / tinyBuild.")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class Program
{
    internal const string Host = "codex.local";
    internal static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeepersCodex");
    internal static Dictionary<string, byte[]> Site;
    internal static string[] Args = new string[0];

    [STAThread]
    static int Main(string[] args)
    {
        Args = args;
        AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message, "Keeper's Codex", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        try { Directory.CreateDirectory(DataDir); } catch (Exception) { }

        try { Site = LoadSite(); }
        catch (Exception ex)
        {
            MessageBox.Show("Could not read the built-in codex files:\n" + ex.Message, "Keeper's Codex",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Form form;
        if (HasArg("--browser")) form = new FallbackForm(null);
        else
        {
            // every failure that can be detected up front lands here, so the fallback dialog
            // becomes the main window instead of hiding behind an empty one
            try { form = CreateWebViewForm(); }
            catch (Exception ex) { form = new FallbackForm(Describe(ex)); }
        }
        Application.Run(form);
        return 0;
    }

    internal static bool SelfTest { get { return ArgValue("--selftest=") != null; } }

    internal static bool HasArg(string a)
    {
        foreach (string s in Args) if (string.Equals(s, a, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string ArgValue(string prefix)
    {
        foreach (string s in Args) if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return s.Substring(prefix.Length);
        return null;
    }

    internal static string Describe(Exception ex)
    {
        // compare by name: when the SDK itself failed to load, its types cannot be referenced here
        if (ex.GetType().Name == "WebView2RuntimeNotFoundException") return "WebView2 runtime is not installed";
        return ex.Message;
    }

    // Kept out of Main so the WebView2 assemblies are only touched after AssemblyResolve is registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static Form CreateWebViewForm()
    {
        PrepareLoader();
        CoreWebView2Environment.GetAvailableBrowserVersionString();   // throws right away when the runtime is absent
        return new CodexForm();
    }

    // The native loader is written next to the user data, so the exe stays a single file.
    // One folder per SDK version: an older build that still has its loader loaded is never touched.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void PrepareLoader()
    {
        byte[] lb = ReadResource("wv2loader");
        string bin = Path.Combine(Path.Combine(DataDir, "bin"),
            typeof(CoreWebView2Environment).Assembly.GetName().Version.ToString());
        Directory.CreateDirectory(bin);
        string loader = Path.Combine(bin, "WebView2Loader.dll");
        if (!File.Exists(loader) || new FileInfo(loader).Length != lb.Length)
        {
            // write privately, then move into place: another instance never sees a half-written file
            string tmp = loader + "." + Process.GetCurrentProcess().Id + ".tmp";
            File.WriteAllBytes(tmp, lb);
            try
            {
                if (File.Exists(loader)) File.Delete(loader);
                File.Move(tmp, loader);
            }
            catch (Exception)
            {
                try { File.Delete(tmp); } catch (Exception) { }
                if (!File.Exists(loader) || new FileInfo(loader).Length != lb.Length) throw;
            }
        }
        CoreWebView2Environment.SetLoaderDllFolderPath(bin);
    }

    // The managed WebView2 SDK assemblies travel inside the exe.
    static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        string res = name == "Microsoft.Web.WebView2.Core" ? "wv2core"
                   : name == "Microsoft.Web.WebView2.WinForms" ? "wv2winforms" : null;
        if (res == null) return null;
        byte[] b = ReadResource(res);
        return b == null ? null : Assembly.Load(b);
    }

    internal static byte[] ReadResource(string name)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (s == null) return null;
            MemoryStream ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }

    static Dictionary<string, byte[]> LoadSite()
    {
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("site.zip"))
        using (ZipArchive zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry en in zip.Entries)
            {
                if (en.FullName.EndsWith("/")) continue;
                using (Stream es = en.Open())
                {
                    MemoryStream ms = new MemoryStream();
                    es.CopyTo(ms);
                    files[en.FullName.Replace('\\', '/')] = ms.ToArray();
                }
            }
        }
        return files;
    }

    // "/img/art/x.png?v=1" -> "img/art/x.png"; "/" -> "index.html"
    internal static string PathOf(string absolutePath)
    {
        string p = Uri.UnescapeDataString(absolutePath ?? "/").TrimStart('/');
        int q = p.IndexOfAny(new char[] { '?', '#' });
        if (q >= 0) p = p.Substring(0, q);
        return p.Length == 0 ? "index.html" : p;
    }

    internal static string ContentType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".html": return "text/html; charset=utf-8";
            case ".js": return "text/javascript; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".png": return "image/png";
            case ".jpg": case ".jpeg": return "image/jpeg";
            case ".woff2": return "font/woff2";
            case ".ico": return "image/x-icon";
            default: return "application/octet-stream";
        }
    }

    internal static void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
    }

    internal static Icon AppIcon()
    {
        try { return new Icon(new MemoryStream(ReadResource("app.ico"))); } catch (Exception) { return null; }
    }
}

// Main window: the codex in WebView2, served from memory at https://codex.local/.
class CodexForm : Form
{
    readonly WebView2 view = new WebView2();
    readonly string stateFile = Path.Combine(Program.DataDir, "window.txt");
    bool fellBack, closing, wasMaximized;

    public CodexForm()
    {
        Text = "Keeper's Codex";
        Icon = Program.AppIcon();
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.Manual;
        LoadBounds();
        view.Dock = DockStyle.Fill;
        view.DefaultBackgroundColor = Color.FromArgb(231, 229, 217);
        Controls.Add(view);
        // after the window is on screen, so a late failure can still hide it cleanly
        Shown += async delegate { await InitAsync(); };
        Resize += delegate
        {
            if (WindowState != FormWindowState.Minimized) wasMaximized = WindowState == FormWindowState.Maximized;
        };
        FormClosing += delegate { closing = true; if (!Program.SelfTest) SaveBounds(); };
    }

    async Task InitAsync()
    {
        try
        {
            // the build self-test gets its own profile, so it never touches the user's saved state
            string profile = Path.Combine(Program.DataDir, Program.SelfTest ? "WebView2-selftest" : "WebView2");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, profile, null);
            await view.EnsureCoreWebView2Async(env);

            CoreWebView2 core = view.CoreWebView2;
            core.Settings.AreDevToolsEnabled = Program.HasArg("--devtools");
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.AddWebResourceRequestedFilter("https://" + Program.Host + "/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += delegate (object s, CoreWebView2WebResourceRequestedEventArgs e)
            {
                string path = Program.PathOf(new Uri(e.Request.Uri).AbsolutePath);
                byte[] body;
                if (Program.Site.TryGetValue(path, out body))
                    e.Response = env.CreateWebResourceResponse(new MemoryStream(body), 200, "OK",
                        "Content-Type: " + Program.ContentType(path) + "\r\nCache-Control: no-cache");
                else
                    e.Response = env.CreateWebResourceResponse(null, 404, "Not Found", "");
            };
            // Boosty, НейроБиржа and any other outside link open in the real browser
            core.NewWindowRequested += delegate (object s, CoreWebView2NewWindowRequestedEventArgs e)
            {
                e.Handled = true;
                Program.OpenExternal(e.Uri);
            };
            core.NavigationStarting += delegate (object s, CoreWebView2NavigationStartingEventArgs e)
            {
                if (!e.Uri.StartsWith("https://" + Program.Host + "/", StringComparison.OrdinalIgnoreCase)
                    && !e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    e.Cancel = true;
                    Program.OpenExternal(e.Uri);
                }
            };
            core.DocumentTitleChanged += delegate { Text = core.DocumentTitle; };

            string selftest = Program.ArgValue("--selftest=");
            if (selftest != null) core.NavigationCompleted += async delegate
            {
                // check used by the build script: counts, every tab renders, both languages work
                string r = await core.ExecuteScriptAsync(
                    "(function(){try{var e=[];for(var k in VIEWS){try{built[k]=true;VIEWS[k]()}catch(x){e.push(k+': '+x.message)}}" +
                    "setLang('ru');var ru=document.querySelector('#tabs button').textContent;setLang('en');" +
                    "return JSON.stringify({ok:true,items:DB.items.length,recipes:DB.recipes.length,dict:Object.keys(RU_DATA).length," +
                    "guides:GUIDES.length,atlas:ATLAS.count,errors:e,ruTab:ru,title:document.title,fonts:document.fonts.size})}" +
                    "catch(x){return JSON.stringify({ok:false,error:String(x)})}})()");
                File.WriteAllText(selftest, r, new UTF8Encoding(false));
                // and a picture of what the window actually shows
                await core.ExecuteScriptAsync("setLang('ru');go('buildings');window.scrollTo(0,0)");
                await Task.Delay(2500);
                using (FileStream png = File.Create(selftest + ".png"))
                    await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
                Close();
            };

            view.Source = new Uri("https://" + Program.Host + "/index.html");
        }
        catch (Exception ex)
        {
            // closing during start-up aborts WebView2; that is not a reason to open a browser
            if (fellBack || closing || IsDisposed || Disposing) return;
            fellBack = true;
            FallbackForm f = new FallbackForm(Program.Describe(ex));
            f.FormClosed += delegate { Close(); };
            Hide();
            f.Show();
        }
    }

    void LoadBounds()
    {
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        Rectangle want = new Rectangle(work.X + work.Width / 12, work.Y + work.Height / 14,
            work.Width * 5 / 6, work.Height * 6 / 7);
        bool max = false;
        try
        {
            string[] p = File.ReadAllText(stateFile).Split(',');
            Rectangle r = new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
            if (r.Width >= 400 && r.Height >= 300)
            {
                // pull the window fully onto the monitor it overlaps most (or the nearest one),
                // so the title bar and the close button are always reachable
                Rectangle wa = Screen.FromRectangle(r).WorkingArea;
                int w = Math.Min(r.Width, wa.Width), h = Math.Min(r.Height, wa.Height);
                int x = Math.Max(wa.Left, Math.Min(r.Left, wa.Right - w));
                int y = Math.Max(wa.Top, Math.Min(r.Top, wa.Bottom - h));
                want = new Rectangle(x, y, w, h);
            }
            max = p.Length > 4 && p[4] == "max";
        }
        catch (Exception) { }
        Bounds = want;
        wasMaximized = max;
        if (max) WindowState = FormWindowState.Maximized;
    }

    void SaveBounds()
    {
        try
        {
            Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            bool max = WindowState == FormWindowState.Maximized
                    || (WindowState == FormWindowState.Minimized && wasMaximized);
            File.WriteAllText(stateFile, r.X + "," + r.Y + "," + r.Width + "," + r.Height + "," + (max ? "max" : "normal"));
        }
        catch (Exception) { }
    }
}

// Fallback: serve the same files on localhost and open the default browser.
class FallbackForm : Form
{
    HttpListener listener;
    string url;

    public FallbackForm(string reason)
    {
        Text = "Keeper's Codex";
        Icon = Program.AppIcon();

        string selftest = Program.ArgValue("--selftest=");
        if (selftest != null)
        {
            // build self-test: never open a browser or wait for a person; say why and quit
            try { File.WriteAllText(selftest, "FALLBACK: " + (reason ?? "--browser"), new UTF8Encoding(false)); }
            catch (Exception) { }
            Opacity = 0; ShowInTaskbar = false;
            Shown += delegate { Close(); };
            return;
        }

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(470, 190);

        Label text = new Label();
        text.AutoSize = true;                  // grows downward to fit the text
        text.MaximumSize = new Size(434, 0);   // wraps at this width
        text.Location = new Point(18, 16);
        Controls.Add(text);

        Button open = new Button();
        open.Text = "Открыть снова / Open again";
        open.SetBounds(18, 138, 210, 32);
        open.Click += delegate { if (url != null) Program.OpenExternal(url); };
        Controls.Add(open);

        Button quit = new Button();
        quit.Text = "Закрыть / Quit";
        quit.SetBounds(242, 138, 140, 32);
        quit.Click += delegate { Close(); };
        Controls.Add(quit);

        try
        {
            Start();
            text.Text = "Справочник открыт в браузере: " + url + "\n" +
                "Пока это окно открыто, он работает. Закройте окно — и справочник остановится.\n\n" +
                "The codex is open in your browser. It keeps running while this window is open." +
                (reason != null ? "\n\n(" + reason + ")" : "");
            if (!Program.HasArg("--no-open")) Program.OpenExternal(url);
        }
        catch (Exception ex)
        {
            text.Text = "Не удалось запустить справочник / Could not start the codex:\n" + ex.Message;
        }
        // after auto-scaling, so the numbers are real pixels at any DPI
        Load += delegate
        {
            int y = text.Bottom + 14;
            open.Top = y; quit.Top = y;
            ClientSize = new Size(ClientSize.Width, y + open.Height + 16);
        };
        FormClosed += delegate { try { if (listener != null) listener.Close(); } catch (Exception) { } };
    }

    // The page keeps its settings in localStorage, which belongs to the origin (the port),
    // so the same port is reused from one launch to the next.
    void Start()
    {
        string portFile = Path.Combine(Program.DataDir, "port.txt");
        string fixedPort = Program.ArgValue("--port=");
        int saved = 0;
        try { saved = int.Parse(File.ReadAllText(portFile).Trim()); } catch (Exception) { }
        Random rnd = new Random();
        int used = 0;
        for (int attempt = 0; attempt < 25 && listener == null; attempt++)
        {
            int port = fixedPort != null ? int.Parse(fixedPort)
                     : attempt == 0 && saved > 1024 && saved < 65536 ? saved
                     : attempt <= 1 ? 38791
                     : rnd.Next(49500, 60000);
            HttpListener l = new HttpListener();
            l.Prefixes.Add("http://localhost:" + port + "/");
            try { l.Start(); listener = l; used = port; url = "http://localhost:" + port + "/"; }
            catch (HttpListenerException) { l.Close(); if (fixedPort != null) break; }
        }
        if (listener == null) throw new InvalidOperationException("no free localhost port");
        if (fixedPort == null && used != saved)
            try { File.WriteAllText(portFile, used.ToString()); } catch (Exception) { }
        Thread t = new Thread(Serve);
        t.IsBackground = true;
        t.Start();
    }

    void Serve()
    {
        while (listener != null && listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = listener.GetContext(); } catch (Exception) { return; }
            try
            {
                string path = Program.PathOf(ctx.Request.Url.AbsolutePath);
                byte[] body;
                if (Program.Site.TryGetValue(path, out body))
                {
                    ctx.Response.ContentType = Program.ContentType(path);
                    ctx.Response.ContentLength64 = body.Length;
                    ctx.Response.OutputStream.Write(body, 0, body.Length);
                }
                else ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
            catch (Exception) { try { ctx.Response.Abort(); } catch (Exception) { } }
        }
    }
}
