using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using PInvokeSerialPort;
using System.Threading;

namespace CS_FPGA_CLIENT
{

    /* For binary miners we can use PINVOKE_SERIAL_PORT or the .NET built in framework (if this is not defined) */
#if USE_PINVOKE_SERIAL_PORT
    public class DeviceUARTBinary : Device
    {
        private string COM;
        private int baudrate;
        private PInvokeSerialPort.SerialPort ComPort;

        private int recv_buff_length = 0;
        private byte[] recv_buff = null;

        /**
         * aDeviceIndex   - indeintifier number of the device. 
         * aReadLines     - If true, then complete liens will be read and passed up, otherwise each byte is passed up. 
         * COM            - name of COM port to use
         * baudrate       - baudrate to use
         **/
        public DeviceUARTBinary(int aDeviceIndex, string COM, int baudrate = 115200)
                : base(aDeviceIndex)
        {
            if (COM.StartsWith("COM"))
            {
                string newCOM = "\\\\.\\" + COM;
                Program.Logger("Converting " + COM + " -> " + newCOM);
                COM = newCOM;
            }
            this.COM = COM;
            this.baudrate = baudrate;
            startComPort();
        }

        private void startComPort()
        {
            ComPort = new PInvokeSerialPort.SerialPort(COM, baudrate);
            ComPort.AutoReopen = true;
            ComPort.DataReceived += ReciveByte;
            ComPort.Open();
        }

        public void ForceReconnect()
        {
            ComPort.Close();
            ComPort.Dispose();
            ComPort = null;
            Thread.Sleep(1000);
            startComPort();
        }

        protected override void Dispose(bool disposing)
        {
            if (ComPort != null)
            {
                ComPort.Close();
                ComPort = null;
            }
        }

        public void WriteLine(string line)
        {
            ComPort.WriteLine(line);
        }

        public void Write(byte[] buffer)
        {
            ComPort.Write(buffer);
        }

        /**
         *  Callback that is issued for each received byte. 
         *  Return TRUE to discard buffer, return false to continue to fill in more data into buffer. 
         *  This function is called for each received byte, with the same d buffer until TRUE is returned then it starts over. 
         **/
        protected virtual bool COM_byte(byte[] d, int dataLength) { return true; }

        private void ReciveByte(byte b)
        {
            Program.Logger(String.Format("{0:X2}", b));
            if (recv_buff == null)
            {
                recv_buff = new byte[2048];
                recv_buff_length = 0;
            }
            recv_buff[recv_buff_length++] = (byte)b;
            if (COM_byte(recv_buff, recv_buff_length))
            {
                recv_buff = null;
                recv_buff_length = 0;
            }
        }
        
    }

#endif

    
    public class DeviceUARTBinary : DeviceUARTText
    {
        public DeviceUARTBinary(int aDeviceIndex, string COM, int baudrate = 115200)
            : base(aDeviceIndex, tRMode.BYTE_MODE, COM, baudrate)
        {}

        public void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public void ForceReconnect()
        {
            Program.Logger("DeviceUARTBinary.ForceReconnect  -- NEEDS TO BE IMPLEMENTED when hosted on System.IO.Ports.SerialPort");
        }

    }


    /* Below is using build in System.IO.Ports.SerialPort, it sucks ... */
    public class DeviceUARTText : Device
    {
        private System.IO.Ports.SerialPort ComPort;

        public enum tRMode
        {
            LINE_MODE,
            BYTE_MODE,
            // add a packetized mode for the binary stuff? or is bytemode good enough?
        };

        private Thread ReadThread;
        private bool KillThread = false;
        private tRMode RMode;

        private int recv_buff_length = 0;
        private byte[] recv_buff = null;

        /**
         * aDeviceIndex   - indeintifier number of the device. 
         * aReadLines     - If true, then complete liens will be read and passed up, otherwise each byte is passed up. 
         * COM            - name of COM port to use
         * baudrate       - baudrate to use
         **/
    public DeviceUARTText(int aDeviceIndex, tRMode aRMode, string COM, int baudrate = 115200)
            : base(aDeviceIndex)
        {
            RMode = aRMode;
            ComPort = new System.IO.Ports.SerialPort(COM, baudrate);
            ReadThread = null;
            connect();
        }

        public bool connect()
        {
            if (!ComPort.IsOpen)
            {
                ComPort.Open();
                if (ReadThread != null)
                {
                    KillThread = true;
                    ReadThread.Join(100);
                    ReadThread = null;
                }
                KillThread = false;
                ReadThread = new Thread(Read);
                ReadThread.Start();
            }
            return ComPort.IsOpen;
        }

        public bool IsOpen { get { return ComPort.IsOpen; } }

        protected override void Dispose(bool disposing)
        {
            if (ComPort != null)
            {
                ComPort.Close();
                ComPort = null;
            }
            if (ReadThread != null)
            {
                KillThread = true;
                ReadThread.Abort();
                ReadThread = null;
            }
        }

        public void WriteLine(string line)
        {
            ComPort.WriteLine(line);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            ComPort.Write(buffer, offset, count);
        }

        /**
         *  Callback that is issued when in line mode, when a complete new line has been received. 
         **/
        protected virtual void COM_line(string line) { }
        /**
         *  Callback that is issued when in byte for each received byte. 
         *  Return TRUE to discard buffer, return false to continue to fill in more data into buffer. 
         *  This function is called for each received byte, with the same d buffer until TRUE is returned then it starts over. 
         **/
        protected virtual bool COM_byte(byte[] d, int dataLength) { return true; }

        public virtual void Read()
        {
            while (!KillThread)
            {
                try
                {
                    switch (RMode)
                    {
                        case tRMode.LINE_MODE:
                            COM_line(ComPort.ReadLine().TrimEnd('\r', '\n'));
                            break;

                        case tRMode.BYTE_MODE:
                            int b = ComPort.ReadByte();
                            if (b == -1)
                            {
                                KillThread = true;
                            }
                            else
                            {
                                if (recv_buff == null)
                                {
                                    recv_buff = new byte[2048];
                                    recv_buff_length = 0;
                                }
                                recv_buff[recv_buff_length++] = (byte)b;
                                if(COM_byte(recv_buff, recv_buff_length))
                                {
                                    recv_buff = null;
                                    recv_buff_length = 0;
                                }
                            }                              
                            break;
                    }
                }
                catch (Exception e)
                {
                    Program.Logger(e.ToString());
                    KillThread = true;
                }
            }
        }
    }

}
