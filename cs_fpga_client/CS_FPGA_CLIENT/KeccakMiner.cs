using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{
    public interface StandardNonceCallbackI
    {
        void FoundNonce(uint nonce);
    }
    public interface StandardWorkerI
    {
        void SetNonceCallback(StandardNonceCallbackI nonceCallback);
        void NewWork(byte[] data);
    }

    public class KeccakMiner : StandardNonceCallbackI
    {
        private StandardStratum kcs;
        private Device device;
        private StandardWorkerI keccakWorker;
        StandardStratum.Work curWork = null, prevWork = null; /* We need to check nonces comming back against previus work as well */
        private bool stopped = false;

        private HashLib.IHash keccak_hash;

        public KeccakMiner(StandardStratum _kcs, Device _device, StandardWorkerI _keccakWorker)
        {
            kcs = _kcs;
            keccakWorker = _keccakWorker;
            device = _device;
            keccakWorker.SetNonceCallback(this);

            keccak_hash = HashLib.HashFactory.Crypto.SHA3.CreateKeccak256();
            //TTT();
        }

        public void workLoop()
        {

            // Wait for the first PascalJob to arrive.
            int elapsedTime = 0;
            while ((kcs.GetJob() == null) && !stopped)
            {
                System.Threading.Thread.Sleep(100);
                elapsedTime += 100;
                if (elapsedTime >= 5000)
                {
                    Program.Logger("Waiting for job from pool...\n");
                    elapsedTime = 0;
                }
            }



            while (!stopped)
            {
                // Each time pascalWork.Blob gives a different blob for the same work, so this is how we get more 32bit nonce search areas... 
                // Just have to rerun this loop at a higher rate then we run out of work nonces... 
                // For now let's make sure we call this at least every 2s, that means we can support hash speeds up to 2GH/s . 
                // Callback will be asyncrhonus, so have to lock cur and pre work while updating them. 
                lock (this)
                {
                    prevWork = curWork;                             // remember the old work item in case the miner reports finished work on it, we can still hand them in (if the job is the same). 
                    curWork = kcs.GetWork();                        // Get a new work item (can be for the same job). 
                    keccakWorker.NewWork(curWork.Blob);
                }

                // let's do 200 loops, 10ms delay each loop, should be less then 4s.  Then bail out if we get new orders from pool ofcourse. 
                //for (int i = 0; i < 200; i++)
                for (int i = 0; i < 200; i++)
                {
                    if (stopped || (kcs.GetJob().Equals(curWork.Job) == false))
                    {
                        Console.WriteLine("DEBUG - BREAK TO GIVE NEW POOL JOB");
                        break;
                    }
                    System.Threading.Thread.Sleep(10);
                }

            }
        }

        /*
        public void TTT()
        {
            // TESTING 
            HashLib.IHash hash = HashLib.HashFactory.Crypto.SHA3.CreateKeccak256();
            uint nonce = 1555736682;

            for (nonce = 1555736682-30; nonce < 1555736682+30; nonce++)
            {
                uint[] test_d = {
                0x02000000,
                0x1bf623cb,
                0x95be646d,
                0x1b9517bb,
                0xddcaf549,
                0x8ea62844,
                0x0764def0,
                0x73330000,
                0x00000000,
                0x808b4238,
                 0xc4277388,
                 0xab539e45,
                 0xc86d024c,
                 0x5c673def,
                 0xe0ce09dd,
                 0x16f4b072,
                 0xeb3242d5,
                 0xd54ff75a,
                 0xc2a9561a };
                byte[] hashIn = new byte[80];
                Buffer.BlockCopy(test_d, 0, hashIn, 0, 19 * 4);
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[76] = (byte)((nonce >> 24) & 0xff);
                hashIn[77] = (byte)((nonce >> 16) & 0xff);
                hashIn[78] = (byte)((nonce >> 8) & 0xff);
                hashIn[79] = (byte)((nonce) & 0xff);
                byte[] h = hash.ComputeBytes(hashIn).GetBytes();
                uint[] h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine("TTT H1: " + Utilities.hex(h32, " "));

                Buffer.BlockCopy(test_d, 0, hashIn, 0, 19 * 4);
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h = hash.ComputeBytes(hashIn).GetBytes();
                h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine("TTT H2: " + Utilities.hex(h32, " "));
            }
        }
        */

        public void FoundNonce(uint nonce)
        {
            if (curWork == null)
                return;

            UInt64 target = (UInt64)((double)0xffff0000UL / (kcs.Difficulty / 256));
            //UInt64 target = (UInt64)((double)0xffff0000UL / (kcs.Difficulty / 512));
            //Console.WriteLine("target : " + String.Format("0x{0:X16}", target));

            byte[] hashIn = curWork.Blob;
            hashIn[79] = (byte)((nonce >> 24) & 0xff);
            hashIn[78] = (byte)((nonce >> 16) & 0xff);
            hashIn[77] = (byte)((nonce >> 8) & 0xff);
            hashIn[76] = (byte)((nonce) & 0xff);
            byte[] h = keccak_hash.ComputeBytes(hashIn).GetBytes();
            uint[] h32 = new uint[h.Length >> 2];
            Buffer.BlockCopy(h, 0, h32, 0, h.Length);
            /* Difficulty check */
            ulong val = ((ulong)h32[6]) | ((ulong)h32[7] << 32);
            if (val <= target)
            {
                kcs.Submit(device, curWork, nonce);

                Program.Logger("submit val:" + String.Format("0x{0:X16}", val) + " t64:" + String.Format("0x{0:X16}", target));

                return;
            }
            

            if (prevWork != null)
            {
                hashIn = prevWork.Blob;
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h = keccak_hash.ComputeBytes(hashIn).GetBytes();
                uint[] prev_h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, prev_h32, 0, h.Length);
                ulong val2 = ((ulong)prev_h32[6]) | ((ulong)prev_h32[7] << 32);
                if (val2 <= target)
                {
                    kcs.Submit(device, prevWork, nonce);
                    return;
                }
                /*
                Program.Logger("curw: " + Utilities.hex(h32, " "));
                Program.Logger("prew: " + Utilities.hex(prev_h32, " "));
                uint[] t = new uint[2];
                t[0] = (uint)(target & 0xFFFFFFFF);
                t[1] = (uint)(target >> 32);
                Program.Logger("Neither did meet the difficulty target of " + Utilities.hex(t, " "));*/

                Program.Logger("val:" + String.Format("0x{0:X16}", val) + " val2: " + String.Format("0x{0:X16}", val2) + " t64:" + String.Format("0x{0:X16}", target));
            }



            /*
             // Test code to searc hfor right combo and nonce-slip 
            HashLib.IHash hash = HashLib.HashFactory.Crypto.SHA3.CreateKeccak256();

            uint s_nonce = nonce - 30;
            uint e_nonce = nonce + 30;
            uint o_nonce = nonce;

            for (nonce = s_nonce; nonce < e_nonce; nonce++)
            {

                byte[] hashIn = curWork.Blob;
                hashIn[76] = (byte)((nonce >> 24) & 0xff);
                hashIn[77] = (byte)((nonce >> 16) & 0xff);
                hashIn[78] = (byte)((nonce >> 8) & 0xff);
                hashIn[79] = (byte)((nonce) & 0xff);
                byte[] h = hash.ComputeBytes(hashIn).GetBytes();
                uint[] h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine(""+(int)(nonce- o_nonce)+ " H1: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h = hash.ComputeBytes(hashIn).GetBytes();
                h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine("" + (int)(nonce - o_nonce) + " H2: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[76] = (byte)((nonce >> 24) & 0xff);
                hashIn[77] = (byte)((nonce >> 16) & 0xff);
                hashIn[78] = (byte)((nonce >> 8) & 0xff);
                hashIn[79] = (byte)((nonce) & 0xff);
                h = hash.ComputeBytes(hashIn).GetBytes();
                h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine("" + (int)(nonce - o_nonce) + " H3: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h = hash.ComputeBytes(hashIn).GetBytes();
                h32 = new uint[h.Length >> 2];
                Buffer.BlockCopy(h, 0, h32, 0, h.Length);
                Console.WriteLine("" + (int)(nonce - o_nonce) + " H4: " + Utilities.hex(h32, " "));

            }
            */

        }


    }


}
