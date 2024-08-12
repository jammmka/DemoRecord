using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Net.Sockets;
using System.Net;
using static gUSBampSyncDemoCS.gUSBampWrapper;
using System.Data.SqlClient;

namespace gUSBampSyncDemoCS
{
    class Program
    {
        /// <summary>
        /// The number of seconds that the application should acquire data.
        /// </summary>
        const uint NumSecondsRunning =400; //additional 3sec just in case

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

            foreach (DeviceConfiguration deviceConfiguration in devices.Values)
                numChannels += (deviceConfiguration.SelectedChannels.Count + Convert.ToInt32(deviceConfiguration.TriggerLineEnabled));

            int numValuesAtOnce = numScans * numChannels;

            //const int listenPort = 2020;
            //UdpClient receivingUdpClient = new UdpClient(listenPort);
            //IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                //Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);
                //string returnData = Encoding.ASCII.GetString(receiveBytes);

                //Console.WriteLine("This is the message you received " +
                //                            returnData.ToString());
                //Console.WriteLine("This message was sent from " +
                //                            RemoteIpEndPoint.Address.ToString() +
                //                            " on their port number " +
                //                            RemoteIpEndPoint.Port.ToString());
                //USBampgetinfo
                //string fileName = returnData.ToString() + "_eeg.bin";
                //string timestampName = returnData.ToString() + "_timestamp.bin";
                string fileName = "D:/Experiment2/SeparateCheck/eeg01.bin";
                string timestampName = "D:/Experiment2/SeparateCheck/timestamp01.bin";
                //create file stream
                using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                {
                    using (FileStream timestampStream = new FileStream(timestampName, FileMode.Create))
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
                                string firstTime = startTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                                timestampWriter.WriteLine(firstTime); // Write timestamp
                                timestampWriter.WriteLine("hi");
                                //this is the data processing thread; data received from the devices will be written out to a file here
                                while (DateTime.Now < stopTime)
                                {
                                    float[] data = acquisitionUnit.ReadData(numValuesAtOnce);
                                    DateTime currentTime = DateTime.Now;
                                    int i;
                                    //write data to file
                                    for (i = 0; i < data.Length; i++)
                                        writer.Write(data[i]);
                                        //if ((i + 1) % 16 == 0) // After every 16 data points
                                        //{
                                        //    double offset = (i / 16.0) * (1000.0 / 512.0);
                                        //    DateTime currentTimeS = currentTime.AddMilliseconds(offset);
                                        //    writer.Write(currentTimeS.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                                        //}

                                }
                                DateTime lastTime = DateTime.Now;
                                string byeTime = lastTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                                timestampWriter.WriteLine("bye");
                                timestampWriter.WriteLine(byeTime); // Write timestamp
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
                //Console.ReadKey(true);
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
                deviceConfiguration.BandpassFilters = new Dictionary<byte, int>();
                foreach (byte channel in deviceConfiguration.SelectedChannels)
                {
                    //deviceConfiguration.BandpassFilters[channel] = 54;  // 0.1hz 8th order hpf for 512hz sfr //0.1,1,2,5
                    //deviceConfiguration.BandpassFilters[channel] = 60;  // 100hz 8th order lpf for 512hz sfr //30,60,100,200hz
                    deviceConfiguration.BandpassFilters[channel] = 68;

                    //065 | 0.01 | 200.0 | 512 | 8 | 1
                    //066 | 0.10 | 30.0  | 512 | 8 | 1
                    //067 | 0.10 | 60.0  | 512 | 8 | 1
                    //068 | 0.10 | 100.0 | 512 | 8 | 1
                    //069 | 0.10 | 200.0 | 512 | 8 | 1
                }
                deviceConfiguration.NotchFilters = new Dictionary<byte, int>();
                foreach (byte channel in deviceConfiguration.SelectedChannels)
                {
                    deviceConfiguration.NotchFilters[channel] = 4;  // 48-52hz 4th order hpf for 512hz sfr //5 = 58-62hz
                }
                deviceConfiguration.BipolarSettings = new gUSBampWrapper.Bipolar();
                deviceConfiguration.CommonGround = new gUSBampWrapper.Gnd
                {
                    Gnd1 = true,
                    Gnd2 = true,
                    Gnd3 = true,
                    Gnd4 = true,
                };
                deviceConfiguration.CommonReference = new gUSBampWrapper.Ref
                {
                    Ref1 = true,
                    Ref2 = true,
                    Ref3 = true,
                    Ref4 = true,
                };
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
