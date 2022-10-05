using System;
using System.Net.NetworkInformation;
using System.Net;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Various helpers to help manipulation and validation of endpoints.
    /// </summary>
    public static class EndPointHelpers
    {
        /// <summary>
        /// Try to validate (not bullet proof but not bad either) if the given end point appear to be a valid endpoint
        /// a REST service could listen to (on the local computer).
        /// </summary>
        /// <param name="endpoint">End point to validate</param>
        /// <returns>An empty string indicate endpoint is valid, anything else indicate it is not and gives information
        /// on what in the endpoint is not valid.</returns>
        public static string ValidateLocal(string endpoint)
        {
            try
            {
                // Unfortunately Uri does not support parsing uri like : http://*:8100, so we have to check it ourselves.
                string effectiveEndpoint = endpoint;
                if (endpoint.ToLower().StartsWith("http://*"))
                {
                    effectiveEndpoint = "http://0.0.0.0" + endpoint.Substring(8);
                }

                // Parse the endpoint using .Net classes
                var uri = new Uri(effectiveEndpoint);
                if (uri.Scheme.ToLower() != "http")
                {
                    return $"{endpoint} does not start with http://.";
                }
                if (uri.IsLoopback)
                {
                    return "";
                }
                if (!IPAddress.TryParse(uri.Host, out var parsedAddress))
                {
                    return $"Failed to parse {uri.Host} to an IP address.";
                }
                if (parsedAddress.Equals(IPAddress.Any) || parsedAddress.Equals(IPAddress.IPv6Any) ||
                    parsedAddress.Equals(IPAddress.Loopback) || parsedAddress.Equals(IPAddress.IPv6Loopback))
                {
                    return "";
                }

                // Try to see if it is one of our local addresses
                bool found = false;
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (item.OperationalStatus == OperationalStatus.Up)
                    {
                        IPInterfaceProperties adapterProperties = item.GetIPProperties();
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {
                            if (ip.Address.Equals(parsedAddress))
                            {
                                found = true;
                                break;  // break the loop!!
                            }
                        }
                    }
                    if (found) { break; }
                }
                if (!found)
                {
                    return $"{uri.Host} does not refer to a local IP address.";
                }

                // Success
                return "";
            }
            catch (Exception e)
            {
                return $"Error parsing {endpoint}: {e}";
            }
        }
    }
}
