using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
//using HashLib;



namespace CS_FPGA_CLIENT
{
    class LbryStratum : Stratum
    {
        public new class Work : Stratum.Work
        {
            readonly private Job mJob;

            public Job Job {
                get { return mJob; }
            }

            public Work(Job aJob)
                : base(aJob)
            {
                mJob = aJob;
            }


            public byte[] Blob
            {
                get
                {
                    IHash hash = HashFactory.Crypto.CreateSHA256();
                    byte[] blob = new byte[112];
                    if (Job != null) {
                        byte[] coinbase = Utilities.StringToByteArray(Job.Coinbase1
                            + Job.Stratum.PoolExtranonce
                            + LocalExtranonceString
                            + Job.Coinbase2);
                        byte[] merkle_root = hash.ComputeBytes(hash.ComputeBytes(coinbase).GetBytes()).GetBytes();
                        foreach (var merkle in Job.Merkles)
                            merkle_root = hash.ComputeBytes(hash.ComputeBytes(Utilities.StringToByteArray(Utilities.ByteArrayToString(merkle_root) + merkle)).GetBytes()).GetBytes();
                        Buffer.BlockCopy(Utilities.StringToByteArray(Job.PrevHash), 0, blob, 4, 32);
                        for (int i = 0; i < 8; ++i) {
                            blob[36 + i * 4 + 0] = merkle_root[i * 4 + 3];
                            blob[36 + i * 4 + 1] = merkle_root[i * 4 + 2];
                            blob[36 + i * 4 + 2] = merkle_root[i * 4 + 1];
                            blob[36 + i * 4 + 3] = merkle_root[i * 4 + 0];
                        }
                        Buffer.BlockCopy(Utilities.StringToByteArray(Job.Trie), 0, blob, 68, 32);

                        var array = Utilities.StringToByteArray(Job.Version);
                        blob[0] = array[0];
                        blob[1] = array[1];
                        blob[2] = array[2];
                        blob[3] = array[3];
                        array = Utilities.StringToByteArray(Job.NTime);
                        blob[100] = array[0];
                        blob[101] = array[1];
                        blob[102] = array[2];
                        blob[103] = array[3];
                        array = Utilities.StringToByteArray(Job.NBits);
                        blob[104] = array[0];
                        blob[105] = array[1];
                        blob[106] = array[2];
                        blob[107] = array[3];
                    }

                    return blob;
                }
            }
        }

        public new class Job : Stratum.Job
        {
            private string mID;
            private string mPrevHash;
            private string mTrie;
            private string mCoinbase1;
            private string mCoinbase2;
            private string[] mMerkles;
            private string mNBits;
            private string mNTime;
            private string mVersion;
            private LbryStratum mStratum;

            public String ID { get { return mID; } }
            public String PrevHash { get { return mPrevHash; } }
            public String Trie { get { return mTrie; } }
            public String Coinbase1 { get { return mCoinbase1; } }
            public String Coinbase2 { get { return mCoinbase2; } }
            public String[] Merkles { get { return mMerkles; } }
            public String NBits { get { return mNBits; } }
            public String NTime { get { return mNTime; } }
            public String Version { get { return mVersion; } }
            public new LbryStratum Stratum { get { return mStratum; } }

            public Job(LbryStratum aStratum, string aID, string aPrevHash, string aTrie, string aCoinbase1, string aCoinbase2, string[] aMerkles, string aVersion, string aNBits, string aNTime)
                : base(aStratum)
            {
                mStratum = aStratum;
                mID = aID;
                mPrevHash = aPrevHash;
                mTrie = aTrie;
                mCoinbase1 = aCoinbase1;
                mCoinbase2 = aCoinbase2;
                mMerkles = aMerkles;
                mVersion = aVersion;
                mNBits = aNBits;
                mNTime = aNTime;
            }

            public bool Equals(Job aJob) {
                return aJob != null
                    && mID == aJob.mID
                    && mPrevHash == aJob.mPrevHash
                    && mCoinbase1 == aJob.mCoinbase1
                    && mCoinbase2 == aJob.mCoinbase2
                    && mMerkles == aJob.mMerkles
                    && mVersion == aJob.mVersion
                    && mNBits == aJob.mNBits
                    && mNTime == aJob.mNTime;
            }
        }

