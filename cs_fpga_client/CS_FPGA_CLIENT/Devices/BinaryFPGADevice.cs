using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{

    public abstract class GenericComVerA_BinaryFPGADevice : DeviceUARTBinary
    {

        public override String GetVendor() { return "GAGGA_GALACTIC_SYNDICATE"; }
        public override String GetName() { return "GenericComA_BinaryFPGA"; }
        protected byte tid;
        int sent_works_since_last_ack;  /* setting to 0 each time we receive an ack, +1 each time we send work. if to high then close-open fpga connection */
        protected int nonce_length;

        protected abstract void FoundNonce(byte[] nonce, int dataLength);
        protected virtual void MinerInfo(string minerInfo) { }

        private byte[] bad_work_acks = new byte[16]; /* keep track of the last 16 work acks, 1 indicates bad work ack. */

        public GenericComVerA_BinaryFPGADevice(int aDeviceIndex, string COM, int nonce_length = 4)
            : base(aDeviceIndex, COM)
        {
            tid = 0;
            sent_works_since_last_ack = 0;
            this.nonce_length = nonce_length;
            RequestMinerInfo();            
        }

        protected void RequestMinerInfo()
        {
            byte[] midtid = new byte[2];
            midtid[0] = 1;
            midtid[1] = 0;
            Write(midtid);
        }

        protected void SendNewWork(byte[] b_data, int dataLength)
        {
            if (sent_works_since_last_ack > 10)
            {   // no ack for 10 sent work package, reopen FPGA connection
                Program.Logger("ERR: No ack for "+sent_works_since_last_ack+" sent work package, reopen FPGA connection.");
                sent_works_since_last_ack = 0;
                ForceReconnect();
            }
            byte[] midtid = new byte[2 + dataLength];
            midtid[0] = 3;
            midtid[1] = ++tid;
            Array.Copy(b_data, 0, midtid, 2, dataLength);
            try
            {
                Write(midtid);
                sent_works_since_last_ack++;
                //Console.WriteLine("BSW:" + ComPort.BytesToWrite);
            }
            catch (System.Exception e)
            {
                Program.Logger(e.ToString());
            }
        }

        private void add_work_ack_to_statistics(byte badAck)
        {
            int i;
            for (i = (bad_work_acks.Length - 1); i > 0 ; i--)
                bad_work_acks[i] = bad_work_acks[i-1];
            bad_work_acks[0] = badAck;
        }

        private float bad_ack_ratio()
        {
            byte C = 0;
            int i;
            for (i = 0; i < bad_work_acks.Length; i++)
                C += bad_work_acks[i];
            return (float)C / (float)bad_work_acks.Length;
        }

        protected override bool COM_byte(byte[] d, int dataLength)
        {
            /* Check that we have a start of a supported message */
            switch (d[0])
            {
                case 1: /* Miner info */
                    if (dataLength < 5)
                    {
                        return false;  /* wait for more data */
                    }
                    else if (dataLength == 5)
                    {
                        string miner_info = System.Text.Encoding.ASCII.GetString(d, 1, 4);
                        Program.Logger("Miner info " + miner_info);
                        MinerInfo(miner_info);
                        return true; /* full message received clear receive buffer */
                    }
                    break;

                case 3: /* Work ack */
                    if (dataLength < 2)
                    {
                        return false;  /* wait for more data */
                    }
                    else if (dataLength == 2)
                    {
                        byte ack_tid = d[1];
                        if (ack_tid == tid)
                        {
                            add_work_ack_to_statistics(0);
                            Program.Logger("New Task ACK! " + tid);
                            sent_works_since_last_ack = 0;
                        }
                        else
                        {
                            add_work_ack_to_statistics(1);
                            float nack_ratio = bad_ack_ratio();
                            Program.Logger("New Task NAK! FPGA:" + ack_tid + " HOST:" + tid + " NACK ratio:" + nack_ratio);
                            if(nack_ratio >= 0.5f)
                            {
                                /* Try and restart the UART device and see if it helps */
                                for (int i = 0; i < bad_work_acks.Length; i++)
                                    bad_work_acks[i] = 0;
                                Program.Logger("To high nack ratio, trying to close -> open UART device and reconnect. ");
                                ForceReconnect();
                            }
                        }
                        return true; /* full message received clear receive buffer */
                    }
                    break;

                case 4: /* found nonce */
                    if (dataLength < (nonce_length + 2))
                    {
                        return false;  /* wait for more data */
                    }
                    else if (dataLength == (nonce_length + 2))
                    {
                        byte wtid = d[1];
                        FoundNonce(d, dataLength);
                        return true; /* full message received clear receive buffer */
                    }
                    break;
            }
            return true;
        }
    }

}

