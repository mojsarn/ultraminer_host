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
    class CryptoNightStratum : Stratum
    {
        public new class Work : Stratum.Work
        {
            readonly private Job mJob;

            public new Job GetJob() { return mJob; }

            public Work(Job aJob)
                : base(aJob)
            {
                mJob = aJob;
            }
        }

        public new class Job : Stratum.Job
        {
            readonly String mID;
            readonly String mBlob;
            readonly String mTarget;

            public String ID { get { return mID; } }
            public String Blob { get { return mBlob; } }
            public String Target { get { return mTarget; } }

            public Job(Stratum aStratum, string aID, string aBlob, string aTarget)
                : base(aStratum)
            {
                mID = aID;
                mBlob = aBlob;
                mTarget = aTarget;
            }

            public bool Equals(Job aJob) {
                return aJob != null
                    && mID == aJob.mID
                    && mBlob == aJob.mBlob
                    && mTarget == aJob.mTarget;
            }
        }

        String mUserID;
        Job mJob;
        private Mutex mMutex = new Mutex();

        public Job GetJob()
        {
            return mJob;
        }

        protected override void ProcessLine(String line)
        {
            Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(line);
            if (response.ContainsKey("method") && response.ContainsKey("params"))
            {
                string method = (string)response["method"];
                JContainer parameters = (JContainer)response["params"];
                if (method.Equals("job"))
                {
                    try  {  mMutex.WaitOne(5000); } catch (Exception) { }
                    mJob = new Job(this, (string)parameters["job_id"], (string)parameters["blob"], (string)parameters["target"]);
                    try  {  mMutex.ReleaseMutex(); } catch (Exception) { }
                    if (!SilentMode) Program.Logger("Received new job: " + parameters["job_id"]);
                }
                else
                {
                    Program.Logger("Unknown stratum method: " + line);
                }
            }
            else if (response.ContainsKey("id") && response.ContainsKey("error"))
            {
                var ID = response["id"];
                var error = response["error"];

                if (error == null) {
                    ReportAcceptedShare();
                } else if (error != null) {
                    ReportRejectedShare((String)(((JContainer)response["error"])["message"]));
                }
            }
            else
            {
                Program.Logger("Unknown JSON message: " + line);
            }
        }

        override protected void Authorize()
        {
            var line = Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "method", "login" },
                { "params", new Dictionary<string, string> {
                    { "login", Username },
                    { "pass", "x" },
                    { "agent", Program.appName + "/" + Program.appVersion}}},
                { "id", 1 }
            });
            WriteLine(line);

            if ((line = ReadLine()) == null)
                throw new Exception("Disconnected from stratum server.");
            JContainer result;
            Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(line);
            if (response.ContainsKey("error") && response["error"] != null)
                throw new UnrecoverableException((String)(((JContainer)response["error"])["message"]));
            result = ((JContainer)response["result"]);
            var status = (String)(result["status"]);
            if (status != "OK")
                throw new AuthorizationFailedException();

            try  {  mMutex.WaitOne(5000); } catch (Exception) { }
            mUserID = (String)(result["id"]);
            mJob = new Job(this, (String)(((JContainer)result["job"])["job_id"]), (String)(((JContainer)result["job"])["blob"]), (String)(((JContainer)result["job"])["target"]));
            try  {  mMutex.ReleaseMutex(); } catch (Exception) { }
        }

        public void Submit(Device device, Job job, UInt32 output, String result)
        {
            if (Stopped)
                return;

            try  {  mMutex.WaitOne(5000); } catch (Exception) { }
            ReportSubmittedShare(device);
            try
            {
                String stringNonce = String.Format("{0:x2}{1:x2}{2:x2}{3:x2}", ((output >> 0) & 0xff), ((output >> 8) & 0xff), ((output >> 16) & 0xff), ((output >> 24) & 0xff));
                String message = JsonConvert.SerializeObject(new Dictionary<string, Object> {
                    { "method", "submit" },
                    { "params", new Dictionary<String, String> {
                        { "id", mUserID },
                        { "job_id", job.ID },
                        { "nonce", stringNonce },
                        { "result", result }}},
                    { "id", 4 }});
                WriteLine(message);
                Program.Logger("Device #" + device.DeviceIndex + " submitted a share to " + ServerAddress + " as " + (Utilities.IsDevFeeAddress(Username) ? "a DEVFEE" : Username) + ".");
            }
            catch (Exception ex)
            {
                Program.Logger("Failed to submit share: " + ex.Message + "\nReconnecting to the server...");
                Reconnect();
            }
            try  {  mMutex.ReleaseMutex(); } catch (Exception) { }
        }

        public new Work GetWork()
        {
            return new Work(mJob);
        }

        public CryptoNightStratum(String aServerAddress, int aServerPort, String aUsername, String aPassword, String aPoolName)
            : base(aServerAddress, aServerPort, aUsername, aPassword, aPoolName, "cryptonight")
        {
        }
    }
}