        private int mJsonRPCMessageID = 1;
        private Job mJob = null;
        private Mutex mMutex = new Mutex();

        public Job GetJob()
        {
            return mJob;
        }

        public new Work GetWork()
        {
            return new Work(mJob);
        }

        protected override void ProcessLine(String line)
        {
            //Program.Logger("line: " + line);
            Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(line);
            if (response.ContainsKey("method") && response.ContainsKey("params"))
            {
                string method = (string)response["method"];
                JArray parameters = (JArray)response["params"];
                if (method.Equals("mining.set_difficulty"))
                {
                    try  { mMutex.WaitOne(5000); } catch (Exception) { }
                    mDifficulty = (double)parameters[0];
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    Program.Logger("Difficulty set to " + (double)parameters[0] + ".");
                }
                else if (method.Equals("mining.notify"))
                {
                    bool jobChanged = (mJob == null || mJob.ID != (string)parameters[0]);
                    try { mMutex.WaitOne(5000); }
                    catch (Exception) { }
                    mJob = (new Job(this, (string)parameters[0], (string)parameters[1], (string)parameters[2], (string)parameters[3], (string)parameters[4], Array.ConvertAll(((JArray)parameters[5]).ToArray(), item => (string)item), (string)parameters[6], (string)parameters[7], (string)parameters[8]));
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    if (!SilentMode && jobChanged) Program.Logger("Received new job: " + parameters[0]);
                }
                else if (method.Equals("mining.set_extranonce"))
                {
                    try  { mMutex.WaitOne(5000); } catch (Exception) { }
                    mPoolExtranonce = (String)parameters[0];
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    Program.Logger("Received new extranonce: " + parameters[0]);
                }
                else if (method.Equals("client.reconnect"))
                {
                    throw new Exception("client.reconnect");
                }
                else
                {
                    Program.Logger("Unknown stratum method: " + line);
                }
            }   
            else if (response.ContainsKey("id") && response.ContainsKey("result"))
            {
                var ID = response["id"].ToString();
                bool result = (response["result"] == null) ? false : (bool)response["result"];

                if (ID == "3" && !result)
                {
                    throw (UnrecoverableException = new UnrecoverableException("Authorization failed."));
                }
                else if ((ID != "1" && ID != "2" && ID != "3") && result)
                {
                    ReportAcceptedShare();
                } else if ((ID != "1" && ID != "2" && ID != "3") && !result)
                {
                    ReportRejectedShare((String)(((JArray)response["error"])[1]));
                }
            }
            else
            {
                Program.Logger("Unknown JSON message: " + line);
            }
        }

