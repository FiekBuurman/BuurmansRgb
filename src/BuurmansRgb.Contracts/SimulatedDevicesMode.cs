namespace BuurmansRgb.Contracts;

// When the engine shows its simulated devices. Automatic shows them only while there is no real
// device, so they make way once OpenRGB brings real devices and come back when those go.
public enum SimulatedDevicesMode
{
    Automatic,
    Always,
    Never,
}
