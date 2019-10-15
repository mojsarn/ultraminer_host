using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CS_FPGA_CLIENT
{
    public interface PascNonceCallbackI
    {
        void FoundNonce(uint nonce);
    }
    public interface PascalWorkerI
    {
        void SetNonceCallback(PascNonceCallbackI nonceCallback);
        void NewWork(uint[] data) ;
    }

    public class PascalMiner : PascNonceCallbackI
    {

        private PascalStratum ps;
        private Device device;
        private PascalWorkerI pascalWorker;
        PascalMinerWork curWork = null, prevWork = null; /* We need to check nonces comming back against previus work as well */

        private class PascalMinerWork
        {
            public static readonly int sPascalInputSize = 196;
            public static readonly int sPascalMidstateSize = 32;
            public byte[] mPascalInput = new byte[sPascalInputSize];
            public byte[] mPascalMidstate = new byte[sPascalMidstateSize];
            public PascalStratum.Work work;

            private static byte[] test1_mPascalMidState = { 0x97, 0xFE, 0xA5, 0xE5, 0xD4, 0x06, 0x4C, 0xB1, 0xB1, 0xBD, 0x62, 0x51, 0xC8, 0x0F, 0xA9, 0x54, 0x8D, 0x77, 0x15, 0x83, 0xA2, 0x91, 0x68, 0xCD, 0x01, 0x6E, 0xAF, 0x48, 0x94, 0x45, 0x0C, 0xA6 };
            private static byte[] test1_mPascalInput = { 0x8C, 0xD1, 0x01, 0x00, 0xCA, 0x02, 0x20, 0x00, 0x66, 0x62, 0x93, 0xEB, 0x10, 0x87, 0x63, 0xDE, 0x78, 0x0F, 0xD6, 0xEE, 0x5F, 0x2D, 0x8F, 0x92, 0xA9, 0xC6, 0x9F, 0xC3, 0xE3, 0x6B, 0x5A, 0x40, 0xA9, 0xE8, 0xD2, 0x55, 0x23, 0xF6, 0x19, 0xC7, 0x20, 0x00, 0x97, 0xFC, 0x79, 0x5B, 0x55, 0xD5, 0x0A, 0x41, 0xDC, 0x8A, 0xBD, 0x09, 0x9A, 0xDF, 0x96, 0x15, 0x2A, 0x2F, 0x07, 0xA1, 0xC3, 0x54, 0x80, 0xDD, 0x45, 0x12, 0xAB, 0xC4, 0xE4, 0x26, 0x69, 0x01, 0x20, 0xA1, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x6A, 0x8F, 0xCA, 0x30, 0x70, 0x61, 0x73, 0x6C, 0x2E, 0x66, 0x61, 0x69, 0x72, 0x70, 0x6F, 0x6F, 0x6C, 0x2E, 0x78, 0x79, 0x7A, 0x5F, 0x45, 0x35, 0x30, 0x45, 0x36, 0x46, 0x37, 0x45, 0x55, 0x2D, 0x47, 0xD2, 0x4C, 0x31, 0xA5, 0xF6, 0x66, 0xD3, 0x9E, 0x66, 0x1C, 0xA8, 0x9F, 0xE3, 0xD0, 0x28, 0x2D, 0x85, 0x25, 0x7E, 0xA1, 0xEC, 0xF9, 0x2C, 0x88, 0xA1, 0xA1, 0x20, 0x70, 0xD3, 0xAB, 0x58, 0x27, 0x03, 0x00, 0x99, 0x41, 0x47, 0xDB, 0x76, 0x17, 0x25, 0x39, 0xEB, 0x24, 0xE5, 0xC3, 0x1C, 0xF1, 0xB7, 0x26, 0xF9, 0xF3, 0xB6, 0x80, 0xDA, 0xE0, 0x48, 0xAF, 0x7D, 0xB3, 0x3A, 0x9D, 0xC7, 0x6D, 0xB8, 0x6F, 0x12, 0xAB, 0x90, 0x00, 0x00, 0x00, 0x00, 0xC6, 0xD1, 0xD2, 0x5A };
            private static uint test1_nonce = 2156823382;

            private static byte[] test2_mPascalInput = { 0x8C, 0xD1, 0x01, 0x00, 0xCA, 0x02, 0x20, 0x00, 0x66, 0x62, 0x93, 0xEB, 0x10, 0x87, 0x63, 0xDE, 0x78, 0x0F, 0xD6, 0xEE, 0x5F, 0x2D, 0x8F, 0x92, 0xA9, 0xC6, 0x9F, 0xC3, 0xE3, 0x6B, 0x5A, 0x40, 0xA9, 0xE8, 0xD2, 0x55, 0x23, 0xF6, 0x19, 0xC7, 0x20, 0x00, 0x97, 0xFC, 0x79, 0x5B, 0x55, 0xD5, 0x0A, 0x41, 0xDC, 0x8A, 0xBD, 0x09, 0x9A, 0xDF, 0x96, 0x15, 0x2A, 0x2F, 0x07, 0xA1, 0xC3, 0x54, 0x80, 0xDD, 0x45, 0x12, 0xAB, 0xC4, 0xE4, 0x26, 0x69, 0x01, 0x20, 0xA1, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x6A, 0x8F, 0xCA, 0x30, 0x70, 0x61, 0x73, 0x6C, 0x2E, 0x66, 0x61, 0x69, 0x72, 0x70, 0x6F, 0x6F, 0x6C, 0x2E, 0x78, 0x79, 0x7A, 0x5F, 0x45, 0x35, 0x30, 0x45, 0x36, 0x46, 0x37, 0x45, 0xEC, 0x2D, 0xCB, 0x28, 0x7C, 0xB5, 0xFD, 0x2A, 0x66, 0xD3, 0x9E, 0x66, 0x1C, 0xA8, 0x9F, 0xE3, 0xD0, 0x28, 0x2D, 0x85, 0x25, 0x7E, 0xA1, 0xEC, 0xF9, 0x2C, 0x88, 0xA1, 0xA1, 0x20, 0x70, 0xD3, 0xAB, 0x58, 0x27, 0x03, 0x00, 0x99, 0x41, 0x47, 0xD5, 0x6C, 0x3D, 0x38, 0xEE, 0x26, 0x71, 0x6C, 0x31, 0xBD, 0xFF, 0x7C, 0x5F, 0xFF, 0x76, 0xC9, 0x24, 0xA7, 0x35, 0x08, 0x44, 0x6D, 0x3C, 0xB0, 0x95, 0x4E, 0xCE, 0x54, 0x79, 0x06, 0x11, 0x74, 0x00, 0x00, 0x00, 0x00, 0x05, 0xD2, 0xD2, 0x5A };
            private static uint test2_nonce = 782512610;

            public PascalMinerWork(PascalStratum.Work work)
            {
                this.work = work;
                CalculatePascalMidState();
            }

            static uint[] H256 =
            {
                0x6A09E667, 0xBB67AE85, 0x3C6EF372,
                0xA54FF53A, 0x510E527F, 0x9B05688C,
                0x1F83D9AB, 0x5BE0CD19
            };

            // based on HashLib's SHA256 implementation
            private static readonly uint[] s_K =
            {
                0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
                0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
                0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
                0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
                0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
                0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
                0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
                0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
            };

            private void CalculatePascalMidState()
            {
                Array.Copy(work.Blob, mPascalInput, sPascalInputSize);

                uint[] state = new uint[] { 0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19 };

                for (int block = 0; block < 3; ++block)
                {
                    uint[] data = new uint[80];

                    uint A = state[0];
                    uint B = state[1];
                    uint C = state[2];
                    uint D = state[3];
                    uint E = state[4];
                    uint F = state[5];
                    uint G = state[6];
                    uint H = state[7];

                    for (int j = 0; j < 16; ++j)
                        data[j] = (uint)(mPascalInput[block * 64 + j * 4 + 0] << 24)
                                  | (uint)(mPascalInput[block * 64 + j * 4 + 1] << 16)
                                  | (uint)(mPascalInput[block * 64 + j * 4 + 2] << 8)
                                  | (uint)(mPascalInput[block * 64 + j * 4 + 3] << 0);

                    for (int r = 16; r < 64; r++)
                    {
                        uint T = data[r - 2];
                        uint T2 = data[r - 15];
                        data[r] = (((T >> 17) | (T << 15)) ^ ((T >> 19) | (T << 13)) ^ (T >> 10)) + data[r - 7] +
                            (((T2 >> 7) | (T2 << 25)) ^ ((T2 >> 18) | (T2 << 14)) ^ (T2 >> 3)) + data[r - 16];
                    }

                    for (int r = 0; r < 64; r++)
                    {
                        uint T = s_K[r] + data[r] + H + (((E >> 6) | (E << 26)) ^ ((E >> 11) | (E << 21)) ^ ((E >> 25) |
                                 (E << 7))) + ((E & F) ^ (~E & G));
                        uint T2 = (((A >> 2) | (A << 30)) ^ ((A >> 13) | (A << 19)) ^
                                  ((A >> 22) | (A << 10))) + ((A & B) ^ (A & C) ^ (B & C));
                        H = G;
                        G = F;
                        F = E;
                        E = D + T;
                        D = C;
                        C = B;
                        B = A;
                        A = T + T2;
                    }

                    state[0] += A;
                    state[1] += B;
                    state[2] += C;
                    state[3] += D;
                    state[4] += E;
                    state[5] += F;
                    state[6] += G;
                    state[7] += H;
                }

                for (int j = 0; j < 8; ++j)
                {
                    mPascalMidstate[j * 4 + 0] = (byte)((state[j] >> 0) & 0xff);
                    mPascalMidstate[j * 4 + 1] = (byte)((state[j] >> 8) & 0xff);
                    mPascalMidstate[j * 4 + 2] = (byte)((state[j] >> 16) & 0xff);
                    mPascalMidstate[j * 4 + 3] = (byte)((state[j] >> 24) & 0xff);
                }
            }


            static void SHA256Transform(uint[] data, uint[] m_state, uint[] m_state_out)
            {
                uint A = m_state[0];
                uint B = m_state[1];
                uint C = m_state[2];
                uint D = m_state[3];
                uint E = m_state[4];
                uint F = m_state[5];
                uint G = m_state[6];
                uint H = m_state[7];

                for (int r = 16; r < 64; r++)
                {
                    uint T = data[r - 2];
                    uint T2 = data[r - 15];
                    data[r] = (((T >> 17) | (T << 15)) ^ ((T >> 19) | (T << 13)) ^ (T >> 10)) + data[r - 7] +
                        (((T2 >> 7) | (T2 << 25)) ^ ((T2 >> 18) | (T2 << 14)) ^ (T2 >> 3)) + data[r - 16];
                }

                for (int r = 0; r < 64; r++)
                {
                    uint T = s_K[r] + data[r] + H + (((E >> 6) | (E << 26)) ^ ((E >> 11) | (E << 21)) ^ ((E >> 25) |
                             (E << 7))) + ((E & F) ^ (~E & G));
                    uint T2 = (((A >> 2) | (A << 30)) ^ ((A >> 13) | (A << 19)) ^
                              ((A >> 22) | (A << 10))) + ((A & B) ^ (A & C) ^ (B & C));
                    H = G;
                    G = F;
                    F = E;
                    E = D + T;
                    D = C;
                    C = B;
                    B = A;
                    A = T + T2;
                }

                m_state_out[0] = m_state[0] + A;
                m_state_out[1] = m_state[1] + B;
                m_state_out[2] = m_state[2] + C;
                m_state_out[3] = m_state[3] + D;
                m_state_out[4] = m_state[4] + E;
                m_state_out[5] = m_state[5] + F;
                m_state_out[6] = m_state[6] + G;
                m_state_out[7] = m_state[7] + H;
            }

            static uint[] pad_data =
            {
                0x00000000, 0x00000000, 0x80000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000640
            };

            static uint[] pad_data2 =
            {
                0x80000000, 0x00000000, 0x00000000, 0x00000000,
                0x00000000, 0x00000000, 0x00000000, 0x00000100
            };

            public uint[] HashPascal(uint nonce)
            {
                uint[] data = new uint[64];
                uint[] state = new uint[8];
                Buffer.BlockCopy(mPascalMidstate, 0, state, 0, 8 * 4);
                Array.Copy(pad_data, data, pad_data.Length);
                data[0] = ((uint)mPascalInput[192] << 24) | ((uint)mPascalInput[193] << 16) | ((uint)mPascalInput[194] << 8) | ((uint)mPascalInput[195]);
                //Console.WriteLine("data_0:" + String.Format("0x{0:X8}", data[0]));
                data[1] = Utilities.swap32(nonce);
                SHA256Transform(data, state, data);
                Array.Copy(pad_data2, 0, data, 8, 8);
                SHA256Transform(data, H256, state);
                return state;
            }

            public bool findHashCPU(uint nonce, uint amount, ulong target, out uint foundNonce)
            {
                int i;
                for (i = 0; i < amount; i++)
                {
                    uint[] state = HashPascal(nonce);
                    ulong val = ((ulong)state[1]) | ((ulong)state[0] << 32);
                    if (val <= target)
                    {
                        //Program.Logger("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                        Program.Logger("FOUND NONCE state : " + Utilities.hex(state, " "));
                        foundNonce = nonce;
                        return true;
                    }
                    nonce++;
                }
                foundNonce = 0;
                return false;
            }

            public void sendWorkToWorker(PascalWorkerI pascalWorker)
            {
                uint[] d = new uint[9];
                Buffer.BlockCopy(mPascalMidstate, 0, d, 0, 8 * 4);
                d[8] = ((uint)mPascalInput[192] << 24) | ((uint)mPascalInput[193] << 16) | ((uint)mPascalInput[194] << 8) | ((uint)mPascalInput[195]);
                pascalWorker.NewWork(d);
            }

            public void test()
            {
                Array.Copy(test1_mPascalMidState, mPascalMidstate, mPascalMidstate.Length);
                Array.Copy(test1_mPascalInput, mPascalInput, mPascalInput.Length);

                Console.WriteLine("mid state : " + Utilities.hex(mPascalMidstate));
                CalculatePascalMidState();
                Console.WriteLine("mid state : " + Utilities.hex(mPascalMidstate));

                uint foundNonce;
                findHashCPU(test1_nonce/* - 20*/, 40, (UInt64)((double)0xffff0000UL / 2), out foundNonce);
                Console.WriteLine("FOUND NOCNE : " + foundNonce + " expected : " + test1_nonce);

                Array.Copy(test2_mPascalInput, mPascalInput, mPascalInput.Length);
                CalculatePascalMidState();
                findHashCPU(test2_nonce - 20, 40, (UInt64)((double)0xffff0000UL / 2), out foundNonce);
                Console.WriteLine("FOUND NOCNE : " + foundNonce + " expected : " + test2_nonce);
            }

        }


        private bool stopped = false;

        public PascalMiner(PascalStratum ps, Device device, PascalWorkerI pascalWorker)
        {
            this.ps = ps;
            this.device = device;
            this.pascalWorker = pascalWorker;
            pascalWorker.SetNonceCallback(this);
        }


        public void workLoop()
        {

            // Wait for the first PascalJob to arrive.
            int elapsedTime = 0;
            while ((ps.GetJob() == null) && !stopped)
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
                
                PascalStratum.Work stratumWork = ps.GetWork();  // Get a new work item (can be for the same job). 

                // Callback will be asyncrhonus, so have to lock cur and pre work while updating them. 
                lock (this)
                {
                    prevWork = curWork;                             // remember the old work item in case the miner reports finished work on it, we can still hand them in (if the job is the same). 
                    curWork = new PascalMinerWork(stratumWork);
                    curWork.sendWorkToWorker(pascalWorker);
                }

                // let's do 200 loops, 10ms delay each loop, should be less then 4s.  Then bail out if we get new orders from pool ofcourse. 
                for (int i = 0; i < 200; i++)
                {
                    if (stopped || (ps.GetJob().Equals(stratumWork.Job) == false))
                        break;
                    System.Threading.Thread.Sleep(10);
                }
                
            }
        }

        public void FoundNonce(uint nonce)
        {
            /* Check if good enough nonce found, if so send to pool */
            UInt64 PascalTarget = (UInt64)((double)0xffff0000UL / ps.Difficulty);
            lock (this)
            {
                uint[] h_cur = curWork.HashPascal(nonce);
                uint[] h_prev;
                if (prevWork != null)
                    h_prev = prevWork.HashPascal(nonce);
                else
                    h_prev = new uint[8];
                //Console.WriteLine("h_cur:" + Utilities.hex(h_cur, " "));
                //Console.WriteLine("h_prev:" + Utilities.hex(h_prev, " "));

                bool nonceFound = false;

                ulong val = ((ulong)h_cur[1]) | ((ulong)h_cur[0] << 32);
                if(val <= PascalTarget)
                {
                    ps.Submit(device, curWork.work, nonce);
                    nonceFound = true;
                    //Program.Logger("Found hash : " + Utilities.hex(h_cur, " "))
                }

                val = ((ulong)h_prev[1]) | ((ulong)h_prev[0] << 32);
                if (val <= PascalTarget && prevWork != null)
                {
                    ps.Submit(device, prevWork.work, nonce);
                    nonceFound = true;
                    //Program.Logger("Found hash : " + Utilities.hex(h_cur, " "))
                }

                if(!nonceFound)
                {
                    Program.Logger("Found nonce, but neither cur or prev good enough");
                    Program.Logger("cur : " + Utilities.hex(h_cur, " "));
                    Program.Logger("prev : " + Utilities.hex(h_prev, " "));
                }
            }
        }

    }
}
