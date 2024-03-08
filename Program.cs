using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace gUSBampSyncDemoCS
{
    class Program
    {
        /// <summary>
        /// The number of seconds that the application should acquire data.
        /// </summary>
        const uint NumSecondsRunning = 120; ///add more sec due to filter

        /// <summary>
        /// Starts data acquisition and writes received data to a binary file.
        /// </summary>
        /// <remarks>
        /// You can read the file into matlab using the following code:
        /// <code>
        /// fid = fopen('receivedData.bin', 'rb');
        /// data = fread(fid, [<i>number of total channels</i>, inf], 'float32');
        /// fclose(fid);
        /// </code>
        /// </remarks>
        static void Main()
        {
            DataAcquisitionUnit acquisitionUnit = new DataAcquisitionUnit();

            //create device configurations
            Dictionary<string, DeviceConfiguration> devices = CreateDefaultDeviceConfigurations("UB-2012.05.40");

            //determine how many bytes should be read and processed at once by the processing thread (not the acquisition thread!)
            int numScans = 512;
            int numChannels = 0;

            TimeSpan samplingPeriod = TimeSpan.FromSeconds(1.0/numScans);

            foreach (DeviceConfiguration deviceConfiguration in devices.Values)
                numChannels += (deviceConfiguration.SelectedChannels.Count + Convert.ToInt32(deviceConfiguration.TriggerLineEnabled));

            int numValuesAtOnce = numScans * numChannels;

            try
            {
                //create file stream
                using (FileStream fileStream = new FileStream("new/receivedData_1.bin", FileMode.Create))
                {
                    using (FileStream timestampStream = new FileStream("new/timestamps_1.bin", FileMode.Create))
                    {
                        using (BinaryWriter writer = new BinaryWriter(fileStream))
                        {
                            using (StreamWriter timestampWriter = new StreamWriter(timestampStream))
                            {
                                //start acquisition thread
                                acquisitionUnit.StartAcquisition(devices);

                                //to stop the application after a specified time, get start time
                                DateTime startTime = DateTime.Now;
                                DateTime stopTime = startTime.AddSeconds(NumSecondsRunning);

                                //this is the data processing thread; data received from the devices will be written out to a file here
                                while (DateTime.Now < stopTime)
                                {
                                    DateTime currenttime = DateTime.Now;

                                    float[] data = acquisitionUnit.ReadData(numValuesAtOnce);                                    
                                    //write data to file
                                    for (int i = 0; i < data.Length; i++)
                                        writer.Write(data[i]);

                                    DateTime aftertime = DateTime.Now;
                                    TimeSpan recordingDuration = aftertime - currenttime;
                                    long timestamp = aftertime.Ticks / TimeSpan.TicksPerMillisecond;

                                    //int currenttime = startTime.Millisecond;
                                    //long currenttime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

                                    //string stringtime = currenttime.ToString();

                                    timestampWriter.WriteLine(aftertime); // Write timestamp
                                    
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\t{0}", ex.Message);
            }
            finally
            {
                //stop data acquisition
                acquisitionUnit.StopAcquisition();

                Console.WriteLine("Press any key exit...");
                Console.ReadKey(true);
            }
        }

        static Dictionary<string, DeviceConfiguration> CreateDefaultDeviceConfigurations(params string[] serialNumbers)
        {
            Dictionary<string, DeviceConfiguration> deviceConfigurations = new Dictionary<string, DeviceConfiguration>();

            for (int i = 0; i < serialNumbers.Length; i++)
            {
                DeviceConfiguration deviceConfiguration = new DeviceConfiguration();
                deviceConfiguration.SelectedChannels = new List<byte>(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
                deviceConfiguration.NumberOfScans = 16;
                deviceConfiguration.SampleRate = 512;
                deviceConfiguration.IsSlave = (i > 0);

                deviceConfiguration.TriggerLineEnabled = false;
                deviceConfiguration.SCEnabled = false;
                deviceConfiguration.Mode = gUSBampWrapper.OperationModes.Normal;
                deviceConfiguration.BandpassFilters = new Dictionary<byte, int>(); ///apply filter
                deviceConfiguration.NotchFilters = new Dictionary<byte, int>();
                deviceConfiguration.BipolarSettings = new gUSBampWrapper.Bipolar();
                deviceConfiguration.CommonGround = new gUSBampWrapper.Gnd();
                deviceConfiguration.CommonReference = new gUSBampWrapper.Ref();
                deviceConfiguration.Drl = new gUSBampWrapper.Channel();

                deviceConfiguration.Dac = new gUSBampWrapper.DAC();
                deviceConfiguration.Dac.WaveShape = gUSBampWrapper.WaveShapes.Sine;
                deviceConfiguration.Dac.Amplitude = 2000;
                deviceConfiguration.Dac.Frequency = 10;
                deviceConfiguration.Dac.Offset = 2047;

                deviceConfigurations.Add(serialNumbers[i], deviceConfiguration);
            }

            return deviceConfigurations;
        }
    }
}
