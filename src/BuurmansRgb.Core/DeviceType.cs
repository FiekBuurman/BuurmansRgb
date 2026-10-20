namespace BuurmansRgb.Core;

// The number of a type goes over the pipe, so a new type gets a new number and an existing
// number never changes.
public enum DeviceType
{
    Motherboard = 0,
    Ram = 1,
    Gpu = 2,
    Fan = 3,
    LedStrip = 4,
    Case = 5,
    Keyboard = 6,
    Mouse = 7,
    Headset = 8,
    Other = 9,
    Mousemat = 10,
    HeadsetStand = 11,
    Speaker = 12,
    Gamepad = 13,
    Microphone = 14,
    Keypad = 15,
}
