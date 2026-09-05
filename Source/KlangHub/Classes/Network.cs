using KlangHub.Core.Models;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace KlangHub.Classes
{
    public static class Network
    {
        /// <summary>
        /// Return all IP4 addresses that are in use on the system.
        /// </summary>
        /// <returns></returns>
        public static List<NetworkAdapter> GetIp4ddresses(bool all = true)
        {
            var adapters = new List<NetworkAdapter>();

            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in networkInterfaces)
            {
                var properties = networkInterface.GetIPProperties();
                if (properties != null)
                {
                    foreach (var ip in properties.UnicastAddresses)
                    {
                        if (ip != null && ip.Address != null)
                        {
                            var address = ip.Address;
                            if ((all && 
                                    networkInterface.OperationalStatus == OperationalStatus.Up && 
                                    address.AddressFamily == AddressFamily.InterNetwork)
                                || (address.AddressFamily == AddressFamily.InterNetwork
                                    && IsInLocalIpRange(address)
                                    && networkInterface.OperationalStatus != OperationalStatus.Down
                                    && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                    && properties.GatewayAddresses.Count > 0))
                            {
                                adapters.Add(
                                    new NetworkAdapter
                                    {
                                        IPAddress = address,
                                        IsEthernet = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit ||
                                                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                                                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
                                                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT
                                    });
                            }
                        }
                    }
                }
            }

            return adapters;
        }

        /// <summary>
        /// Check if the IP address is in the local ip ranges.
        /// </summary>
        /// <param name="address">IP address</param>
        /// <returns>true if it's a local IP address, or false</returns>
        public static bool IsInLocalIpRange(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            // Compared as numbers, not as text. The middle RFC 1918 block is 172.16.0.0/12 - the first
            // sixteen of the 172 networks, not all 256 - and the prefix test this replaces accepted the
            // rest of them too, 172.217 among them, which is ordinary public space that is very much in
            // use. An adapter on such an address would have been offered to the speakers as local.
            var octets = address.GetAddressBytes();

            return (octets[0] == 192 && octets[1] == 168)
                || octets[0] == 10
                || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31);
        }

        /// <summary>
        /// Return the local IP address. 
        /// If there are multiple IP addresses the first ethernet address is returned.
        /// </summary>
        /// <returns>the IP address, or null if there aren't any</returns>
        public static IPAddress? GetIp4Address()
        {
            var addressesInUse = GetIp4ddresses(false);
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            if (addressesInUse.Count == 0 || ipHostInfo.AddressList.Count() == 0)
                return null;

            var ipAddress = addressesInUse.First().IPAddress;
            var addressFound = false;
            foreach (var address in ipHostInfo.AddressList)
            {
                var adapter = addressesInUse.Where(x => x.IPAddress!.ToString() == address.ToString()).FirstOrDefault();
                if (adapter != null && 
                    !address.IsIPv4MappedToIPv6 && 
                    !address.IsIPv6LinkLocal && 
                    !address.IsIPv6Multicast && 
                    !address.IsIPv6SiteLocal && 
                    !address.IsIPv6Teredo)
                {
                    if (!addressFound || adapter.IsEthernet)
                    {
                        addressFound = true;
                        ipAddress = address;
                    }
                }
            }
            return ipAddress;
        }
    }
}
