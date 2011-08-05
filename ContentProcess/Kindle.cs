namespace e2Kindle.ContentProcess
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Kindle
    {
        private static readonly Func<DriveInfo, bool> IsKindle = d => (d.DriveType == DriveType.Removable && d.IsReady && d.VolumeLabel == "Kindle");

        public static string DriveLetter
        {
            get
            {
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Kindle isn't connected.");
                }

                return DriveInfo.GetDrives().First(IsKindle).Name;
            }
        }

        public static bool IsConnected
        {
            get
            {
                return DriveInfo.GetDrives().Any(IsKindle);
            }
        }

        public static void SendDocument(string fileName)
        {
            if (!IsConnected) throw new InvalidOperationException("Kindle isn't connected.");
            if (!File.Exists(fileName)) throw new FileNotFoundException("Specified file not found.", fileName);

            File.Copy(fileName, Path.Combine(DriveLetter, "documents", Path.GetFileName(fileName)), true);
        }
    }
}