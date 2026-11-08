using Niimbot.Net;
using Niimbot.Net.Commands;
using Niimbot.Net.Encoding;
using Niimbot.Net.Framing;
using Niimbot.Net.Profiles;
using Niimbot.Net.Tests.Fakes;
using Xunit;

namespace Niimbot.Net.Tests;

public class ClientTests
{
    private static (NiimbotClient client, FakeTransport transport, FakeB1Printer printer) NewB1()
    {
        var printer = new FakeB1Printer();
        var transport = new FakeTransport { Responder = printer.Respond };
        return (new NiimbotClient(transport), transport, printer);
    }

    [Theory]
    [InlineData(6400)]
    [InlineData(4608)]
    public async Task M2H_and_M3_identical_copies_use_one_job_and_one_upload(int modelId)
    {
        var printer = new FakeB1Printer { ModelId = modelId };
        var transport = new FakeTransport { Responder = printer.Respond };
        await using var client = new NiimbotClient(transport);
        await client.ConnectAsync();
        await client.PrintAsync(new MonochromeBitmap(8,2,[0xFF,0]),new PrintOptions { Copies = 2 });
        var starts = transport.WrittenPackets.Where(p => p.Command == (byte)RequestCommandId.PrintStart).ToArray();
        Assert.Single(starts); Assert.Equal(2, starts[0].Data[1]);
        Assert.Equal(modelId == 4608 ? 8 : 9, starts[0].Data.Length);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintEnd);
        var pages = transport.WrittenPackets.Where(p => p.Command == (byte)RequestCommandId.SetPageSize).ToArray();
        Assert.Single(pages); Assert.Equal(2, pages[0].Data[5]);
        Assert.Equal(modelId == 4608 ? 6 : 45, pages[0].Data.Length);
    }

    [Theory]
    [InlineData(RequestCommandId.SetDensity, ResponseCommandId.In_SetDensity)]
    [InlineData(RequestCommandId.SetLabelType, ResponseCommandId.In_SetLabelType)]
    [InlineData(RequestCommandId.PrintStart, ResponseCommandId.In_PrintStart)]
    public async Task M2H_waits_for_explicitly_rejected_setup_before_opening_second_page(RequestCommandId command, ResponseCommandId response)
    {
        var printer = new FakeB1Printer { ModelId = 4608 };
        var attempts = 0;
        var transport = new FakeTransport { Responder = packet =>
            packet.Command == (byte)command && ++attempts == 1
                ? new[] { new NiimbotPacket((byte)response, new byte[] { 0 }) }
                : printer.Respond(packet) };
        await using var client = new NiimbotClient(transport);
        await client.ConnectAsync();
        await client.PrintAsync(new MonochromeBitmap(8,2,[255,0]), new PrintOptions { Copies = 2 });
        Assert.Equal(2, attempts);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PageStart);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintEnd);
        var commands = transport.WrittenPackets.Where(p => p.Command == (byte)command).ToArray();
        Assert.Equal(commands[0].Data, commands[1].Data);
    }

    [Theory]
    [InlineData(4608)]
    [InlineData(6400)]
    public async Task Distinct_labels_share_job_and_confirm_each_counter_once(int modelId)
    {
        var printer = new FakeB1Printer { ModelId = modelId };
        var transport = new FakeTransport { Responder = printer.Respond };
        await using var client = new NiimbotClient(transport); await client.ConnectAsync();
        var confirmed = 0;
        await client.PrintContinuousAsync(3, i => new MonochromeBitmap(8,1,[(byte)(128 >> i)]),
            labelCompleted: () => confirmed++);
        Assert.Equal(3, confirmed);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintStart);
        Assert.Equal(3, transport.WrittenPackets.Count(p => p.Command == (byte)RequestCommandId.SetPageSize));
        Assert.Equal(3, transport.WrittenPackets.Count(p => p.Command == (byte)RequestCommandId.PageEnd));
        Assert.Equal(modelId == 4608 ? 3 : 0, transport.WrittenPackets.Count(p => p.Command == (byte)RequestCommandId.PageStart));
        Assert.Equal((byte)RequestCommandId.PrintEnd, transport.WrittenPackets[^1].Command);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintEnd);
    }

    [Fact]
    public async Task Failed_second_page_preserves_only_confirmed_first_label_and_does_not_retry()
    {
        var printer = new FakeB1Printer { ModelId = 4608 }; var pages = 0; var completed = 0;
        var transport = new FakeTransport { Responder = p => p.Command == (byte)RequestCommandId.PageStart && ++pages == 2
            ? new[] { new NiimbotPacket((byte)ResponseCommandId.In_PrintError, new byte[] { 6 }) } : printer.Respond(p) };
        await using var client = new NiimbotClient(transport); await client.ConnectAsync();
        await Assert.ThrowsAsync<PrintException>(() => client.PrintContinuousAsync(3,
            _ => new MonochromeBitmap(8,1,[255]), labelCompleted: () => completed++));
        Assert.Equal(1, completed);
        Assert.Equal(2, pages);
        Assert.DoesNotContain(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintEnd);
    }

    [Fact]
    public async Task Batch_cancellation_stops_queuing_and_explicit_cleanup_ends_once()
    {
        var printer = new FakeB1Printer { ModelId = 4608 };
        var transport = new FakeTransport { Responder = printer.Respond };
        await using var client = new NiimbotClient(transport); await client.ConnectAsync();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PrintContinuousAsync(3,
            _ => new MonochromeBitmap(8,1,[255]), labelCompleted: () => cancellation.Cancel(), ct: cancellation.Token));
        await client.EndPrintJobAsync();
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.SetPageSize);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintEnd);
    }

    [Fact]
    public async Task M2H_copy_failure_stops_without_retry_and_identifies_the_label_and_command()
    {
        var printer = new FakeB1Printer { ModelId = 4608 }; var pages = 0;
        var transport = new FakeTransport { Responder = p => p.Command == (byte)RequestCommandId.PageStart && ++pages == 2
            ? new[] { new NiimbotPacket((byte)ResponseCommandId.In_PrintError,new byte[] { 6 }) } : printer.Respond(p) };
        await using var client = new NiimbotClient(transport); await client.ConnectAsync();
        var error = await Assert.ThrowsAsync<PrintException>(() => client.PrintContinuousAsync(3, _ => new MonochromeBitmap(8,2,[255,0])));
        Assert.Equal(PrinterErrorCode.DataError,error.Code); Assert.Contains("PageStart",error.Message);
        Assert.Single(transport.WrittenPackets, p => p.Command == (byte)RequestCommandId.PrintStart);
    }

    [Fact]
    public async Task Cancelled_stream_can_end_job_and_print_again_without_reconnecting()
    {
        var (client, transport, printer) = NewB1();
        await using var owned = client;
        await client.ConnectAsync();
        using var cancellation = new CancellationTokenSource();
        transport.Responder = packet =>
        {
            if (packet.Command == (byte)RequestCommandId.PrintBitmapRow)
                cancellation.Cancel();
            return printer.Respond(packet);
        };
        var bitmap = new MonochromeBitmap(8, 2, [0xFF, 0x00]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PrintAsync(bitmap, ct: cancellation.Token));
        transport.Responder = printer.Respond;
        await client.EndPrintJobAsync();
        Assert.Equal((byte)RequestCommandId.PrintEnd, transport.WrittenPackets[^1].Command);
        Assert.True(client.IsConnected);
        await client.PrintAsync(bitmap);
        Assert.Equal((byte)RequestCommandId.PrintEnd, transport.WrittenPackets[^1].Command);
    }

    [Fact]
    public async Task ConnectAsync_identifies_the_B1_and_reads_capabilities()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;

        var caps = await client.ConnectAsync();

        Assert.Equal(PrinterModel.B1, caps.Model);
        Assert.Equal(4096, caps.ModelId);
        Assert.Equal(203, caps.Dpi);
        Assert.InRange(caps.MaxPrintWidthMm, 47.5, 48.5);
        Assert.True(caps.SupportsRfid);
        Assert.Equal("B1TEST01", caps.SerialNumber);
    }

    [Fact]
    public async Task GetStatusAsync_decodes_cover_paper_and_battery()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        var status = await client.GetStatusAsync();

        Assert.Equal(false, status.CoverOpen);
        Assert.Equal(true, status.PaperPresent);
        Assert.Equal(BatteryChargeLevel.Charge100, status.Battery);
    }

    [Fact]
    public async Task PrintAsync_runs_the_B1_sequence_and_streams_rows()
    {
        var (client, transport, printer) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        // A 16×4 label with one dense row and the rest blank.
        var packed = new byte[2 * 4];
        packed[2] = 0xFF;
        packed[3] = 0xFF;
        var bitmap = new MonochromeBitmap(16, 4, packed);

        await client.PrintAsync(bitmap, new PrintOptions { Copies = 1 });

        // The print-phase opcodes appear in the right order.
        var sequence = transport.WrittenPackets
            .Select(p => (RequestCommandId)p.Command)
            .Where(IsPrintPhase)
            .ToList();

        Assert.Equal(
        [
            RequestCommandId.SetDensity,
            RequestCommandId.SetLabelType,
            RequestCommandId.PrintStart,
            RequestCommandId.PageStart,
            RequestCommandId.SetPageSize,
            RequestCommandId.PageEnd,
            RequestCommandId.PrintStatus,
            RequestCommandId.PrintEnd,
        ], sequence);

        // Rows were streamed (one empty-row run + one bitmap row).
        Assert.NotEmpty(printer.ReceivedRows);
        Assert.Contains(printer.ReceivedRows, p => p.Command == (byte)RequestCommandId.PrintBitmapRow);
    }

    [Fact]
    public async Task PrintAsync_rejects_a_bitmap_wider_than_the_printhead()
    {
        var (client, _, _) = NewB1();
        await using var _c = client;
        await client.ConnectAsync();

        // 400px > B1's 384px head.
        var tooWide = new MonochromeBitmap(400, 1, new byte[(400 + 7) / 8]);
        await Assert.ThrowsAsync<ArgumentException>(() => client.PrintAsync(tooWide));
    }

    private static bool IsPrintPhase(RequestCommandId id) => id is
        RequestCommandId.SetDensity or RequestCommandId.SetLabelType or RequestCommandId.PrintStart or
        RequestCommandId.PageStart or RequestCommandId.SetPageSize or RequestCommandId.PageEnd or
        RequestCommandId.PrintStatus or RequestCommandId.PrintEnd;
}
