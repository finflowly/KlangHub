using KlangHub.Application;

namespace KlangHub.Classes
{
    public static class FilterDevices
    {
        /// <summary>
        /// Determine if the device is shown, depending on the filter.
        /// </summary>
        public static bool ShowFilterDevices(bool isGroup, FilterDevicesEnum value)
        {
            switch (value)
            {
                case FilterDevicesEnum.ShowAll:
                    return true;
                case FilterDevicesEnum.DevicesOnly:
                    return !isGroup;
                case FilterDevicesEnum.GroupsOnly:
                    return isGroup;
                default:
                    return true;
            }
        }
    }

}
