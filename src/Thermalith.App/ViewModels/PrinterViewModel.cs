using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Niimbot.Net;
using Niimbot.Net.Capabilities;
using Niimbot.Net.Commands;
using Niimbot.Net.Transport;
using Thermalith.App.Services;
using Thermalith.Core.Serialization;

namespace Thermalith.App.ViewModels;

/// <summary>A discovered serial port and whether a NIIMBOT answered a probe on it (build spec §5.1).</summary>
public sealed record PortItem(string Port, string Label, bool IsNiimbot);

/// <summary>
/// The printer panel (build spec §7): discover/probe serial ports, connect, show paper/cover/battery
/// status, set density/copies/label-type + a calibration offset, and print the Core-rendered raster
/// over <c>Niimbot.Net</c>. Connection lifetime is owned here; the client is disposed on disconnect.
/// </summary>
public sealed partial class PrinterViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private NiimbotClient? _client;
    private PrinterCapabilities? _caps;
    private readonly PrinterOffsetStore _offsetStore = new(Path.Combine(PlatformDirectories.AppData(), "printer-offsets.json"));
    private bool _restoringOffsets;

    public PrinterViewModel(EditorViewModel editor) => _editor = editor;

    public ObservableCollection<PortItem> Ports { get; } = [];
    public ObservableCollection<LabelType> LabelTypes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private PortItem? _selectedPort;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshPortsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeLabelsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeLabelsCommand))]
    private bool _isConnected;

    [ObservableProperty] private string _connectionInfo = "Not connected.";
    [ObservableProperty] private string _statusInfo = "";

    /// <summary>Concise loaded-roll summary for the main interface, e.g. "B1 · 59/96 labels".</summary>
    [ObservableProperty] private string _loadedRollText = "No printer";

    /// <summary>The RFID of the currently-loaded roll (set on connect/refresh), or null. Used by the shell to resolve/prompt.</summary>
    public RfidInfo? LoadedRfid { get; private set; }

    // ── Matched roll (GitHub #18) ─────────────────────────────────────────────────────────────
    // A known barcode used to be applied in silence, so there was no way to see what the app had
    // matched or to correct a definition that was saved wrong. The shell resolves the barcode and
    // pushes the result here; the panel shows it and offers an edit.

    /// <summary>Name of the learned-catalogue roll matched to the loaded tag, or null when the roll
    /// is unknown, untagged, or no printer is connected.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchedRoll))]
    private string? _matchedRollName;

    /// <summary>One-line details of the matched roll, e.g. "50 × 30 mm · Gap · 198 left".</summary>
    [ObservableProperty] private string? _matchedRollDetail;

    public bool HasMatchedRoll => !string.IsNullOrEmpty(MatchedRollName);

    /// <summary>Raised when the user asks to review or correct the matched roll. The shell owns the
    /// roll store and the dialog, so it handles this the same way it handles a first-time definition.</summary>
    public event EventHandler? EditMatchedRollRequested;

    [RelayCommand]
    private void EditMatchedRoll() => EditMatchedRollRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Show (or clear, with nulls) the roll the shell matched for the loaded tag.</summary>
    public void SetMatchedRoll(string? name, string? detail)
    {
        MatchedRollName = name;
        MatchedRollDetail = detail;
    }

    /// <summary>Labels left on the loaded roll (Total − Used from RFID), or null when the roll count is
    /// unknown (no tag / non-RFID model). Drives the data-merge roll-capacity guard (GitHub #7).</summary>
    public int? RemainingLabels =>
        LoadedRfid is { TagPresent: true } r && r.TotalLabels > 0
            ? r.TotalLabels - (r.UsedLabels < 0 ? 0 : r.UsedLabels)
            : null;

    /// <summary>DPI of the connected printer, or null.</summary>
    public int? ConnectedDpi => _caps?.Dpi;

    /// <summary>The connected printer's feed direction, or null when disconnected. <c>Left</c> marks a
    /// side-fed D-series whose narrow head needs the canvas oriented across it.</summary>
    public Niimbot.Net.Profiles.PrintDirection? ConnectedPrintDirection => _caps?.PrintDirection;

    /// <summary>
    /// The connected printer's printable width in mm — the printhead pixel limit converted to mm and
    /// rounded down to 0.1 mm so a canvas at this width never exceeds the printhead (worklist §A6).
    /// Null when disconnected. The B1 reports 384 px @ 203 dpi → 48.0 mm (its 50 mm stock has ~1 mm
    /// unprintable margins each side).
    /// </summary>
    public double? ConnectedPrintableWidthMm => _caps is { Dpi: > 0, PrintheadPixels: > 0 } c
        ? Math.Floor(c.PrintheadPixels / (c.Dpi / 25.4) * 10) / 10
        : null;

    /// <summary>Raised after a connect/refresh so the shell can look up or prompt for the loaded roll.</summary>
    public event EventHandler? RollDetected;

    /// <summary>Raised after a successful connect so the shell can persist the last printer (port + model).</summary>
    public event EventHandler? Connected;

    /// <summary>The port we connected on / the connected model — for persistence + startup reconnect.</summary>
    public string? ConnectedPort { get; private set; }
    public string? ConnectedModel => _caps?.ModelName;
    [ObservableProperty] private string _message = "";
    // Nullable so the bound ComboBox can briefly hold null while LabelTypes is cleared on reconnect
    // (binding a null back into a non-nullable enum threw InvalidCastException on the interface).
    [ObservableProperty] private LabelType? _selectedLabelType = LabelType.WithGaps;
    [ObservableProperty] private int _density = 5;
    [ObservableProperty] private int _densityMin = 1;
    [ObservableProperty] private int _densityMax = 5;
    [ObservableProperty] private int _copies = 1;
    [ObservableProperty] private int _offsetX;
    [ObservableProperty] private int _offsetY;

    partial void OnOffsetXChanged(int value) => SaveOffsets();
    partial void OnOffsetYChanged(int value) => SaveOffsets();

    private void RestoreOffsets()
    {
        _restoringOffsets = true;
        try
        {
            var offsets = _offsetStore.Load(_caps?.SerialNumber);
            OffsetX = offsets.X;
            OffsetY = offsets.Y;
        }
        catch
        {
            // A missing/unreadable calibration must never leak the previous printer's offsets.
            OffsetX = OffsetY = 0;
        }
        finally { _restoringOffsets = false; }
    }

    private void SaveOffsets()
    {
        if (_restoringOffsets || !IsConnected || string.IsNullOrWhiteSpace(_caps?.SerialNumber)) return;
        try { _offsetStore.Save(_caps.SerialNumber, new PrinterOffsets(OffsetX, OffsetY)); }
        catch (Exception ex) { Message = "Could not save printer offsets: " + ex.Message; }
    }

    // ── Discovery ────────────────────────────────────────────────────────────────────────────

    // Greys while any operation is in flight (the busy cursor + spinner give the visible feedback).
    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task RefreshPortsAsync()
    {
        IsBusy = true;
        Message = "Scanning ports…";
        Ports.Clear();
        try
        {
            var found = 0;
            foreach (var info in SerialPortEnumerator.Enumerate())
            {
                Message = $"Probing {info.PortName}…";
                var probe = await PrinterProbe.ProbeAsync(info.PortName);
                if (probe is not null) found++;
                var transport = SerialPortTransport.Describe(info.PortName);
                var tag = transport is null ? "" : $" ({transport})";
                var label = (probe is null ? $"{info.PortName} — no printer" : $"{info.PortName} — {probe.ModelName}") + tag;
                Ports.Add(new PortItem(info.PortName, label, probe is not null));
            }
            SelectedPort = Ports.FirstOrDefault(p => p.IsNiimbot) ?? Ports.FirstOrDefault();
            Message = Ports.Count == 0
                ? "No serial ports found."
                : found > 0
                    ? $"Found {found} printer(s) across {Ports.Count} port(s)."
                    : $"Scanned {Ports.Count} port(s); no NIIMBOT found.";
        }
        catch (Exception ex)
        {
            Message = "Scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Connection ───────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        IsBusy = true;
        Message = $"Connecting to {SelectedPort.Port}…";
        try
        {
            _client = NiimbotClient.FromSerialPort(SelectedPort.Port);
            _caps = await _client.ConnectAsync();
            IsConnected = true;
            RestoreOffsets();

            DensityMin = _caps.DensityMin;
            DensityMax = _caps.DensityMax;
            Density = _caps.DensityMax;
            LabelTypes.Clear();
            foreach (var t in _caps.SupportedLabelTypes) LabelTypes.Add(t);
            SelectedLabelType = LabelTypes.Count > 0 ? LabelTypes[0] : LabelType.WithGaps;

            ConnectionInfo = $"{_caps.ModelName} · {_caps.Dpi} dpi · head {_caps.PrintheadPixels}px"
                + (_caps.FirmwareVersion is { Length: > 0 } fw ? $" · fw {fw}" : "");
            Message = "Connected.";
            ConnectedPort = SelectedPort.Port;
            Connected?.Invoke(this, EventArgs.Empty); // shell persists this printer for startup reconnect
            await RefreshStatusCoreAsync();
            RollDetected?.Invoke(this, EventArgs.Empty); // let the shell resolve/prompt the loaded roll
        }
        catch (Exception ex)
        {
            Message = "Connect failed: " + ex.Message;
            await SafeDisposeAsync();
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Startup background scan + reconnect: pre-fills the port list, then connects if the last
    /// printer is found (same port, the single NIIMBOT matching its model, or — when we've connected
    /// before and only one port exists — that lone port even if the probe didn't ID it). Otherwise just
    /// pre-selects. Runs off the UI thread (fire-and-forget) so the user can keep working while it scans.</summary>
    public async Task AutoConnectAsync(string? lastPort, string? lastModel)
    {
        // We've reconnected before iff there's a saved printer. Gates the lone-port fallback below so a
        // first-ever run with one unrelated serial device doesn't blindly dial it.
        var hasHistory = lastPort is { Length: > 0 } || lastModel is { Length: > 0 };

        // Retry the scan a few times: right after a previous instance closes, the OS/driver can still be
        // releasing the COM port (USB/Bluetooth serial), so the first probe may miss the printer.
        for (var attempt = 0; attempt < 3 && !IsConnected; attempt++)
        {
            if (attempt > 0) await Task.Delay(1200);
            if (IsConnected || IsBusy) return; // user connected/scanned meanwhile
            await RefreshPortsAsync();
            if (IsConnected || IsBusy) return;

            var target = Ports.FirstOrDefault(p => p.IsNiimbot && p.Port == lastPort)
                ?? (lastModel is { Length: > 0 } && Ports.Count(p => p.IsNiimbot && p.Label.Contains(lastModel)) == 1
                    ? Ports.First(p => p.IsNiimbot && p.Label.Contains(lastModel))
                    : Ports.Count(p => p.IsNiimbot) == 1 ? Ports.First(p => p.IsNiimbot) : null)
                // Connected before + exactly one port present → just try it, even if the probe came back
                // "no printer". The probe is flaky right after a prior instance closes; a lone port is
                // almost certainly the printer. A wrong guess just fails ConnectAsync and the loop retries.
                ?? (hasHistory && Ports.Count == 1 ? Ports[0] : null);
            if (target is null) continue; // nothing to try yet — retry (port may still be releasing)

            SelectedPort = target;
            await ConnectAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            if (_client is not null) await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Message = "Disconnect: " + ex.Message;
        }
        finally
        {
            await SafeDisposeAsync();
            IsConnected = false;
            ConnectionInfo = "Not connected.";
            StatusInfo = "";
            LoadedRollText = "No printer";
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshStatusAsync()
    {
        IsBusy = true;
        try { await RefreshStatusCoreAsync(); }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Re-read the loaded roll after the user swaps labels (the protocol gives no change event, so this is
    /// explicit). Re-reads RFID, then raises <see cref="RollDetected"/> so the shell resolves/prompts as on connect.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ChangeLabelsAsync()
    {
        IsBusy = true;
        Message = "Reading new roll…";
        try
        {
            await RefreshStatusCoreAsync();
            RollDetected?.Invoke(this, EventArgs.Empty);
            Message = "Roll updated.";
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshStatusCoreAsync()
    {
        if (_client is null) return;
        try
        {
            var s = await _client.GetStatusAsync();
            StatusInfo = string.Join("   ",
                $"cover: {Describe(s.CoverOpen, "open", "closed")}",
                $"paper: {Describe(s.PaperPresent, "present", "out")}",
                $"battery: {(s.Battery is { } b ? b.ToString() : "—")}",
                s.Temperature is { } t ? $"temp: {t}°C" : "");

            // Refresh the loaded-roll label count (consumed labels change as you print).
            try { UpdateLoadedRoll(await _client.GetRfidInfoAsync()); }
            catch { UpdateLoadedRoll(null); }
        }
        catch (Exception ex)
        {
            StatusInfo = "status unavailable: " + ex.Message;
        }
    }

    /// <summary>Set the selected print label-type from a roll's paper-type string (drives SetLabelType at print).</summary>
    [ObservableProperty] private bool _allowMediaOverride;

    public void ApplyPaperType(string? paperType)
    {
        SelectedLabelType = paperType?.ToLowerInvariant() switch
        {
            "black" => LabelType.Black,
            "continuous" => LabelType.Continuous,
            "transparent" => LabelType.Transparent,
            _ => LabelType.WithGaps,
        };
    }

    private void UpdateLoadedRoll(RfidInfo? rfid)
    {
        LoadedRfid = rfid;
        if (!IsConnected || _caps is null)
        {
            LoadedRollText = "No printer";
            SetMatchedRoll(null, null);   // nothing loaded — drop any stale match
            return;
        }
        var model = _caps.ModelName;
        if (rfid is { TagPresent: true } r && r.TotalLabels > 0)
        {
            var used = r.UsedLabels < 0 ? 0 : r.UsedLabels;
            LoadedRollText = $"{model} · {r.TotalLabels - used}/{r.TotalLabels} labels";
        }
        else
        {
            LoadedRollText = $"{model} connected";
        }
    }

    public Func<Task<bool>>? ConfirmLoadedLabelAsync { get; set; }

    // Read the tag immediately before printing; never silently resize the document or change media settings.
    public async Task<RfidInfo?> ReadLoadedRollForPrintAsync()
    {
        if (_client is null) return null;
        var rfid = await _client.GetRfidInfoAsync();
        UpdateLoadedRoll(rfid);
        return rfid;
    }

    public Func<string, Task<bool>>? ConfirmParameterSequenceAsync { get; set; }
    private async Task<bool> ConfirmParameterRunAsync(int count)
    {
        var incrementing = _editor.HasIncrementingParameters;
        var summary = _editor.BeginParameterPrintRun(count);
        if (!incrementing) return true;
        if (ConfirmParameterSequenceAsync is not null) return await ConfirmParameterSequenceAsync(summary);
        Message = "Auto-increment printing requires confirmation.";
        return false;
    }
    public Func<int, Task<bool>>? ConfirmLargeCopyCountAsync { get; set; }

    private async Task<bool> ConfirmCopiesAsync(int copies)
    {
        if (copies <= 20) return true;
        if (ConfirmLargeCopyCountAsync is not null)
            return await ConfirmLargeCopyCountAsync(copies);
        Message = "Printing more than 20 copies requires confirmation.";
        return false;
    }

    private CancellationTokenSource? _printCancellation;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelPrintCommand))]
    [NotifyPropertyChangedFor(nameof(PrintButtonText))]
    [NotifyPropertyChangedFor(nameof(PrintButtonCommand))]
    private bool _isPrinting;

    public string PrintButtonText => IsPrinting ? "Cancel Print" : "Print";
    public System.Windows.Input.ICommand PrintButtonCommand => IsPrinting ? CancelPrintCommand : PrintCommand;

    private static Niimbot.Net.Encoding.MonochromeBitmap RenderOnUi(Func<Niimbot.Net.Encoding.MonochromeBitmap> render) =>
        Avalonia.Threading.Dispatcher.UIThread.CheckAccess() ? render()
            : Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(render).GetAwaiter().GetResult();

    private bool CanCancelPrint() => IsPrinting && _printCancellation is { IsCancellationRequested: false };

    [RelayCommand(CanExecute = nameof(CanCancelPrint))]
    private void CancelPrint()
    {
        _printCancellation?.Cancel();
        CancelPrintCommand.NotifyCanExecuteChanged();
        Message = "Cancelling print…";
    }

    private async Task EndCancelledPrintAsync()
    {
        try
        {
            if (_client is not null) await _client.EndPrintJobAsync();
            Message = "Print cancelled. A label already buffered by the printer may still finish.";
        }
        catch (Exception ex) { Message = "Sending stopped; could not confirm printer job end: " + ex.Message; }
    }

    private void FinishPrint()
    {
        _editor.EndParameterPrintRun();
        IsPrinting = false;
        _printCancellation?.Dispose();
        _printCancellation = null;
        IsBusy = false;
    }

    // ── Print ────────────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task PrintAsync()
    {
        if (_client is null || !IsConnected || IsBusy) return;
        IsBusy = true;
        _printCancellation = new CancellationTokenSource();
        IsPrinting = true;
        Message = "Rendering…";
        try
        {
            var requestedCopies = Math.Max(1, Copies);
            if (!await ConfirmCopiesAsync(requestedCopies)) { Message = "Print cancelled."; return; }
            if (ConfirmLoadedLabelAsync is not null && !await ConfirmLoadedLabelAsync()) { Message = "Print cancelled."; return; }
            _printCancellation.Token.ThrowIfCancellationRequested();
            if (!await ConfirmParameterRunAsync(requestedCopies)) { Message = "Print cancelled."; return; }
            var mono = _editor.RenderForPrint();
            var rawW = mono.WidthPx;
            var rawH = mono.HeightPx;
            if (_caps is { } caps && mono.WidthPx > caps.PrintheadPixels)
                // Canvas wider than the printhead → crop the centred printable strip (the ~1mm/side the
                // head can't reach); content there is mechanically unprintable (crop-don't-block, §F).
                mono = CropCentered(mono, caps.PrintheadPixels);

            // Diagnostic for the B4 "only the top prints" report: what we actually hand the printer.
            var diag = $"raster {rawW}×{rawH}px → {mono.WidthPx}×{mono.HeightPx}px sent " +
                       $"(head {_caps?.PrintheadPixels}px), orient {_editor.OrientationDeg}°, " +
                       $"canvas {_editor.CanvasWidthMm:0.#}×{_editor.CanvasHeightMm:0.#}mm @ {_caps?.Dpi}dpi";
            Console.WriteLine("[Thermalith print] " + diag);

            var options = new PrintOptions
            {
                Density = Density,
                Copies = _editor.HasIncrementingParameters ? 1 : requestedCopies,
                LabelType = SelectedLabelType ?? LabelType.WithGaps,
                HorizontalAlign = PrintAlignment.Center,
                OffsetXPx = OffsetX,
                OffsetYPx = OffsetY,
            };
            var progress = new Progress<PrintProgress>(p =>
            {
                if (IsPrinting && _printCancellation is { IsCancellationRequested: false })
                    Message = $"Printing {p.Page}/{p.TotalPages} — {p.PagePrintPercent}%";
            });

            if (_editor.HasIncrementingParameters)
            {
                if (_client.SupportsContinuousBatch)
                {
                    await _client.PrintContinuousAsync(requestedCopies, label =>
                    {
                        var raster = label == 0 ? mono : RenderOnUi(() => _editor.RenderForPrint(label));
                        return _caps is { } head && raster.WidthPx > head.PrintheadPixels ? CropCentered(raster, head.PrintheadPixels) : raster;
                    }, options, _editor.CompleteParameterLabel, progress, _printCancellation.Token);
                }
                else
                for (var label = 0; label < requestedCopies; label++)
                {
                    _printCancellation.Token.ThrowIfCancellationRequested();
                    var raster = label == 0 ? mono : _editor.RenderForPrint(label);
                    if (_caps is { } head && raster.WidthPx > head.PrintheadPixels)
                        raster = CropCentered(raster, head.PrintheadPixels);
                    Message = $"Printing label {label + 1}/{requestedCopies}…";
                    try { await _client.PrintAsync(raster, options, ct: _printCancellation.Token); }
                    catch (PrintException ex) { throw new PrintException($"Label {label + 1}/{requestedCopies}: {ex.Message}",ex.Code,ex); }
                    _editor.CompleteParameterLabel();
                }
            }
            else await _client.PrintAsync(mono, options, progress, _printCancellation.Token);
            Message = "Printed.";
            await RefreshStatusCoreAsync();
        }
        catch (OperationCanceledException) when (_printCancellation?.IsCancellationRequested == true)
        {
            await EndCancelledPrintAsync();
        }
        catch (Exception ex)
        {
            Message = "Print failed: " + ex.Message;
        }
        finally { FinishPrint(); }
    }

    /// <summary>
    /// Batch-print a data-merge run (GitHub #7): render + send one label per row, honouring the current
    /// Copies setting, reporting rows-printed via <paramref name="progress"/>. Cancellation is checked
    /// only <i>between</i> rows via <paramref name="cancelled"/>, so the in-flight label always finishes
    /// feeding — a roll change may swap label size/type, so we never auto-continue past it. Returns the
    /// number of rows printed.
    /// </summary>
    public async Task<int> PrintMergeAsync(
        int rowCount,
        Func<int, int, Niimbot.Net.Encoding.MonochromeBitmap> renderRow,
        IProgress<int>? progress,
        Func<bool> cancelled)
    {
        if (_client is null || !IsConnected || IsBusy) return 0;
        IsBusy = true;
        _printCancellation = new CancellationTokenSource();
        IsPrinting = true;
        var printed = 0;
        try
        {
            var copies = Math.Max(1, Copies);
            if (rowCount > 0 && !await ConfirmCopiesAsync(copies)) { Message = "Print cancelled."; return 0; }
            if (rowCount > 0 && ConfirmLoadedLabelAsync is not null && !await ConfirmLoadedLabelAsync()) { Message = "Print cancelled."; return 0; }
            if (rowCount > 0 && !await ConfirmParameterRunAsync(checked(rowCount * copies))) { Message = "Print cancelled."; return 0; }
            if (_client.SupportsContinuousBatch && rowCount > 0)
            {
                var incrementing = _editor.HasIncrementingParameters;
                var batchOptions = new PrintOptions
                {
                    Density = Density, Copies = incrementing ? 1 : copies,
                    LabelType = SelectedLabelType ?? LabelType.WithGaps,
                    HorizontalAlign = PrintAlignment.Center, OffsetXPx = OffsetX, OffsetYPx = OffsetY,
                };
                var confirmed = 0;
                await _client.PrintContinuousAsync(incrementing ? checked(rowCount * copies) : rowCount, index =>
                {
                    if (cancelled()) _printCancellation.Cancel();
                    _printCancellation.Token.ThrowIfCancellationRequested();
                    var raster = RenderOnUi(() => renderRow(incrementing ? index / copies : index, incrementing ? index : checked(index * copies)));
                    return _caps is { } head && raster.WidthPx > head.PrintheadPixels ? CropCentered(raster, head.PrintheadPixels) : raster;
                }, batchOptions, () =>
                {
                    if (incrementing) _editor.CompleteParameterLabel();
                    if (++confirmed % copies == 0) { printed++; progress?.Report(printed); }
                }, ct: _printCancellation.Token);
            }
            else
            for (var i = 0; i < rowCount; i++)
            {
                if (cancelled() || _printCancellation.IsCancellationRequested) break; // stop before the next row
                var mono = renderRow(i, checked(i * copies));
                if (_caps is { } caps && mono.WidthPx > caps.PrintheadPixels)
                    mono = CropCentered(mono, caps.PrintheadPixels);
                var options = new PrintOptions
                {
                    Density = Density,
                    Copies = _editor.HasIncrementingParameters ? 1 : copies,
                    LabelType = SelectedLabelType ?? LabelType.WithGaps,
                    HorizontalAlign = PrintAlignment.Center,
                    OffsetXPx = OffsetX,
                    OffsetYPx = OffsetY,
                };
                if (_editor.HasIncrementingParameters)
                {
                    for (var copy = 0; copy < copies; copy++)
                    {
                        if (cancelled()) return printed;
                        _printCancellation.Token.ThrowIfCancellationRequested();
                        var raster = copy == 0 ? mono : renderRow(i, checked(i * copies + copy));
                        if (_caps is { } head && raster.WidthPx > head.PrintheadPixels)
                            raster = CropCentered(raster, head.PrintheadPixels);
                        await _client.PrintAsync(raster, options, ct: _printCancellation.Token);
                        _editor.CompleteParameterLabel();
                    }
                }
                else await _client.PrintAsync(mono, options, ct: _printCancellation.Token);
                printed++;
                progress?.Report(printed);
            }
        }
        catch (OperationCanceledException) when (_printCancellation?.IsCancellationRequested == true)
        {
            await EndCancelledPrintAsync();
        }
        finally
        {
            try { await RefreshStatusCoreAsync(); } catch { /* best-effort roll refresh */ }
            FinishPrint();
        }
        return printed;
    }

    /// <summary>Crop a 1bpp raster to a centred target width (drop the equal margins each side).</summary>
    private static Niimbot.Net.Encoding.MonochromeBitmap CropCentered(Niimbot.Net.Encoding.MonochromeBitmap src, int targetWidthPx)
    {
        if (targetWidthPx >= src.WidthPx) return src;
        var h = src.HeightPx;
        var x0 = (src.WidthPx - targetWidthPx) / 2;
        var bpr = (targetWidthPx + 7) / 8;
        var packed = new byte[bpr * h];
        for (var y = 0; y < h; y++)
        {
            var row = src.Row(y);
            for (var x = 0; x < targetWidthPx; x++)
            {
                var sx = x0 + x;
                if ((row[sx >> 3] & (0x80 >> (sx & 7))) != 0)
                    packed[y * bpr + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }
        return new Niimbot.Net.Encoding.MonochromeBitmap(targetWidthPx, h, packed);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────────

    /// <summary>Best-effort teardown on window close (fire-and-forget; the OS reclaims the port regardless).</summary>
    public void Shutdown() => _ = SafeDisposeAsync();

    private async Task SafeDisposeAsync()
    {
        if (_client is null) return;
        try { await _client.DisposeAsync(); }
        catch { /* already gone */ }
        _client = null;
    }

    // ── CanExecute ───────────────────────────────────────────────────────────────────────────

    private bool NotBusy() => !IsBusy;
    private bool CanConnect() => !IsBusy && !IsConnected && SelectedPort is not null;
    private bool CanOperate() => !IsBusy && IsConnected;

    private static string Describe(bool? value, string whenTrue, string whenFalse) =>
        value is null ? "—" : value.Value ? whenTrue : whenFalse;
}
