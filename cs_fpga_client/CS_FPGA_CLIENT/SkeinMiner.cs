using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{
 
    public class SkeinMiner : StandardNonceCallbackI
    {
        private StandardStratum sks;
        private Device device;
        private StandardWorkerI skeinWorker;
        StandardStratum.Work curWork = null, prevWork = null; /* We need to check nonces comming back against previus work as well */
        private bool stopped = false;

        private HashLib.IHash skein_hash;
        private HashLib.IHash sha256_hash;

        public SkeinMiner(StandardStratum sks, Device device, StandardWorkerI skeinWorker)
        {
            this.sks = sks;
            this.skeinWorker = skeinWorker;
            this.device = device;
            skeinWorker.SetNonceCallback(this);

            skein_hash = HashLib.HashFactory.Crypto.SHA3.CreateSkein512();
            sha256_hash = HashLib.HashFactory.Crypto.CreateSHA256();
        }


        private static void skein_tfbig_addkey(ref ulong w0, ref ulong w1, ref ulong w2, ref ulong w3, ref ulong w4, ref ulong w5, ref ulong w6, ref ulong w7, ulong[] h, ulong[] t, uint s)
        {
            w0 = w0 + h[(s + 0) % 9];
            w1 = w1 + h[(s + 1) % 9];
            w2 = w2 + h[(s + 2) % 9];
            w3 = w3 + h[(s + 3) % 9];
            w4 = w4 + h[(s + 4) % 9];
            w5 = w5 + h[(s + 5) % 9] + t[(s + 0) % 3];
            w6 = w6 + h[(s + 6) % 9] + t[(s + 1) % 3];
            w7 = w7 + h[(s + 7) % 9] + s;
        }

        private static ulong rotl(ulong a_uint, int a_n)
        {
            return ((a_uint << a_n) | (a_uint >> (64 - a_n)));
        }

        private static void skein_tfbig_mix8(ref ulong p0, ref ulong p1, ref ulong p2, ref ulong p3, ref ulong p4, ref ulong p5, ref ulong p6, ref ulong p7, int r0, int r1, int r2, int r3)
        {
            p0 = p0 + p1;
            p1 = rotl(p1, r0) ^ p0;

            p2 = p2 + p3;
            p3 = rotl(p3, r1) ^ p2;

            p4 = p4 + p5;
            p5 = rotl(p5, r2) ^ p4;

            p6 = p6 + p7;
            p7 = rotl(p7, r3) ^ p6;
        }

        private static void skein_tfbig_4e(ref ulong p0, ref ulong p1, ref ulong p2, ref ulong p3, ref ulong p4, ref ulong p5, ref ulong p6, ref ulong p7, ulong[] t, ulong[] h, uint s)
        {
            skein_tfbig_addkey(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, h, t, s);
            skein_tfbig_mix8(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, 46, 36, 19, 37);
            skein_tfbig_mix8(ref p2, ref p1, ref p4, ref p7, ref p6, ref p5, ref p0, ref p3, 33, 27, 14, 42);
            skein_tfbig_mix8(ref p4, ref p1, ref p6, ref p3, ref p0, ref p5, ref p2, ref p7, 17, 49, 36, 39);
            skein_tfbig_mix8(ref p6, ref p1, ref p0, ref p7, ref p2, ref p5, ref p4, ref p3, 44, 9, 54, 56);
        }

        private static void skein_tfbig_4o(ref ulong p0, ref ulong p1, ref ulong p2, ref ulong p3, ref ulong p4, ref ulong p5, ref ulong p6, ref ulong p7, ulong[] t, ulong[] h, uint s)
        {
            skein_tfbig_addkey(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, h, t, s);
            skein_tfbig_mix8(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, 39, 30, 34, 24);
            skein_tfbig_mix8(ref p2, ref p1, ref p4, ref p7, ref p6, ref p5, ref p0, ref p3, 13, 50, 10, 17);
            skein_tfbig_mix8(ref p4, ref p1, ref p6, ref p3, ref p0, ref p5, ref p2, ref p7, 25, 29, 39, 43);
            skein_tfbig_mix8(ref p6, ref p1, ref p0, ref p7, ref p2, ref p5, ref p4, ref p3, 8, 35, 56, 22);
        }

        private static void skein_ubi_big(byte[] buf, ulong[] h, ulong[] t)
        {
            ulong[] m = new ulong[8];
            Buffer.BlockCopy(buf, 0, m, 0, 64);
            ulong p0 = m[0];
            ulong p1 = m[1];
            ulong p2 = m[2];
            ulong p3 = m[3];
            ulong p4 = m[4];
            ulong p5 = m[5];
            ulong p6 = m[6];
            ulong p7 = m[7];
            //t[0] = (bcount << 6);
            //t[1] = (bcount >> 58) + (etype << 55);
            //t[2] = t[0] ^ t[1];
            //h[8] = h[0] ^ h[1] ^ h[2] ^ h[3] ^ h[4] ^ h[5] ^ h[6] ^ h[7] ^ 0x1BD11BDAA9FC1A22;
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 0);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 1);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 2);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 3);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 4);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 5);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 6);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 7);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 8);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 9);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 10);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 11);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 12);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 13);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 14);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 15);
            skein_tfbig_4e(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 16);
            skein_tfbig_4o(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, t, h, 17);
            skein_tfbig_addkey(ref p0, ref p1, ref p2, ref p3, ref p4, ref p5, ref p6, ref p7, h, t, 18);
            h[0] = m[0] ^ p0;
            h[1] = m[1] ^ p1;
            h[2] = m[2] ^ p2;
            h[3] = m[3] ^ p3;
            h[4] = m[4] ^ p4;
            h[5] = m[5] ^ p5;
            h[6] = m[6] ^ p6;
            h[7] = m[7] ^ p7;
        }

        private byte[] generate_miner_input(byte[] buf)
        {
            ulong[] h = new ulong[] { 0x4903ADFF749C51CE, 0x0D95DE399746DF03, 0x8FD1934127C79BCE, 0x9A255629FF352CB1, 0x5DB62599DF6CA7B0, 0xEABE394CA9D5C3F4, 0x991112C71A75B523, 0xAE18A40B660FCC33, 0xcab2076d98173ec4 };
            ulong[] t = new ulong[] { 64, 0x7000000000000000, 0x7000000000000040 };
            skein_ubi_big(buf, h, t);
            h[8] = h[0] ^ h[1] ^ h[2] ^ h[3] ^ h[4] ^ h[5] ^ h[6] ^ h[7] ^ 0x1BD11BDAA9FC1A22;

            byte[] miner_input = new byte[9 * 8 + 8 + 4];
            Buffer.BlockCopy(h, 0, miner_input, 0, 9 * 8);
            miner_input[9 * 8 + 0] = buf[64 + 0];
            miner_input[9 * 8 + 1] = buf[64 + 1];
            miner_input[9 * 8 + 2] = buf[64 + 2];
            miner_input[9 * 8 + 3] = buf[64 + 3];
            miner_input[9 * 8 + 4] = buf[64 + 4];
            miner_input[9 * 8 + 5] = buf[64 + 5];
            miner_input[9 * 8 + 6] = buf[64 + 6];
            miner_input[9 * 8 + 7] = buf[64 + 7];
            miner_input[9 * 8 + 8] = buf[64 + 8];
            miner_input[9 * 8 + 9] = buf[64 + 9];
            miner_input[9 * 8 + 10] = buf[64 + 10];
            miner_input[9 * 8 + 11] = buf[64 + 11];



            return miner_input;
        }

        private uint[] calc_skein_mining_hash(byte[] d)
        {
            //Console.WriteLine("hash input : " + Utilities.hex(d, ", 0x"));
            byte[] skein_out = skein_hash.ComputeBytes(d).GetBytes();
            byte[] sha256_out = sha256_hash.ComputeBytes(skein_out).GetBytes();
            uint[] hash = new uint[8];
            Buffer.BlockCopy(sha256_out, 0, hash, 0, 32);
            //Console.WriteLine("hash output : " + Utilities.hex(hash, ", 0x"));
            return hash;
        }

        public void workLoop()
        {

            // Wait for the first skein work to arrive.
            int elapsedTime = 0;
            while ((sks.GetJob() == null) && !stopped)
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
                    curWork = sks.GetWork();                        // Get a new work item (can be for the same job). 
                    skeinWorker.NewWork(generate_miner_input(curWork.Blob));
                }

                // let's do 200 loops, 10ms delay each loop, should be less then 4s.  Then bail out if we get new orders from pool ofcourse. 
                //for (int i = 0; i < 200; i++)
                for (int i = 0; i < 200; i++)
                {
                    if (stopped || (sks.GetJob().Equals(curWork.Job) == false))
                    {
                        Console.WriteLine("DEBUG - BREAK TO GIVE NEW POOL JOB");
                        break;
                    }
                    System.Threading.Thread.Sleep(10);
                }

            }
        }

        public void FoundNonce(uint nonce)
        {
            if (curWork == null)
                return;

            UInt64 target = (UInt64)((double)0xffff0000UL / sks.Difficulty);
            //UInt64 target = (UInt64)((double)0xffff0000UL / (kcs.Difficulty / 256));
            Console.WriteLine("target : " + String.Format("0x{0:X16}", target));

            
            byte[] hashIn = curWork.Blob;
            hashIn[79] = (byte)((nonce >> 24) & 0xff);
            hashIn[78] = (byte)((nonce >> 16) & 0xff);
            hashIn[77] = (byte)((nonce >> 8) & 0xff);
            hashIn[76] = (byte)((nonce) & 0xff);
            uint[] h32 = calc_skein_mining_hash(hashIn);
            /* Difficulty check */
            ulong val = ((ulong)h32[6]) | ((ulong)h32[7] << 32);
            if (val <= target)
            {
                sks.Submit(device, curWork, nonce);

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
                uint[] prev_h32 = calc_skein_mining_hash(hashIn); ;
                ulong val2 = ((ulong)prev_h32[6]) | ((ulong)prev_h32[7] << 32);
                if (val2 <= target)
                {
                    sks.Submit(device, prevWork, nonce);
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




            // Test code to searc hfor right combo and nonce-slip 
            uint s_nonce = nonce - 100;
            uint e_nonce = nonce + 100;
            uint o_nonce = nonce;

            for (nonce = s_nonce; nonce < e_nonce; nonce++)
            {

                hashIn = curWork.Blob;
                hashIn[76] = (byte)((nonce >> 24) & 0xff);
                hashIn[77] = (byte)((nonce >> 16) & 0xff);
                hashIn[78] = (byte)((nonce >> 8) & 0xff);
                hashIn[79] = (byte)((nonce) & 0xff);
                h32 = calc_skein_mining_hash(hashIn);
                if (((h32[7] & 0xFFFF) == 0) || ((h32[7] & 0xFFFF0000) == 0))
                    Console.WriteLine(""+(int)(nonce- o_nonce)+ " H1: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h32 = calc_skein_mining_hash(hashIn);
                if (((h32[7] & 0xFFFF) == 0) || ((h32[7] & 0xFFFF0000) == 0))
                    Console.WriteLine("" + (int)(nonce - o_nonce) + " H2: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[76] = (byte)((nonce >> 24) & 0xff);
                hashIn[77] = (byte)((nonce >> 16) & 0xff);
                hashIn[78] = (byte)((nonce >> 8) & 0xff);
                hashIn[79] = (byte)((nonce) & 0xff);
                h32 = calc_skein_mining_hash(hashIn);
                if (((h32[7] & 0xFFFF) == 0) || ((h32[7] & 0xFFFF0000) == 0))
                    Console.WriteLine("" + (int)(nonce - o_nonce) + " H3: " + Utilities.hex(h32, " "));

                hashIn = curWork.Blob;
                hashIn = Utilities.FlipByteArrayUInt32(hashIn);
                hashIn[79] = (byte)((nonce >> 24) & 0xff);
                hashIn[78] = (byte)((nonce >> 16) & 0xff);
                hashIn[77] = (byte)((nonce >> 8) & 0xff);
                hashIn[76] = (byte)((nonce) & 0xff);
                h32 = calc_skein_mining_hash(hashIn);
                if (((h32[7] & 0xFFFF) == 0) || ((h32[7] & 0xFFFF0000) == 0))
                    Console.WriteLine("" + (int)(nonce - o_nonce) + " H4: " + Utilities.hex(h32, " "));

            }

        }


    }

}



