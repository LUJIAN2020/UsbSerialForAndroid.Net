namespace UsbSerialForAndroid.Net.Enums
{
    /// <summary>
    /// Future Technology Devices International, Ltd
    /// </summary>
    public enum Ftdi
    {
        FT232R = 0x6001,
        FT2232H = 0x6010,
        FT4232H = 0x6011,
        FT232H = 0x6014,
        /// <summary>
        /// same ID for FT230X, FT231X, FT234XD
        /// </summary>
        FT231X = 0x6015,
    }
}
