using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{
    public class UnrecoverableException : Exception {
        public static bool IsUnrecoverableException(Exception ex) {
            if (ex.Message == "OpenCL error code detected: InvalidBufferSize."
                || ex.Message == "OpenCL error code detected: MemoryObjectAllocationFailure."
                || ex.Message == "OpenCL error code detected: OutOfHostMemory."
                || (new Regex(@"No such host is known")).Match(ex.Message).Success)
                return true;
            return false;
        }

        public UnrecoverableException(string s)
            : base(s == "No such host is known" ? "No such host is known." : s) {
        }

        public UnrecoverableException(Exception ex, Device device)
            : base(ex.Message == "OpenCL error code detected: InvalidBufferSize." ? "Not enough memory on Device #" + device.DeviceIndex + " (" + device.GetVendor() + " " + device.GetName() + ").\nIntensity may be too high." :
                   ex.Message == "OpenCL error code detected: MemoryObjectAllocationFailure." ? "Not enough memory on Device #" + device.DeviceIndex + " (" + device.GetVendor() + " " + device.GetName() + ").\nIntensity may be too high." :
                   ex.Message == "OpenCL error code detected: OutOfHostMemory." ? "Out of host memory." :
                                                                                    ex.Message) {
        }
    }

    public class StratumServerUnavailableException : UnrecoverableException {
        public StratumServerUnavailableException() : base("Stratum server unavailable.") { }
    }

    public class AuthorizationFailedException : UnrecoverableException {
        public AuthorizationFailedException() : base("Authorization failed.") { }
    }
}
