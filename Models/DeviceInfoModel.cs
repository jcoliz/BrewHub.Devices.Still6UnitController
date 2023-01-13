using System.Runtime.InteropServices;

namespace BrewHub.Controller.Models;

public class DeviceInformationModel
{
    // Manufacturer of the device THIS CODE is running on
    public string Manufacturer => "BrewHub";
    public string? DeviceModel => "Digital Distillery Controller DC-01";
    public string? SoftwareVersion { get; set; } = "0.0.1";

    public string OperatingSystemName => RuntimeInformation.OSDescription;

    public string ProcessorArchitecture => RuntimeInformation.OSArchitecture.ToString();

    public double AvailableStorageKB 
    {
        get
        {
            double result = 0;
            var drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.IsReady)
                {
                    result += (double)drive.AvailableFreeSpace / 1024.0;
                }
            }
            return result;
        }
    }

    public double AvailableMemoryKB
    {
        get
        {
            var tm = GC.GetGCMemoryInfo();
            var mem = tm.TotalAvailableMemoryBytes;

            return (double)mem / 1024.0;
        }
    }
}