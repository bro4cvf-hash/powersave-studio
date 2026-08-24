namespace PowerSave.Core;

using PowerSave.Infra;

public static class BluetoothController
{
    public static async Task<(bool Ok, string Message)> ToggleAsync()
    {
        try
        {
            var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync()
                .AsTask().ConfigureAwait(false);
            var bt = radios.FirstOrDefault(r => r.Kind == Windows.Devices.Radios.RadioKind.Bluetooth);
            if (bt is null)
            {
                Logger.Info("bluetooth: no radio present");
                return (false, "No Bluetooth radio found");
            }

            bool turnOn = bt.State != Windows.Devices.Radios.RadioState.On;
            await bt.SetStateAsync(turnOn
                ? Windows.Devices.Radios.RadioState.On
                : Windows.Devices.Radios.RadioState.Off).AsTask().ConfigureAwait(false);
            Logger.Info($"bluetooth turned {(turnOn ? "on" : "off")}");
            return (true, $"Bluetooth {(turnOn ? "on" : "off")} — saves power when idle");
        }
        catch (Exception ex)
        {
            Logger.Warn("bluetooth toggle failed: " + ex.Message);
            return (false, "Bluetooth control blocked on this system");
        }
    }
}
