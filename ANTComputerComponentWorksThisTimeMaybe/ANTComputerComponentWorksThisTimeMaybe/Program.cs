/*
This software is subject to the license described in the License.txt file
included with this software distribution. You may not use this file except
in compliance with this license.

Copyright (c) Dynastream Innovations Inc. 2016
All rights reserved.
*/

//////////////////////////////////////////////////////////////////////////
// To use the managed library, you must:
// 1. Import ANT_NET.dll as a reference
// 2. Reference the ANT_Managed_Library namespace
// 3. Include the following files in the working directory of your application:
//  - DSI_CP310xManufacturing_3_1.dll
//  - DSI_SiUSBXp_3_1.dll
//  - ANT_WrappedLib.dll
//  - ANT_NET.dll
//////////////////////////////////////////////////////////////////////////

/*
 * Custom code for processing MIDI files
 * 
 */
#define ENABLE_EXTENDED_MESSAGES // Un - coment to enable extended messages

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ANT_Managed_Library;

namespace Program
{
    class Program
    {

        static readonly byte CHANNEL_TYPE_INVALID = 2;

        static readonly byte USER_ANT_CHANNEL = 0;         // ANT Channel to use
        static readonly ushort USER_DEVICENUM = 0;        // Device number    
        static readonly byte USER_DEVICETYPE = 4;          // Device type
        static readonly byte USER_TRANSTYPE = 4;           // Transmission type

        static readonly byte USER_RADIOFREQ = 81;          // RF Frequency + 2400 MHz
        static readonly ushort USER_CHANNELPERIOD = 8192;  // Channel Period (8192/32768)s period = 4Hz

        static readonly byte[] USER_NETWORK_KEY = { 0, 0, 0, 0, 0, 0, 0, 0 };
        static readonly byte USER_NETWORK_NUM = 0;         // The network key is assigned to this network number

        static ANT_Device device0;
        static ANT_Channel channel0;
        static ANT_ReferenceLibrary.ChannelType channelType;
        static byte[] txBuffer = { 0, 0, 0, 0, 0, 0, 0, 0 };
        static bool bDone;
        static bool bDisplay;
        static bool bBroadcasting;
        static int iIndex = 0;

        /*
         * New variables introduced in order to send the midi file. 
         */
        static System.IO.BinaryReader midiFileReader;
        static bool taskInProgress;
        static byte lastInstructionType;
        static long numOfBytesLeftInMessage;
        static long waitTime;
        static byte octavesShifted = 0;


        ////////////////////////////////////////////////////////////////////////////////
        // Main
        //
        // Usage:
        //
        // c:\demo_net.exe [channel_type]
        //
        // ... where
        // channel_type:  Master = 0, Slave = 1
        //
        // ... example
        //
        // c:\demo_net.exe 0
        // 
        // will connect to an ANT USB stick open a Master channel
        //
        // If optional arguements are not supplied, user will 
        // be prompted to enter these after the program starts
        //
        ////////////////////////////////////////////////////////////////////////////////
        static void Main(string[] args)
        {
            byte ucChannelType = 0;
            if (args.Length > 0)
            {
                ucChannelType = byte.Parse(args[0]);
            }

            try
            {
                Init();
                selectTrack();
                Start(ucChannelType);
                midiFileReader.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Demo failed with exception: \n" + ex.Message);
            }
        }



