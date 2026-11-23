using Falcon.Haptics;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;

namespace Falcon.Diagnostics;

internal sealed class DiagnosticWindow : Form
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Falcon Diagnostics";
    static readonly Color Ink = Color.FromArgb(23, 38, 53);
    static readonly Color Muted = Color.FromArgb(63, 80, 96);
    static readonly Color Accent = Color.FromArgb(5, 98, 102);
    readonly EventWaitHandle showRequest;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    readonly Label usb = TextLabel();
    readonly Label driver = TextLabel();
    readonly Label sdk = TextLabel();
    readonly Label firmware = TextLabel();
    readonly Label result = TextLabel();
    readonly Label setupMessage = TextLabel();
    readonly Label[] axes = new Label[3];
    readonly Label[] buttons = new Label[4];
    readonly Button start = new() { Text = "Lancer le test", AutoSize = true };
    readonly Button stop = new() { Text = "Arrêter", AutoSize = true, Enabled = false };
    readonly ComboBox effectMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 138 };
    readonly NumericUpDown effectIntensity = new() { Minimum = 5, Maximum = 100, Value = 30, Width = 58 };
    readonly Button enableEffect = new() { Text = "Activer le mode", AutoSize = true, Enabled = false };
    readonly Button disableEffect = new() { Text = "Couper les forces", AutoSize = true, Enabled = false };
    readonly Label effectHint = TextLabel();
    readonly Label effectStatus = TextLabel();
    readonly Button setupButton = new() { AutoSize = true };
    readonly Control setupCard;
    readonly CheckBox startup = new() { Text = "Surveiller à l’ouverture de ma session Windows", AutoSize = true };
    UsbFalcon device;
    string libraryPath;
    string configuredPath;
    string probeError;
    string sdkVersion;
    string sdkError;
    string setupFeedback;
    long nextInspect, effectHeartbeat;
    bool quitting, settingStartup, autoStartPending, setupRunning;
    CancellationTokenSource cancellation;
    Task test;
    Reading reading;
    EffectRequest effectRequest;
    int effectGain = 30, effectOutput;
    string effectError;
    sealed record Reading(FalconSample? Sample, string Firmware, string Error, uint SeenButtons = 0, uint MovedAxes = 0);

    public DiagnosticWindow(EventWaitHandle showRequest)
    {
        this.showRequest = showRequest;
        Text = "Diagnostic Novint Falcon";
        MinimumSize = new Size(650, 625);
        ClientSize = new Size(700, 715);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(242, 246, 249);
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        Icon = SystemIcons.Information;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 1, Padding = new Padding(24, 16, 24, 12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var title = TextLabel("Novint Falcon");
        title.Font = new Font(Font.FontFamily, 26, FontStyle.Bold);
        layout.Controls.Add(title);
        var version = typeof(DiagnosticWindow).Assembly.GetName().Version;
        layout.Controls.Add(TextLabel($"Diagnostic Windows · v{version.ToString(3)} · {RuntimeInformation()}"));
        layout.Controls.Add(Card("Connexion", usb, driver, sdk, firmware));

        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
        var refresh = new Button { Text = "Actualiser", AutoSize = true };
        refresh.Click += (_, _) => Inspect(true);
        var browse = new Button { Text = "Choisir dhd64.dll", AutoSize = true };
        browse.Click += (_, _) => Browse();
        var download = new Button { Text = "SDK Force Dimension", AutoSize = true };
        download.Click += (_, _) => { try { Program.OpenSdkDownload(); } catch (Exception ex) { result.Text = ex.Message; } };
        options.Controls.AddRange(new Control[] { refresh, browse, download });
        layout.Controls.Add(options);

        setupMessage.MaximumSize = new Size(580, 0);
        setupButton.Click += async (_, _) => await InstallSetup();
        setupCard = Card("Configuration Windows", setupMessage, setupButton);
        setupCard.Visible = false;
        layout.Controls.Add(setupCard);

        var measure = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, Margin = new Padding(0, 5, 0, 5) };
        for (int i = 0; i < 3; i++)
        {
            measure.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            axes[i] = TextLabel($"{(char)('X' + i)}   — mm");
            axes[i].Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            measure.Controls.Add(axes[i], i, 0);
        }

        var keys = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Margin = new Padding(0, 4, 0, 8) };
        for (int i = 0; i < 4; i++)
        {
            buttons[i] = TextLabel($"Bouton {i + 1}");
            buttons[i].Padding = new Padding(10, 5, 10, 5);
            buttons[i].BackColor = BackColor;
            keys.Controls.Add(buttons[i]);
        }
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0) };
        start.BackColor = Accent;
        start.ForeColor = Color.White;
        start.FlatStyle = FlatStyle.Flat;
        start.Click += (_, _) => StartTest();
        stop.Click += (_, _) => CancelTest();
        actions.Controls.AddRange(new Control[] { start, stop });
        result.MaximumSize = new Size(580, 0);
        result.Text = "Après la mise sous tension, tirez doucement la poignée vers vous puis repoussez-la. Bougez-la sur les trois axes et pressez les quatre boutons.";
        layout.Controls.Add(Card("Diagnostic de la poignée", measure, keys, result, actions));
        layout.Controls.Add(BuildEffects());
        layout.Controls.Add(startup);

        menu.Items.Add("Ouvrir le diagnostic", null, (_, _) => Reveal());
        menu.Items.Add("Quitter", null, (_, _) => Quit());
        tray = new NotifyIcon { Icon = Icon, ContextMenuStrip = menu, Text = Text, Visible = true };
        tray.DoubleClick += (_, _) => Reveal();
        timer.Tick += (_, _) => Tick();
        Deactivate += (_, _) => DisarmEffect();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) DisarmEffect(); };
        FormClosing += HandleClosing;
        startup.Checked = IsStartupEnabled();
        startup.CheckedChanged += (_, _) => SetStartup();
    }

    Control BuildEffects()
    {
        effectMode.Items.AddRange(new[] { "Eau", "Brique", "Résistance", "Ressort", "Gravier", "Levier moteur" });
        effectMode.SelectedIndex = 0;
        effectMode.AccessibleName = "Mode haptique";
        effectIntensity.AccessibleName = "Intensité en pourcentage";
        effectHint.MaximumSize = new Size(580, 0);
        effectStatus.MaximumSize = new Size(580, 0);
        effectMode.SelectedIndexChanged += (_, _) => { DisarmEffect(); DescribeEffect(); };
        effectIntensity.ValueChanged += (_, _) => Volatile.Write(ref effectGain, (int)effectIntensity.Value);
        enableEffect.Click += (_, _) =>
        {
            Volatile.Write(ref effectError, null);
            Interlocked.Exchange(ref effectHeartbeat, Environment.TickCount64);
            Volatile.Write(ref effectRequest, new EffectRequest((HapticMode)effectMode.SelectedIndex));
            RenderEffect();
        };
        disableEffect.Click += (_, _) => DisarmEffect();
        DescribeEffect();
        RenderEffect();

        var controls = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0) };
        controls.Controls.AddRange(new Control[] { effectMode, TextLabel("Intensité (%)"), effectIntensity, enableEffect, disableEffect });
        return Card("Retour de force", controls, effectHint, effectStatus);
    }

    void DescribeEffect() => effectHint.Text = (HapticMode)effectMode.SelectedIndex switch
    {
        HapticMode.Water => "La poignée freine progressivement quand vous la déplacez plus vite.",
        HapticMode.Brick => "Poussez la poignée vers l’appareil pour rencontrer une paroi virtuelle.",
        HapticMode.Resistance => "La poignée oppose une résistance régulière à vos mouvements.",
        HapticMode.Spring => "Un ressort ramène la poignée vers la position où vous avez pressé le bouton 1.",
        HapticMode.Gravel => "Le frottement varie pendant le déplacement pour donner une sensation granuleuse.",
        HapticMode.EngineLever => "Le levier vibre au ralenti même immobile et revient doucement vers son point de départ.",
        _ => ""
    };

    void DisarmEffect()
    {
        Volatile.Write(ref effectRequest, null);
    }

    void RenderEffect()
    {
        bool armed = Volatile.Read(ref effectRequest) != null;
        bool applyingForce = Volatile.Read(ref effectOutput) != 0;
        enableEffect.Enabled = !armed && !applyingForce && device.Started && test is { IsCompleted: false }
            && !cancellation.IsCancellationRequested && Volatile.Read(ref reading)?.Sample != null;
        disableEffect.Enabled = armed || applyingForce;
        effectStatus.Text = Volatile.Read(ref effectError) ?? (armed
            ? applyingForce ? "Effet actif · relâchez le bouton 1 pour couper l’effort."
                            : "Maintenez le bouton 1 de la poignée pour sentir l’effet."
            : "Forces désactivées. Gardez la fenêtre active pendant le test.");
    }

    static string RuntimeInformation() => $".NET {Environment.Version} · x64";

    static Label TextLabel(string text = "") => new()
    {
        AutoSize = true, Text = text, ForeColor = Muted, Margin = new Padding(0, 3, 8, 3)
    };

    static Control Card(string heading, params Control[] contents)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, BackColor = Color.White,
            Padding = new Padding(14, 9, 14, 9), Margin = new Padding(0, 6, 0, 6)
        };
        var title = TextLabel(heading);
        title.ForeColor = Ink;
        title.Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold);
        panel.Controls.Add(title);
        foreach (Control control in contents)
            panel.Controls.Add(control);
        return panel;
    }

    internal void Start(bool background)
    {
        Inspect(true);
        timer.Start();
        if (!background)
            Reveal();
    }

    void Tick()
    {
        if (showRequest.WaitOne(0))
            Reveal();
        long now = Environment.TickCount64;
        if (Visible && Form.ActiveForm == this && WindowState != FormWindowState.Minimized)
            Interlocked.Exchange(ref effectHeartbeat, now);
        else
            DisarmEffect();
        if (now >= nextInspect)
        {
            nextInspect = now + 650;
            Inspect(false);
        }
        RenderTest();
        RenderEffect();
    }

    void Inspect(bool refreshSdk)
    {
        UsbFalcon previous = device;
        try
        {
            device = WindowsFalcon.Inspect();
            probeError = null;
        }
        catch (Exception ex)
        {
            probeError = ex.Message;
        }

        if (probeError is null && previous.Present && (!device.Present || device.Instance != previous.Instance))
        {
            CancelTest();
            ResetMeasurements();
            autoStartPending = false;
            result.Text = "Le Falcon précédent a été débranché.";
        }
        if (previous.Started && !device.Started)
            CancelTest();

        string found = configuredPath ?? FalconSdk.Find();
        if (configuredPath != null && !FalconSdk.IsLibrary(configuredPath))
        {
            configuredPath = null;
            found = FalconSdk.Find();
        }
        if (refreshSdk || found != libraryPath)
        {
            libraryPath = found;
            sdkVersion = sdkError = null;
            try
            {
                if (libraryPath != null)
                {
                    sdkVersion = FileVersionInfo.GetVersionInfo(libraryPath).ProductVersion;
                    if (string.IsNullOrWhiteSpace(sdkVersion))
                        sdkVersion = "Bibliothèque trouvée · version à lire pendant le test";
                }
            }
            catch (Exception ex)
            {
                sdkError = ex.Message;
            }
        }
        usb.Text = probeError ?? (device.Present ? "USB  ·  Falcon détecté   (0403:CB48)" : "USB  ·  Falcon débranché");
        driver.Text = !device.Present ? "Pilote  ·  En attente du Falcon"
            : device.UsesLegacyDriver ? $"Pilote  ·  FTDIBUS {device.DriverVersion} · remplacement requis pour DHD"
            : device.Started ? $"Pilote  ·  {device.Service ?? "démarré"}  {device.DriverVersion}"
            : $"Pilote  ·  {(device.Problem == 28 ? "aucun pilote compatible" : "périphérique arrêté")}  (code {device.Problem})";
        sdk.Text = $"SDK DHD  ·  {sdkError ?? sdkVersion ?? "introuvable"}";
        if (string.IsNullOrEmpty(firmware.Text))
            firmware.Text = "Firmware  ·  Lancer le test pour lire la version";
        start.Enabled = !setupRunning && probeError is null && device.Started && !device.UsesLegacyDriver && sdkError is null && libraryPath != null &&
            (test is null || test.IsCompleted);
        bool arrived = probeError is null && device.Present && device.Instance != previous.Instance;
        if (arrived)
            autoStartPending = true;
        if (autoStartPending && test is null && start.Enabled)
            StartTest();
        ShowSetup();
        if (arrived)
            Reveal();
    }

    SetupNeed CurrentSetupNeed => FalconSetup.GetNeed(device, sdkError == null ? libraryPath : null);

    void ShowSetup()
    {
        SetupNeed need = CurrentSetupNeed;
        setupCard.Visible = setupRunning || setupFeedback != null || need != SetupNeed.None;
        setupButton.Visible = need != SetupNeed.None;
        setupButton.Enabled = !setupRunning;
        setupButton.Text = need == SetupNeed.Sdk ? "Installer le SDK officiel"
            : device.UsesLegacyDriver ? "Réparer le pilote du Falcon" : "Installer le pilote USB";
        setupMessage.Text = setupFeedback ?? (need == SetupNeed.Sdk
            ? "Le SDK Force Dimension manque. Le diagnostic peut déjà détecter le branchement. L’installation du SDK et du pilote nécessite l’accord de Windows."
            : device.UsesLegacyDriver
                ? "Le pilote FTDI est associé au Falcon. La réparation installe le pilote officiel Force Dimension sur ce modèle USB. Confirmez la demande de Windows."
                : "Windows détecte le Falcon et attend son pilote USB. Confirmez la demande de Windows pour l’installer.");
    }

    async Task InstallSetup()
    {
        SetupNeed need = CurrentSetupNeed;
        if (setupRunning || need == SetupNeed.None)
            return;

        setupRunning = true;
        CancelTest();
        start.Enabled = false;
        setupFeedback = "Préparation de l’installation…";
        ShowSetup();
        try
        {
            var progress = new Progress<string>(message =>
            {
                if (setupRunning && !IsDisposed)
                {
                    setupFeedback = message;
                    ShowSetup();
                }
            });
            bool rebootRequired = await FalconSetup.InstallAsync(need, libraryPath, progress);
            if (!IsDisposed)
            {
                Inspect(true);
                setupFeedback = rebootRequired ? "Windows demande un redémarrage. Redémarrez le PC, puis rouvrez le diagnostic."
                    : device.Started && device.UsesWinUsb ? "Pilote WinUSB associé. Lancez le test pour vérifier les axes et le firmware."
                    : !device.Present ? "Branchez le Falcon pour vérifier le pilote et lancer la lecture."
                    : "L’association WinUSB reste à vérifier. Débranchez puis rebranchez le Falcon et actualisez.";
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            setupFeedback = "Installation annulée dans la fenêtre Windows.";
        }
        catch (Exception ex)
        {
            setupFeedback = ex.Message;
        }
        finally
        {
            setupRunning = false;
            if (!IsDisposed)
                Inspect(true);
        }
    }

    void Browse()
    {
        using var dialog = new OpenFileDialog { FileName = "dhd64.dll", Filter = "Force Dimension (dhd64.dll)|dhd64.dll" };
        if (dialog.ShowDialog(this) == DialogResult.OK && FalconSdk.IsLibrary(dialog.FileName))
        {
            configuredPath = dialog.FileName;
            Inspect(true);
        }
    }

    void StartTest()
    {
        if (test is { IsCompleted: false } || !start.Enabled)
            return;
        autoStartPending = false;
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        DisarmEffect();
        Volatile.Write(ref effectError, null);
        ResetMeasurements();
        result.Text = "Ouverture du Falcon…";
        start.Enabled = false;
        stop.Enabled = true;
        var path = libraryPath;
        var token = cancellation.Token;
        test = Task.Run(() =>
        {
            string version = null;
            try
            {
                if (token.IsCancellationRequested)
                    return;
                using var falcon = new FalconDevice(path);
                for (int attempt = 0; ; attempt++)
                {
                    try { falcon.Connect(); break; }
                    catch (InvalidOperationException) when (attempt < 2)
                    {
                        if (token.WaitHandle.WaitOne(400))
                            return;
                    }
                }
                version = falcon.FirmwareVersion;
                using var playback = new HapticPlayback(falcon);
                var lower = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
                var upper = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
                uint seenButtons = 0, movedAxes = 0;
                while (!token.IsCancellationRequested)
                {
                    FalconSample sample = falcon.Read();
                    seenButtons |= sample.Buttons;
                    TrackAxis(0, sample.X);
                    TrackAxis(1, sample.Y);
                    TrackAxis(2, sample.Z);
                    if (token.IsCancellationRequested)
                        break;

                    Volatile.Write(ref reading, new Reading(sample, version, null, seenButtons, movedAxes));
                    var request = Volatile.Read(ref effectRequest);
                    if (request != null && Environment.TickCount64 - Interlocked.Read(ref effectHeartbeat) > 240)
                    {
                        Interlocked.CompareExchange(ref effectRequest, null, request);
                        request = null;
                    }
                    try
                    {
                        bool applying = playback.Update(request, sample, Volatile.Read(ref effectGain));
                        Volatile.Write(ref effectOutput, applying ? 1 : 0);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref effectRequest, null, request);
                        Volatile.Write(ref effectError, ex.Message);
                        playback.Stop();
                        Volatile.Write(ref effectOutput, 0);
                    }

                    if (token.WaitHandle.WaitOne(request == null ? 16 : 2))
                        break;
                }

                void TrackAxis(int axis, double value)
                {
                    lower[axis] = Math.Min(lower[axis], value);
                    upper[axis] = Math.Max(upper[axis], value);
                    if (upper[axis] - lower[axis] >= .001)
                        movedAxes |= 1u << axis;
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Volatile.Write(ref reading, new Reading(null, version, ex.Message));
            }
            finally
            {
                DisarmEffect();
                Volatile.Write(ref effectOutput, 0);
            }
        });
    }

    void RenderTest()
    {
        if (test is null)
            return;

        Reading current = device.Started ? Volatile.Read(ref reading) : null;
        if (current?.Firmware != null)
            firmware.Text = $"Firmware  ·  {current.Firmware}";
        if (current?.Sample is FalconSample sample)
        {
            UpdateAxis(0, sample.X, current.MovedAxes);
            UpdateAxis(1, sample.Y, current.MovedAxes);
            UpdateAxis(2, sample.Z, current.MovedAxes);
            for (int i = 0; i < 4; i++)
            {
                bool pressed = !test.IsCompleted && (sample.Buttons & (1u << i)) != 0;
                bool seen = (current.SeenButtons & (1u << i)) != 0;
                buttons[i].Text = seen ? $"✓ Bouton {i + 1}" : $"Bouton {i + 1}";
                buttons[i].BackColor = pressed ? Color.LightSeaGreen : BackColor;
            }
            result.Text = current.MovedAxes == 7 && current.SeenButtons == 15
                ? "Test réussi · mouvement détecté sur les trois axes et quatre boutons essayés."
                : test.IsCompleted ? "Test arrêté. Les dernières mesures restent affichées."
                : "Lecture active · si les axes restent fixes, tirez puis repoussez doucement la poignée. Pressez chacun des quatre boutons.";
        }
        if (!test.IsCompleted)
            return;
        if (current?.Error != null)
            result.Text = current.Error.Contains("no device found", StringComparison.OrdinalIgnoreCase)
                ? "Le SDK ne peut pas ouvrir le Falcon. Vérifiez son alimentation et fermez tout autre test ou projet Unity qui l’utilise."
                : current.Error;
        stop.Enabled = false;
        test = null;
        Inspect(true);
    }

    void UpdateAxis(int i, double value, uint movedAxes)
    {
        string confirmed = (movedAxes & (1u << i)) != 0 ? " ✓" : "";
        axes[i].Text = $"{(char)('X' + i)}  {value * 1000:0.0} mm{confirmed}";
    }

    void ResetMeasurements()
    {
        Volatile.Write(ref reading, null);
        firmware.Text = "Firmware  ·  Lancer le test pour lire la version";
        for (int i = 0; i < axes.Length; i++)
            axes[i].Text = $"{(char)('X' + i)}   — mm";
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Text = $"Bouton {i + 1}";
            buttons[i].BackColor = BackColor;
        }
    }

    void CancelTest()
    {
        DisarmEffect();
        if (test is { IsCompleted: false })
            cancellation.Cancel();
        stop.Enabled = false;
    }

    void Reveal()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        if (!ContainsFocus)
        {
            tray.BalloonTipTitle = Text;
            tray.BalloonTipText = device.Present ? "Falcon détecté. Ouvrez le diagnostic pour voir son état." : "Diagnostic prêt.";
            tray.ShowBalloonTip(4500);
        }
    }

    static string StartupCommand => $"\"{Environment.ProcessPath}\" {Program.StartupArgument}";

    static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(RunValue) as string, StartupCommand, StringComparison.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    void SetStartup()
    {
        if (settingStartup)
            return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (startup.Checked)
                key.SetValue(RunValue, StartupCommand);
            else
                key.DeleteValue(RunValue, false);
        }
        catch (Exception ex)
        {
            result.Text = "Démarrage automatique — " + ex.Message;
            settingStartup = true;
            startup.Checked = IsStartupEnabled();
            settingStartup = false;
        }
    }

    void HandleClosing(object sender, FormClosingEventArgs args)
    {
        if (!quitting && args.CloseReason == CloseReason.UserClosing)
        {
            args.Cancel = true;
            DisarmEffect();
            Hide();
            return;
        }
        CancelTest();
    }

    void Quit()
    {
        quitting = true;
        CancelTest();
        timer.Stop();
        tray.Visible = false;
        Application.ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            tray.Dispose();
            menu.Dispose();
            CancelTest();
            test?.Wait(350);
            if (test is null || test.IsCompleted)
                cancellation?.Dispose();
        }
        base.Dispose(disposing);
    }
}
