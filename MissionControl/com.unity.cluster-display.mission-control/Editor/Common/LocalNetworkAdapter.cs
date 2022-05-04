using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Class used to list and select a network adapter on the local computer that can be used for communication with
    /// nodes.
    /// </summary>
    class LocalNetworkAdapter
    {
        List<UnicastIPAddressInformation> m_IpAddresses;

        /// <summary>
        /// Name of the LocalNetworkAdapter matching OS adapter name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Name of the LocalNetworkAdapter used to display this adapter in list of choices, messages, ...
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Find the preferred local address to use to reach given remote address.
        /// </summary>
        /// <param name="remoteEndPoint">Remote endpoint we are trying to communicate with.</param>
        /// <returns>The preferred local address.</returns>
        public IPAddress FindLocalAddressFor(IPEndPoint remoteEndPoint)
        {
            // Search among the addresses associated to this local network adapter for one of the same family as the
            // remote address we are trying to reach.
            if (m_IpAddresses != null && m_IpAddresses.Any())
            {
                var foundAddress = m_IpAddresses.FirstOrDefault(
                    a => a.Address.AddressFamily == remoteEndPoint.Address.AddressFamily);

                // If no address of the same family can be found, let's use the first one in the list and hope for some
                // network magic to translate them from one family to the other.
                foundAddress ??= m_IpAddresses.First();

                return foundAddress.Address;
            }

            // No address specified for the adapter, must be a "placeholder" adapter meaning all adapters, so use the
            // global helper in NetworkUtilities to search among all the network adapters the IP address to use that has
            // the most chances to be able to get to remoteAddress.
            return NetworkUtilities.GetRoutingInterface(remoteEndPoint);
        }

        /// <summary>
        /// Builds a list of <see cref="LocalNetworkAdapter"/> that contains an entry for each functional network
        /// adapter of the local computer.
        /// </summary>
        /// <param name="includeAutomatic">Do we include an entry in the list for automatic selection of the network
        /// adapter?</param>
        /// <param name="hasToBePresent">Name of a <see cref="LocalNetworkAdapter"/> that has to be included in the
        /// list. If it is not present then add a fictive entry associate to the automatic IP address selection.</param>
        /// <returns>The list</returns>
        public static List<LocalNetworkAdapter> BuildList(bool includeAutomatic = false, string hasToBePresent = "")
        {
            var list = new List<LocalNetworkAdapter>();

            if (includeAutomatic)
            {
                list.Add(CreateAutomatic());
            }

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters.OrderBy(a => a.Name))
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    var adapterAddresses = FilterIPAddresses(adapter.GetIPProperties().UnicastAddresses);
                    if (adapterAddresses.Any())
                    {
                        list.Add(new LocalNetworkAdapter(adapter, adapterAddresses));
                    }
                }
            }

            if (!string.IsNullOrEmpty(hasToBePresent))
            {
                if (FindWithNameInList(list, hasToBePresent) == null)
                {
                    list.Add(CreateMissing(hasToBePresent));
                }
            }

            return list;
        }

        /// <summary>
        /// Create a new <see cref="LocalNetworkAdapter"/> for the adapter with the given name.
        /// </summary>
        /// <param name="name">Name of the network interface in the OS</param>
        /// <returns>Corresponding <see cref="LocalNetworkAdapter"/>, cannot be null, a new fictive
        /// <see cref="LocalNetworkAdapter"/> (with the same IP as if (automatic) had been chosen) will be created and
        /// returned if the name cannot be found.</returns>
        public static LocalNetworkAdapter CreateForName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return CreateAutomatic();
            }

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.Name == name && adapter.OperationalStatus == OperationalStatus.Up)
                {
                    var adapterAddresses = FilterIPAddresses(adapter.GetIPProperties().UnicastAddresses);
                    if (adapterAddresses.Any())
                    {
                        return new LocalNetworkAdapter(adapter, adapterAddresses);
                    }
                }
            }

            // If we reach this point it is because no adapter with this name exist or it is not connected, fallback
            // on the missing placeholder.
            return CreateMissing(name);
        }

        /// <summary>
        /// Search the given collection of <see cref="LocalNetworkAdapter"/> for one with the given name and returns it.
        /// </summary>
        /// <param name="list">Collection of <see cref="LocalNetworkAdapter"/>.</param>
        /// <param name="name">Name of the search <see cref="LocalNetworkAdapter"/>.</param>
        /// <returns>The found <see cref="LocalNetworkAdapter"/> or null if nothing is found.</returns>
        public static LocalNetworkAdapter FindWithNameInList(List<LocalNetworkAdapter> list, string name)
        {
            foreach (var localNetworkAdapter in list)
            {
                if (localNetworkAdapter.Name == name)
                {
                    return localNetworkAdapter;
                }
            }

            return null;
        }

        /// <summary>
        /// Search the given collection of <see cref="LocalNetworkAdapter"/> for the index of a
        /// <see cref="LocalNetworkAdapter"/> with the given name.
        /// </summary>
        /// <param name="list">Collection of <see cref="LocalNetworkAdapter"/>.</param>
        /// <param name="name">Name of the search <see cref="LocalNetworkAdapter"/>.</param>
        /// <returns>Index of the <see cref="LocalNetworkAdapter"/> or -1 if not found.</returns>
        public static int IndexOfInList(List<LocalNetworkAdapter> list, string name)
        {
            int index = 0;
            foreach (var localNetworkAdapter in list)
            {
                if (localNetworkAdapter.Name == name)
                {
                    return index;
                }

                ++index;
            }

            return -1;
        }

        /// <summary>
        /// Constructor from a real NetworkInterface for this computer
        /// </summary>
        /// <param name="networkInterface"><see cref="NetworkInterface"/> from which we create this LocalNetworkAdapter.</param>
        /// <param name="addresses">List of ip addresses of the network adapter.</param>
        LocalNetworkAdapter(NetworkInterface networkInterface, List<UnicastIPAddressInformation> addresses)
        {
            System.Diagnostics.Debug.Assert(addresses.Any());

            Name = networkInterface.Name;

            var displayNameBuilder = new StringBuilder();
            displayNameBuilder.Append(networkInterface.Name);
            displayNameBuilder.Append(" (");
            displayNameBuilder.Append(addresses.First().Address);
            foreach (var address in addresses.Skip(1))
            {
                displayNameBuilder.Append(", ");
                displayNameBuilder.Append(address.Address);
            }
            displayNameBuilder.Append(')');
            DisplayName = displayNameBuilder.ToString();

            m_IpAddresses = addresses;
        }

        /// <summary>
        /// Constructor of a fictive <see cref="LocalNetworkAdapter"/>, mostly used to represent special choices when
        /// giving a choice to the user.
        /// </summary>
        /// <param name="name"><see cref="Name"/></param>
        /// <param name="displayName"><see cref="DisplayName"/></param>
        LocalNetworkAdapter(string name, string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }

        /// <summary>
        /// Filter a <see cref="UnicastIPAddressInformationCollection"/> to only keep IP addresses (v4 or v6).
        /// </summary>
        /// <param name="collection">IPAddresses to filter.</param>
        /// <returns>Filtered collection.</returns>
        private static List<UnicastIPAddressInformation> FilterIPAddresses(
            UnicastIPAddressInformationCollection collection)
        {
            var filtered = new List<UnicastIPAddressInformation>();
            foreach (var currentAddress in collection)
            {
                if (currentAddress.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                {
                    filtered.Add(currentAddress);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Create a <see cref="LocalNetworkAdapter"/> representing the automatic choice of network adapter.
        /// </summary>
        /// <returns>The new <see cref="LocalNetworkAdapter"/>.</returns>
        private static LocalNetworkAdapter CreateAutomatic() => new LocalNetworkAdapter(string.Empty, "(automatic)");

        /// <summary>
        /// Create a <see cref="LocalNetworkAdapter"/> representing a missing / disconnected network adapter.
        /// </summary>
        /// <returns>The new <see cref="LocalNetworkAdapter"/>.</returns>
        private static LocalNetworkAdapter CreateMissing(string name) => new LocalNetworkAdapter(name,
            name + " (missing -> automatic)");
    }
}