        ////////////////////////////////////////////////////////////////////////////////
        // Init
        //
        // Initialize demo parameters.
        //
        ////////////////////////////////////////////////////////////////////////////////
        static void Init()
        {
            try
            {
                Console.WriteLine("Attempting to connect to an ANT USB device...");
                device0 = new ANT_Device();   // Create a device instance using the automatic constructor (automatic detection of USB device number and baud rate)
                device0.deviceResponse += new ANT_Device.dDeviceResponseHandler(DeviceResponse);    // Add device response function to receive protocol event messages

                channel0 = device0.getChannel(USER_ANT_CHANNEL);    // Get channel from ANT device
                channel0.channelResponse += new dChannelResponseHandler(ChannelResponse);  // Add channel response function to receive channel event messages
                Console.WriteLine("Initialization was successful!");
            }
            catch (Exception ex)
            {
                if (device0 == null)    // Unable to connect to ANT
                {
                    throw new Exception("Could not connect to any device.\n" +
                    "Details: \n   " + ex.Message);
                }
                else
                {
                    throw new Exception("Error connecting to ANT: " + ex.Message);
                }
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // Start
        //
        // Start the demo program.
        // 
        // ucChannelType_:  ANT Channel Type. 0 = Master, 1 = Slave
        //                  If not specified, 2 is passed in as invalid.
        ////////////////////////////////////////////////////////////////////////////////
        static void Start(byte ucChannelType_)
        {
            byte ucChannelType = ucChannelType_;
            bDone = false;
            bDisplay = true;
            bBroadcasting = false;

            PrintMenu();

            // If a channel type has not been set at the command line,
            // prompt the user to specify one now
            do
            {
                if (ucChannelType == CHANNEL_TYPE_INVALID)
                {
                    Console.WriteLine("Channel Type? (Master = 0, Slave = 1)");
                    try
                    {
                        ucChannelType = byte.Parse(Console.ReadLine());
                    }
                    catch (Exception)
                    {
                        ucChannelType = CHANNEL_TYPE_INVALID;
                    }
                }

                if (ucChannelType == 0)
                {
                    channelType = ANT_ReferenceLibrary.ChannelType.BASE_Master_Transmit_0x10;
                }
                else if (ucChannelType == 1)
                {
                    channelType = ANT_ReferenceLibrary.ChannelType.BASE_Slave_Receive_0x00;
                }
                else
                {
                    ucChannelType = CHANNEL_TYPE_INVALID;
                    Console.WriteLine("Error: Invalid channel type");
                }
            } while (ucChannelType == CHANNEL_TYPE_INVALID);

            try
            {
                ConfigureANT();

                while (!bDone)
                {
                    string command = Console.ReadLine();
                    switch (command)
                    {
                        case "M":
                        case "m":
                            {
                                PrintMenu();
                                break;
                            }
                        case "Q":
                        case "q":
                            {
                                // Quit
                                Console.WriteLine("Closing Channel");
                                bBroadcasting = false;
                                channel0.closeChannel();
                                break;
                            }
                        case "A":
                        case "a":
                            {
                                // Send Acknowledged Data
                                byte[] myTxBuffer = { 1, 2, 3, 4, 5, 6, 7, 8 };
                                channel0.sendAcknowledgedData(myTxBuffer);
                                break;
                            }
                        case "B":
                        case "b":
                            {
                                // Send Burst Data (10 packets)
                                byte[] myTxBuffer = new byte[8 * 10];
                                for (byte i = 0; i < 8 * 10; i++)
                                    myTxBuffer[i] = i;
                                channel0.sendBurstTransfer(myTxBuffer);
                                break;
                            }

                        case "R":
                        case "r":
                            {
                                // Reset the system and start over the test
                                ConfigureANT();
                                break;
                            }

                        case "C":
                        case "c":
                            {
                                // Request capabilities
                                ANT_DeviceCapabilities devCapab = device0.getDeviceCapabilities(500);
                                Console.Write(devCapab.printCapabilities() + Environment.NewLine);
                                break;
                            }
                        case "V":
                        case "v":
                            {
                                // Request version
                                // As this is not available in all ANT parts, we should not wait for a response, so
                                // we do not specify a timeout
                                // The response - if available - will be processed in DeviceResponse
                                device0.requestMessage(ANT_ReferenceLibrary.RequestMessageID.VERSION_0x3E);
                                break;
                            }
                        case "S":
                        case "s":
                            {
                                // Request channel status
                                ANT_ChannelStatus chStatus = channel0.requestStatus(500);

                                string[] allStatus = { "STATUS_UNASSIGNED_CHANNEL",
                                                    "STATUS_ASSIGNED_CHANNEL",
                                                    "STATUS_SEARCHING_CHANNEL",
                                                    "STATUS_TRACKING_CHANNEL"};
                                Console.WriteLine("STATUS: " + allStatus[(int)chStatus.BasicStatus]);
                                break;
                            }
                        case "I":
                        case "i":
                            {
                                // Request channel ID
                                ANT_Response respChID = device0.requestMessageAndResponse(ANT_ReferenceLibrary.RequestMessageID.CHANNEL_ID_0x51, 500);
                                ushort usDeviceNumber = (ushort)((respChID.messageContents[2] << 8) + respChID.messageContents[1]);
                                byte ucDeviceType = respChID.messageContents[3];
                                byte ucTransmissionType = respChID.messageContents[4];
                                Console.WriteLine("CHANNEL ID: (" + usDeviceNumber.ToString() + "," + ucDeviceType.ToString() + "," + ucTransmissionType.ToString() + ")");
                                break;
                            }
                        case "D":
                        case "d":
                            {
                                bDisplay = !bDisplay;
                                break;
                            }
                        case "U":
                        case "u":
                            {
                                // Print out information about the device we are connected to
                                Console.WriteLine("USB Device Description");

                                // Retrieve info
                                Console.WriteLine(String.Format("   VID: 0x{0:x}", device0.getDeviceUSBVID()));
                                Console.WriteLine(String.Format("   PID: 0x{0:x}", device0.getDeviceUSBPID()));
                                Console.WriteLine(String.Format("   Product Description: {0}", device0.getDeviceUSBInfo().printProductDescription()));
                                Console.WriteLine(String.Format("   Serial String: {0}", device0.getDeviceUSBInfo().printSerialString()));
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                    System.Threading.Thread.Sleep(0);
                }
                // Clean up ANT
                Console.WriteLine("Disconnecting module...");
                ANT_Device.shutdownDeviceInstance(ref device0);  // Close down the device completely and completely shut down all communication
                Console.WriteLine("Demo has completed successfully!");
                return;
            }
            catch (Exception ex)
            {
                throw new Exception("Demo failed: " + ex.Message + Environment.NewLine);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // ConfigureANT
        //
        // Resets the system, configures the ANT channel and starts the demo
        ////////////////////////////////////////////////////////////////////////////////
        private static void ConfigureANT()
        {
            Console.WriteLine("Resetting module...");
            device0.ResetSystem();     // Soft reset
            System.Threading.Thread.Sleep(500);    // Delay 500ms after a reset

            // If you call the setup functions specifying a wait time, you can check the return value for success or failure of the command
            // This function is blocking - the thread will be blocked while waiting for a response.
            // 500ms is usually a safe value to ensure you wait long enough for any response
            // If you do not specify a wait time, the command is simply sent, and you have to monitor the protocol events for the response,
            Console.WriteLine("Setting network key...");
            if (device0.setNetworkKey(USER_NETWORK_NUM, USER_NETWORK_KEY, 500))
                Console.WriteLine("Network key set");
            else
                throw new Exception("Error configuring network key");

            Console.WriteLine("Assigning channel...");
            if (channel0.assignChannel(channelType, USER_NETWORK_NUM, 500))
                Console.WriteLine("Channel assigned");
            else
                throw new Exception("Error assigning channel");

            Console.WriteLine("Setting Channel ID...");
            if (channel0.setChannelID(USER_DEVICENUM, false, USER_DEVICETYPE, USER_TRANSTYPE, 500))  // Not using pairing bit
                Console.WriteLine("Channel ID set");
            else
                throw new Exception("Error configuring Channel ID");

            Console.WriteLine("Setting Radio Frequency...");
            if (channel0.setChannelFreq(USER_RADIOFREQ, 500))
                Console.WriteLine("Radio Frequency set");
            else
                throw new Exception("Error configuring Radio Frequency");

            Console.WriteLine("Setting Channel Period...");
            if (channel0.setChannelPeriod(USER_CHANNELPERIOD, 500))
                Console.WriteLine("Channel Period set");
            else
                throw new Exception("Error configuring Channel Period");

            Console.WriteLine("Opening channel...");
            bBroadcasting = true;
            if (channel0.openChannel(500))
            {
                Console.WriteLine("Channel opened");
            }
            else
            {
                bBroadcasting = false;
                throw new Exception("Error opening channel");
            }

#if (ENABLE_EXTENDED_MESSAGES)
            // Extended messages are not supported in all ANT devices, so
            // we will not wait for the response here, and instead will monitor 
            // the protocol events
            Console.WriteLine("Enabling extended messages...");
            device0.enableRxExtendedMessages(true);
#endif
        }

        ////////////////////////////////////////////////////////////////////////////////
        // ChannelResponse
        //
        // Called whenever a channel event is recieved. 
        // 
        // response: ANT message
        ////////////////////////////////////////////////////////////////////////////////
        static void ChannelResponse(ANT_Response response)
        {
            try
            {
                switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
                {
                    case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                        {
                            switch (response.getChannelEventCode())
                            {
                                // This event indicates that a message has just been
                                // sent over the air. We take advantage of this event to set
                                // up the data for the next message period.   
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TX_0x03:
                                    {
                                        prepareNextTxBufferMessage();

                                        // Broadcast data will be sent over the air on
                                        // the next message period
                                        if (bBroadcasting)
                                        {
                                            channel0.sendBroadcastData(txBuffer);

                                            if (bDisplay)
                                            {
                                                // Echo what the data will be over the air on the next message period
                                                Console.WriteLine("Tx: (" + response.antChannel.ToString() + ")" + BitConverter.ToString(txBuffer));
                                            }
                                        }
                                        else
                                        {
                                            string[] ac = { "|", "/", "_", "\\" };
                                            Console.Write("Tx: " + ac[iIndex++] + "\r");
                                            iIndex &= 3;
                                        }
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_SEARCH_TIMEOUT_0x01:
                                    {
                                        Console.WriteLine("Search Timeout");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_FAIL_0x02:
                                    {
                                        Console.WriteLine("Rx Fail");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_RX_FAILED_0x04:
                                    {
                                        Console.WriteLine("Burst receive has failed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_COMPLETED_0x05:
                                    {
                                        Console.WriteLine("Transfer Completed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_FAILED_0x06:
                                    {
                                        Console.WriteLine("Transfer Failed");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_CHANNEL_CLOSED_0x07:
                                    {
                                        // This event should be used to determine that the channel is closed.
                                        Console.WriteLine("Channel Closed");
                                        Console.WriteLine("Unassigning Channel...");
                                        if (channel0.unassignChannel(500))
                                        {
                                            Console.WriteLine("Unassigned Channel");
                                            Console.WriteLine("Press enter to exit");
                                            bDone = true;
                                        }
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_RX_FAIL_GO_TO_SEARCH_0x08:
                                    {
                                        Console.WriteLine("Go to Search");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_CHANNEL_COLLISION_0x09:
                                    {
                                        Console.WriteLine("Channel Collision");
                                        break;
                                    }
                                case ANT_ReferenceLibrary.ANTEventID.EVENT_TRANSFER_TX_START_0x0A:
                                    {
                                        Console.WriteLine("Burst Started");
                                        break;
                                    }
                                default:
                                    {
                                        Console.WriteLine("Unhandled Channel Event " + response.getChannelEventCode());
                                        break;
                                    }
                            }
                            break;
                        }
                    case ANT_ReferenceLibrary.ANTMessageID.BROADCAST_DATA_0x4E:
                    case ANT_ReferenceLibrary.ANTMessageID.ACKNOWLEDGED_DATA_0x4F:
                    case ANT_ReferenceLibrary.ANTMessageID.BURST_DATA_0x50:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BROADCAST_DATA_0x5D:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_ACKNOWLEDGED_DATA_0x5E:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BURST_DATA_0x5F:
                        {
                            if (bDisplay)
                            {
                                if (response.isExtended()) // Check if we are dealing with an extended message
                                {
                                    ANT_ChannelID chID = response.getDeviceIDfromExt();    // Channel ID of the device we just received a message from
                                    Console.Write("Chan ID(" + chID.deviceNumber.ToString() + "," + chID.deviceTypeID.ToString() + "," + chID.transmissionTypeID.ToString() + ") - ");
                                }
                                if (response.responseID == (byte)ANT_ReferenceLibrary.ANTMessageID.BROADCAST_DATA_0x4E
                                    || response.responseID == (byte)ANT_ReferenceLibrary.ANTMessageID.EXT_BROADCAST_DATA_0x5D)
                                    Console.Write("Rx:(" + response.antChannel.ToString() + "): ");
                                else if (response.responseID == (byte)ANT_ReferenceLibrary.ANTMessageID.ACKNOWLEDGED_DATA_0x4F
                                    || response.responseID == (byte)ANT_ReferenceLibrary.ANTMessageID.EXT_ACKNOWLEDGED_DATA_0x5E)
                                    Console.Write("Acked Rx:(" + response.antChannel.ToString() + "): ");
                                else
                                    Console.Write("Burst(" + response.getBurstSequenceNumber().ToString("X2") + ") Rx:(" + response.antChannel.ToString() + "): ");

                                Console.Write(BitConverter.ToString(response.getDataPayload()) + Environment.NewLine);  // Display data payload
                            }
                            else
                            {
                                string[] ac = { "|", "/", "_", "\\" };
                                Console.Write("Rx: " + ac[iIndex++] + "\r");
                                iIndex &= 3;
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Unknown Message " + response.responseID);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Channel response processing failed with exception: " + ex.Message);
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // DeviceResponse
        //
        // Called whenever a message is received from ANT unless that message is a 
        // channel event message. 
        // 
        // response: ANT message
        ////////////////////////////////////////////////////////////////////////////////
        static void DeviceResponse(ANT_Response response)
        {
            switch ((ANT_ReferenceLibrary.ANTMessageID)response.responseID)
            {
                case ANT_ReferenceLibrary.ANTMessageID.STARTUP_MESG_0x6F:
                    {
                        Console.Write("RESET Complete, reason: ");

                        byte ucReason = response.messageContents[0];

                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_POR_0x00)
                            Console.WriteLine("RESET_POR");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_RST_0x01)
                            Console.WriteLine("RESET_RST");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_WDT_0x02)
                            Console.WriteLine("RESET_WDT");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_CMD_0x20)
                            Console.WriteLine("RESET_CMD");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SYNC_0x40)
                            Console.WriteLine("RESET_SYNC");
                        if (ucReason == (byte)ANT_ReferenceLibrary.StartupMessage.RESET_SUSPEND_0x80)
                            Console.WriteLine("RESET_SUSPEND");
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.VERSION_0x3E:
                    {
                        Console.WriteLine("VERSION: " + new ASCIIEncoding().GetString(response.messageContents));
                        break;
                    }
                case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                    {
                        switch (response.getMessageID())
                        {
                            case ANT_ReferenceLibrary.ANTMessageID.CLOSE_CHANNEL_0x4C:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.CHANNEL_IN_WRONG_STATE_0x15)
                                    {
                                        Console.WriteLine("Channel is already closed");
                                        Console.WriteLine("Unassigning Channel...");
                                        if (channel0.unassignChannel(500))
                                        {
                                            Console.WriteLine("Unassigned Channel");
                                            Console.WriteLine("Press enter to exit");
                                            bDone = true;
                                        }
                                    }
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.NETWORK_KEY_0x46:
                            case ANT_ReferenceLibrary.ANTMessageID.ASSIGN_CHANNEL_0x42:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_ID_0x51:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_FREQ_0x45:
                            case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_MESG_PERIOD_0x43:
                            case ANT_ReferenceLibrary.ANTMessageID.OPEN_CHANNEL_0x4B:
                            case ANT_ReferenceLibrary.ANTMessageID.UNASSIGN_CHANNEL_0x41:
                                {
                                    if (response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                                    {
                                        Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                                    }
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.RX_EXT_MESGS_ENABLE_0x66:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                                    {
                                        Console.WriteLine("Extended messages not supported in this ANT product");
                                        break;
                                    }
                                    else if (response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                                    {
                                        Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                                        break;
                                    }
                                    Console.WriteLine("Extended messages enabled");
                                    break;
                                }
                            case ANT_ReferenceLibrary.ANTMessageID.REQUEST_0x4D:
                                {
                                    if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                                    {
                                        Console.WriteLine("Requested message not supported in this ANT product");
                                        break;
                                    }
                                    break;
                                }
                            default:
                                {
                                    Console.WriteLine("Unhandled response " + response.getChannelEventCode() + " to message " + response.getMessageID()); break;
                                }
                        }
                        break;
                    }
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // PrintMenu
        //
        // Display demo menu
        // 
        ////////////////////////////////////////////////////////////////////////////////
        static void PrintMenu()
        {
            // Print out options  
            Console.WriteLine("M - Print this menu");
            Console.WriteLine("A - Send Acknowledged message");
            Console.WriteLine("B - Send Burst message");
            Console.WriteLine("R - Reset");
            Console.WriteLine("C - Request Capabilites");
            Console.WriteLine("V - Request Version");
            Console.WriteLine("I - Request Channel ID");
            Console.WriteLine("S - Request Status");
            Console.WriteLine("U - Request USB Descriptor");
            Console.WriteLine("D - Toggle Display");
            Console.WriteLine("Q - Quit");
        }

        /*
         * Past here begins functions not included in the ANT C# library.
         * This code is made to send a midi file piecewise through ANT.
         * 
         */
        

        static String getTrackDirectory()
        {
            String DebugDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            String binDir = System.IO.Directory.GetParent(DebugDir).FullName;
            String innerAntCCDir = System.IO.Directory.GetParent(binDir).FullName;
            String outerAntCCDir = System.IO.Directory.GetParent(innerAntCCDir).FullName;
            String AntMidiFileSenderDir = System.IO.Directory.GetParent(outerAntCCDir).FullName;
            String trackDir = System.IO.Path.Combine(AntMidiFileSenderDir, "Tracks");
            Console.WriteLine(trackDir);
            return trackDir;
        }

        static void initializeTrackList()
        {

        }

        static void selectTrack()
        {
            String pathToFile = getTrackDirectory();
            while(!pathToFile.Contains("."))
            {
                List<String> fileList = getFileList(pathToFile);
                printNavigationMenu(fileList);
                int selection = getUserSelectedIndex(fileList);
                if(selection == 0)
                {
                    pathToFile = System.IO.Directory.GetParent(pathToFile).FullName;
                }
                else
                {
                    pathToFile = System.IO.Path.Combine(pathToFile, fileList.ElementAt(selection));
                }
            }
            midiFileReader = new System.IO.BinaryReader(new System.IO.FileStream(pathToFile, System.IO.FileMode.Open));

            void printNavigationMenu(List<String> fileList)
            {
                for(int i = 0; i < fileList.Count; i++)
                {
                    int displayedValue = i + 1;
                    Console.WriteLine(displayedValue + ". " + fileList.ElementAt(i));
                }
            }

            List<String> getFileList(String currentDirectory)
            {
                List<String> dirList = new List<String>(System.IO.Directory.EnumerateDirectories(currentDirectory));
                List<String> fileList = new List<String>(System.IO.Directory.EnumerateFiles(currentDirectory));
                dirList.Sort();
                fileList.Sort();

                List<String> everythingList = new List<String>();
                everythingList.Add("Go back one file");
                for(int i = 0; i < dirList.Count; i++)
                {
                    everythingList.Add(dirList.ElementAt(i));
                }
                for(int j = 0; j < fileList.Count; j++)
                {
                    if(fileList.ElementAt(j).Contains(".mid"))
                    {
                        everythingList.Add(fileList.ElementAt(j));
                    }
                }
                return everythingList;
            }

            int getUserSelectedIndex(List<String> fileList)
            {
                bool validInputGiven = false;
                int selection = 1;
                int index = 0;
                while(!validInputGiven)
                {
                    Console.WriteLine("Please make a selection.");
                    String userInput = Console.ReadLine();
                    try
                    {
                        selection = Int32.Parse(userInput);
                        if(selection <= 0)
                        {
                            Console.WriteLine("Numbers smaller than one are invalid.");
                        }
                        else if(selection > fileList.Count)
                        {
                            Console.WriteLine("Your selected number was too large.");
                        }
                        else
                        {
                            index = selection - 1;
                            validInputGiven = true;
                        }
                    }
                    catch(FormatException fe)
                    {
                        Console.WriteLine("Please enter an integer.");
                    }
                    catch(ArgumentNullException ane)
                    {
                        Console.WriteLine("Please enter an integer");
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("An unexpected error occurred while processing your input. Please try again.");
                    }
                }
                return index;
            }
        }

        static void prepareNextTxBufferMessage()
        {
            try
            {
                if(taskInProgress)
                {
                    continuePreviousTask();
                }
                else
                {
                    txBuffer[0] = midiFileReader.ReadByte();
                    byte temp = (byte)(txBuffer[0] & 0xF0);
                    Console.WriteLine(temp);
                    Console.WriteLine("New Instruction Started!");
                    if (txBuffer[0] == 0x4D)
                    {
                        readCommandStartingWith4D();
                    }
                    else if (txBuffer[0] == 0xFF)
                    {
                        readMetaEvent();
                    }
                    else if (isAnEventCommandCode(temp))
                    {
                        readEventCommand(temp);
                    }
                    else
                    {
                        //This is a delta time command
                        Console.WriteLine("Interpreted a delta time instruction outside the finishedInstruction() method, this should not be happenning! Identifier: " + txBuffer[0]);
                        interpretDeltaTime();
                        deltaTimeWait();
                    }
                }
            }
            catch(System.IO.EndOfStreamException eose)
            {
                for(int i = 0; i < txBuffer.Length; i++)
                {
                    txBuffer[i] = 0x00;
                }
            }
        }

        static void readCommandStartingWith4D() //THIS METHOD IS ONLY TO BE EXECUTED WHEN 4D IS ENCOUNTERED, DO NOT USE THIS METHOD TO CONTINUE 4D INSTRUCTIONS
        {
            //This message is either a header chunk or a track chunk
            txBuffer[1] = midiFileReader.ReadByte();
            txBuffer[2] = midiFileReader.ReadByte();
            txBuffer[3] = midiFileReader.ReadByte();
            if(txBuffer[1] == 0x54 && txBuffer[2] == 0x68 && txBuffer[3] == 0x64)
            {
                //This is a header chunk, the following bytes are 00 00 00 06
                for(int i = 4; i <= 7; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                taskInProgress = true; //There is still data to send!
                lastInstructionType = 0x4D;
            }
            else if(txBuffer[1] == 0x54 && txBuffer[2] == 0x72 && txBuffer[3] == 0x6B)
            {
                //This is a track chunk, the next four bytes are all that is needed
                for (int i = 4; i <= 7; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                finishedInstruction();
            }
            else
            {
                Console.WriteLine("A delta time starting of 4D was found. This is currently not supported.");
                // OOPS! turns out this was not a header at all. Remedy the situation by turning 4D into a delta time.
                waitTime += (txBuffer[0] & 0x7f);
                taskInProgress = true;
                lastInstructionType = 0x00;
                // What do we do about the rest of the bits?
                // No solution for the time being...
            }
        }

        static void finishHeaderChunk()
        {
            //Only six bytes of the header chunk are left and can be read directly from the file.
            for(int i = 0; i < 6; i++)
            {
                txBuffer[i] = midiFileReader.ReadByte();
            }
            txBuffer[6] = 0x00;
            txBuffer[7] = 0x00;
            finishedInstruction();
        }

        static bool isAnEventCommandCode(byte command)
        {
            //Valid command codes are 8X, 9X, AX, BX, CX, DX, and EX.
            if(command == 0x80 || command == 0x90 || command == 0xA0 || command == 0xB0 || command == 0xC0 || command == 0xD0 || command == 0xE0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static void readMetaEvent()
        {
            byte metaEventIdentifier = midiFileReader.ReadByte();
            txBuffer[1] = metaEventIdentifier;
            if(metaEventIdentifier == 0x58)
            {
                //Time signature event, length is five bytes.
                for(int i = 2; i <= 6; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                txBuffer[7] = 0x00;
                finishedInstruction();
            }
            else if(metaEventIdentifier == 0x51)
            {
                //Set tempo event, length is four bytes.
                for(int i = 2; i <= 5; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                txBuffer[6] = 0x00;
                txBuffer[7] = 0x00;
                finishedInstruction();
            }
            else if(metaEventIdentifier == 0x03)
            {
                //This is the track name, it's length is variable.
                txBuffer[2] = midiFileReader.ReadByte();
                numOfBytesLeftInMessage = (int)txBuffer[2];
                int i = 3;
                while(numOfBytesLeftInMessage != 0 && i <= 7)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                    i++;
                    numOfBytesLeftInMessage--;
                }
                if(numOfBytesLeftInMessage != 0)
                {
                    //We ran out of space in our message!
                    taskInProgress = true;
                    lastInstructionType = 0xFF;
                }
                else
                {
                    //We had enough space in one message.
                    finishedInstruction();
                }
            }
            else if(metaEventIdentifier == 0x02)
            {
                //This is copyright information, it's length is variable.
                txBuffer[2] = midiFileReader.ReadByte();
                numOfBytesLeftInMessage = (int)txBuffer[2];
                Console.WriteLine(numOfBytesLeftInMessage);
                int i = 3;
                while (numOfBytesLeftInMessage != 0 && i <= 7)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                    i++;
                    numOfBytesLeftInMessage--;
                }
                if (numOfBytesLeftInMessage != 0)
                {
                    //We ran out of space in our message!
                    taskInProgress = true;
                    lastInstructionType = 0xFF;
                }
                else
                {
                    //We had enough space in one message.
                    finishedInstruction();
                }
            }
            else if(metaEventIdentifier == 0x2F)
            {
                //End of track. Length one byte
                for(int i = 2; i <=7; i++)
                {
                    txBuffer[i] = 0x00;
                }
                finishedInstruction();
            }
            else if(metaEventIdentifier == 0x59)
            {
                //Key signature, I am going to discard this for the time being.
                for (int i = 2; i <= 4; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                for (int j = 5; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else
            {
                Console.WriteLine("Unexpected meta event identifier: " + metaEventIdentifier);
            }
        }

        static void readEventCommand(byte command)
        {
            if (command == 0x80)
            {
                //Note off command, the length is two bytes after the command identifier.
                txBuffer[0] = command;
                txBuffer[1] = (byte)(midiFileReader.ReadByte() + (octavesShifted * 32));
                txBuffer[2] = midiFileReader.ReadByte();
                for (int j = 3; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else if (command == 0x90)
            {
                //Note on command, the length is two bytes after the command identifier.
                txBuffer[0] = command;

                byte tentativeNote = (byte)(midiFileReader.ReadByte() + (octavesShifted * 32));
                while (tentativeNote < 48)
                {
                    octavesShifted++;
                    tentativeNote = (byte)(tentativeNote + 32);
                }
                while (tentativeNote > 95)
                {
                    octavesShifted--;
                    tentativeNote = (byte)(tentativeNote - 32);
                }

                txBuffer[1] = (byte)(tentativeNote);
                txBuffer[2] = midiFileReader.ReadByte();
                for (int j = 3; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else if (command == 0xA0)
            {
                //Key after-touch, two bytes after command byte.
                txBuffer[0] = command;
                for (int i = 1; i <= 2; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                for (int j = 3; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else if (command == 0xB0)
            {
                //Control change, two bytes after command byte.
                txBuffer[0] = command;
                for (int i = 1; i <= 2; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                for (int j = 3; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else if (command == 0xC0)
            {
                //Program change command, the next byte is the new program number.
                txBuffer[0] = command;
                txBuffer[1] = midiFileReader.ReadByte();
                for(int i = 2; i <= 7; i++)
                {
                    txBuffer[i] = 0x00;
                }
                finishedInstruction();
            }
            else if (command == 0xD0)
            {
                //Channel after-touch, one byte in length
                txBuffer[0] = command;
                txBuffer[1] = midiFileReader.ReadByte();
                for (int i = 2; i <= 7; i++)
                {
                    txBuffer[i] = 0x00;
                }
                finishedInstruction();
            }
            else if(command == 0xE0)
            {
                //Pitch wheel change, two bytes in length
                txBuffer[0] = command;
                for (int i = 1; i <= 2; i++)
                {
                    txBuffer[i] = midiFileReader.ReadByte();
                }
                for (int j = 3; j <= 7; j++)
                {
                    txBuffer[j] = 0x00;
                }
                finishedInstruction();
            }
            else
            {
                Console.WriteLine("An invalid command got through the check to see if the byte is a valid command.");
            }
        }

        static void continuePreviousTask()
        {
            //No need to check for command codes, they are only four or six bytes.
            if(lastInstructionType == 0x00) //0x00 means a wait instruction is in progress.
            {
                //Console.WriteLine("Waiting for " + waitTime + " ticks...");
                deltaTimeWait();
            }
            else if(lastInstructionType == 0x4D)
            {
                finishHeaderChunk();
            }
            else if(lastInstructionType == 0xFF) //0xFF means a meta event needed more bytes in order to send all its data (presumably text)
            {
                finishSendingDataFromMetaEvent();
            }
            else if(lastInstructionType == 0x4E) //This is the state for cases where interpretDeltaTime accidentally read the 4D of the header chunk and though it was a delta time
            {
                //Assume the byte was already read and was 4D.
                txBuffer[0] = 0x4D;
                readCommandStartingWith4D();
            }
            else
            {
                Console.WriteLine("Attempted to continue an invalid instruction.");
            }
        }

        static void finishSendingDataFromMetaEvent()
        {
            int i = 0;
            while(numOfBytesLeftInMessage != 0 && i <= 7)
            {
                txBuffer[i] = midiFileReader.ReadByte();
                numOfBytesLeftInMessage--;
                i++;
            }
            for(; i <= 7; i++)
            {
                txBuffer[i] = 0x00;
            }
            if(numOfBytesLeftInMessage == 0)
            {
                finishedInstruction();
            }
            else
            {
                //We will continue on the next cycle.
            }
        }

        static void interpretDeltaTime()
        {
            byte currentByte = midiFileReader.ReadByte();
            // ONE SPECIAL CASE TO CONSIDER: Header chunks and track chunks DO NOT have a delta time before them.
            // We check if the current byte is 4D. If it is, we might be accidentally reading part of a header!
            if(currentByte == 0x4D)
            {
                taskInProgress = true;
                lastInstructionType = 0x4E; //0x4E is 0x4D but for the state where intepretDeltaTime has made a mistake!
            }
            else
            {
                int i = 0;
                do
                {
                    waitTime += ((currentByte & 0x7f) * (long)(Math.Pow(256, i)));
                    if((currentByte & 0x80) != 0)
                    {
                        currentByte = midiFileReader.ReadByte();
                    }
                    i++;
                    
                } while ((currentByte & 0x80) != 0);
                if (waitTime != 0)
                {
                    taskInProgress = true;
                    lastInstructionType = 0x00;
                }
                else
                {
                    taskInProgress = false;
                }
            }
        }

        static void deltaTimeWait()
        {
            if(waitTime <= 0)
            {
                waitTime = 0;
                taskInProgress = false;
            }
            else
            {
                Console.WriteLine("Wait...");
                taskInProgress = true;
                lastInstructionType = 0x00;
                waitTime = waitTime - 10; //INCREMENT BY TEN TICKS EVERY 1/4 SECOND
                //Do nothing...
                for (int i = 0; i < txBuffer.Length; i++)
                {
                    txBuffer[i] = 0x00;
                }
            }
        }

        static void finishedInstruction()
        {
            interpretDeltaTime();
        }
    }
}
