using Thermalith.Core.Serialization;
using Xunit;

namespace Thermalith.Core.Tests;

public class PrinterOffsetStoreTests
{
    [Fact]
    public void Serial_calibration_survives_restart_and_stays_separate_for_two_printers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "thermalith-offset-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "offsets.json");
        try
        {
            var store = new PrinterOffsetStore(path);
            Assert.Equal(new PrinterOffsets(), store.Load("new-printer"));
            store.Save("printer-A", new(3, -10));
            store.Save("printer-B", new(-2, 7));
            var restarted = new PrinterOffsetStore(path);
            Assert.Equal(new PrinterOffsets(3, -10), restarted.Load("printer-A"));
            Assert.Equal(new PrinterOffsets(-2, 7), restarted.Load("printer-B"));
            restarted.Save("printer-A", new(0, 0));
            Assert.Equal(new PrinterOffsets(), store.Load("printer-A"));
            Assert.Equal(new PrinterOffsets(-2, 7), store.Load("printer-B"));
            Assert.Equal(new PrinterOffsets(), store.Load(null));
            Assert.Equal(new PrinterOffsets(), store.Load("   "));
            Assert.Throws<ArgumentException>(() => store.Save("", new()));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