        protected override void Authorize()
        {
            try  { mMutex.WaitOne(5000); } catch (Exception) { }

            mJsonRPCMessageID = 1;

            WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.subscribe" },
                { "params", new List<string> {
                    Program.appName + "/" + Program.appVersion
            }}}));

            try {
                Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(ReadLine());
                //mSubsciptionID = (string)(((JArray)(((JArray)(response["result"]))[0]))[1]);
                mPoolExtranonce = (string)(((JArray)(response["result"]))[1]);
                LocalExtranonceSize = (int)(((JArray)(response["result"]))[2]);
                //Program.Logger("mLocalExtranonceSize: " + mLocalExtranonceSize);
            } catch (Exception) {
                throw this.UnrecoverableException = new AuthorizationFailedException();
            }
            
            // mining.extranonce.subscribe
            WriteLine(JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.extranonce.subscribe" },
                { "params", new List<string> {
            }}}));

            WriteLine(JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.authorize" },
                { "params", new List<string> {
                    Username,
                    Password
            }}}));

            try  { mMutex.ReleaseMutex(); } catch (Exception) { }
        }

        public bool VerifyShare(LbryStratum.Work work, UInt32 aNonce, string result)
        {
            var hashSHA256 = HashLib.HashFactory.Crypto.CreateSHA256();
            var hashSHA512 = HashLib.HashFactory.Crypto.CreateSHA512();
            var hashRIPEMD160 = HashLib.HashFactory.Crypto.CreateRIPEMD160();

            byte[] input = work.Blob;
            input[108] = (byte)((aNonce >> 24) & 0xff);
            input[109] = (byte)((aNonce >> 16) & 0xff);
            input[110] = (byte)((aNonce >> 8) & 0xff);
            input[111] = (byte)((aNonce >> 0) & 0xff);

            byte[] inputSwapped = new byte[112];
            for (int i = 0; i < 28; i++)
            {
                inputSwapped[4 * i + 0] = input[4 * i + 3];
                inputSwapped[4 * i + 1] = input[4 * i + 2];
                inputSwapped[4 * i + 2] = input[4 * i + 1];
                inputSwapped[4 * i + 3] = input[4 * i + 0];
            }

            byte[] hash0 = hashSHA256.ComputeBytes(inputSwapped).GetBytes();
            hash0 = hashSHA256.ComputeBytes(hash0).GetBytes();
            hash0 = hashSHA512.ComputeBytes(hash0).GetBytes();

            byte[] hash1 = new byte[32];
            byte[] hash2 = new byte[32];
            Buffer.BlockCopy(hash0, 0, hash1, 0, 32);
            Buffer.BlockCopy(hash0, 32, hash2, 0, 32);
            hash1 = hashRIPEMD160.ComputeBytes(hash1).GetBytes();
            hash2 = hashRIPEMD160.ComputeBytes(hash2).GetBytes();
            hash0 = new byte[40];
            Buffer.BlockCopy(hash1, 0, hash0, 0, 20);
            Buffer.BlockCopy(hash2, 0, hash0, 20, 20);

            hash0 = hashSHA256.ComputeBytes(hash0).GetBytes();
            hash0 = hashSHA256.ComputeBytes(hash0).GetBytes();

            string hash0String = Utilities.ByteArrayToString(hash0);
            //Program.Logger("result: " + result);
            //Program.Logger("hash0:  " + hash0String);

            return result == hash0String;
        }

        public void Submit(Device aDevice, LbryStratum.Work work, UInt32 aNonce, string result)
        {
            if (Stopped)
                return;

            if (!VerifyShare(work, aNonce, result))
            {
                Program.Logger("Error in computation has been detected (Lbry).");
                return; // TODO
            }

            try  { mMutex.WaitOne(5000); } catch (Exception) { }

            ReportSubmittedShare(aDevice);
            try
            {
                String stringNonce = (String.Format("{3:x2}{2:x2}{1:x2}{0:x2}", ((aNonce >> 0) & 0xff), ((aNonce >> 8) & 0xff), ((aNonce >> 16) & 0xff), ((aNonce >> 24) & 0xff)));
                String message = JsonConvert.SerializeObject(new Dictionary<string, Object> {
                    { "id", mJsonRPCMessageID },
                    { "method", "mining.submit" },
                    { "params", new List<string> {
                        Username,
                        work.Job.ID,
                        work.LocalExtranonceString,
                        work.Job.NTime,
                        stringNonce
                }}});
                WriteLine(message);
                ++mJsonRPCMessageID;
            }
            catch (Exception ex) {
                Program.Logger("Failed to submit share: " + ex.Message + "\nReconnecting to the server...");
                Reconnect();
            }

            try  { mMutex.ReleaseMutex(); } catch (Exception) { }
        }

        public LbryStratum(String aServerAddress, int aServerPort, String aUsername, String aPassword, String aPoolName)
            : base(aServerAddress, aServerPort, aUsername, aPassword, aPoolName, "lbry")
        {
        }
    }
}
