﻿#if ENABLE_UPNP

using System;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using Duality;

#if !__NOIPENDPOINT__
using NetEndPoint = System.Net.IPEndPoint;
#endif

namespace Lidgren.Network
{
    /// <summary>
    /// Status of the UPnP capabilities
    /// </summary>
    public enum UPnPStatus
    {
        /// <summary>
        /// Still discovering UPnP capabilities
        /// </summary>
        Discovering,

        /// <summary>
        /// UPnP is not available
        /// </summary>
        NotAvailable,

        /// <summary>
        /// UPnP is available and ready to use
        /// </summary>
        Available
    }

    /// <summary>
    /// UPnP support class
    /// </summary>
    public class NetUPnP
    {
        private struct DiscoveryResult
        {
            public NetEndPoint Sender;
            public string ServiceUrl;
            public string ServiceName;
        }

        private const int c_discoveryTimeOutMillis = 1000;

        private NetPeer m_peer;
        private ManualResetEvent m_discoveryComplete = new ManualResetEvent(false);

        internal double m_discoveryResponseDeadline;

        private UPnPStatus m_status;

        private RawList<DiscoveryResult> m_discoveryResults = new RawList<DiscoveryResult>();

        /// <summary>
        /// Status of the UPnP capabilities of this NetPeer
        /// </summary>
        public UPnPStatus Status { get { return m_status; } }

        /// <summary>
        /// NetUPnP constructor
        /// </summary>
        public NetUPnP(NetPeer peer)
        {
            m_peer = peer;
            m_discoveryResponseDeadline = double.MinValue;
        }

        internal void Discover(NetPeer peer)
        {
            string content =
                "M-SEARCH * HTTP/1.1\r\n" +
                "HOST: 239.255.255.250:1900\r\n" +
                "ST:upnp:rootdevice\r\n" +
                "MAN:\"ssdp:discover\"\r\n" +
                "MX:3\r\n\r\n";

            m_discoveryResponseDeadline = NetTime.Now + 6.0; // arbitrarily chosen number, router gets 6 seconds to respond
            m_status = UPnPStatus.Discovering;

            byte[] raw = System.Text.Encoding.UTF8.GetBytes(content);

            m_peer.LogDebug("Attempting UPnP discovery");
            peer.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

            if (peer.Configuration.BroadcastAddress.Equals(IPAddress.Broadcast)) {
                foreach (IPAddress address in NetUtility.GetBroadcastAddresses()) {
                    peer.RawSend(raw, 0, raw.Length, new NetEndPoint(address, 1900));
                }
            } else {
                peer.RawSend(raw, 0, raw.Length, new NetEndPoint(peer.Configuration.BroadcastAddress, 1900));
            }

            peer.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, false);
        }

        internal void CheckForDiscoveryTimeout()
        {
            if (m_status == UPnPStatus.NotAvailable || NetTime.Now < m_discoveryResponseDeadline) {
                return;
            }

            lock (m_discoveryResults) {
                m_status = (m_discoveryResults.Count > 0 ? UPnPStatus.Available : UPnPStatus.NotAvailable);
            }

            m_discoveryComplete.Set();
            m_peer.LogDebug("UPnP discovery timed out");
        }

        internal void ExtractServiceUrl(NetEndPoint sender, string resp)
        {
#if !DEBUG
            try
            {
#endif
                XmlDocument desc = new XmlDocument();
                using (var response = WebRequest.Create(resp).GetResponse())
                    desc.Load(response.GetResponseStream());

                XmlNamespaceManager nsMgr = new XmlNamespaceManager(desc.NameTable);
                nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                XmlNode typen = desc.SelectSingleNode("//tns:device/tns:deviceType/text()", nsMgr);
                if (!typen.Value.Contains("InternetGatewayDevice"))
                    return;

                string m_serviceName = "WANIPConnection";
                XmlNode node = desc.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:" + m_serviceName + ":1\"]/tns:controlURL/text()", nsMgr);
                if (node == null)
                {
                    // Try another service name
                    m_serviceName = "WANPPPConnection";
                    node = desc.SelectSingleNode("//tns:service[tns:serviceType=\"urn:schemas-upnp-org:service:" + m_serviceName + ":1\"]/tns:controlURL/text()", nsMgr);
                    if (node == null)
                        return;
                }

                lock (m_discoveryResults) {
                    m_discoveryResults.Add(new DiscoveryResult {
                        Sender = sender,
                        ServiceName = m_serviceName,
                        ServiceUrl = CombineUrls(resp, node.Value),
                    });
                }

                m_peer.LogDebug("UPnP service ready");
                //m_status = UPnPStatus.Available;
                //m_discoveryComplete.Set();
#if !DEBUG
            }
            catch
            {
                m_peer.LogVerbose("Exception ignored trying to parse UPnP XML response");
                return;
            }
#endif
        }

        private static string CombineUrls(string gatewayURL, string subURL)
        {
            // Is Control URL an absolute URL?
            if ((subURL.Contains("http:")) || (subURL.Contains(".")))
                return subURL;

            gatewayURL = gatewayURL.Replace("http://", "");  // strip any protocol
            int n = gatewayURL.IndexOf("/");
            if (n != -1)
                gatewayURL = gatewayURL.Substring(0, n);  // Use first portion of URL
            return "http://" + gatewayURL + subURL;
        }

        private bool CheckAvailability()
        {
            if (m_status == UPnPStatus.Discovering) {
                m_discoveryComplete.WaitOne(c_discoveryTimeOutMillis);

                lock (m_discoveryResults) {
                    if (m_discoveryResults.Count > 0) {
                        m_status = UPnPStatus.Available;
                    } else if (NetTime.Now > m_discoveryResponseDeadline) {
                        m_status = UPnPStatus.NotAvailable;
                    }
                }
            }

            return (m_status == UPnPStatus.Available);
        }

        /// <summary>
        /// Add a forwarding rule to the router using UPnP
        /// </summary>
        public bool ForwardPort(int port, string description)
        {
            if (!CheckAvailability()) {
                return false;
            }

            var client = NetUtility.GetSelfAddresses();
            if (client == null) {
                return false;
            }

            bool success = false;
            lock (m_discoveryResults) {
                for (int i = 0; i < m_discoveryResults.Count; i++) {
                    ref DiscoveryResult result = ref m_discoveryResults.Data[i];

                    try {
                        SOAPRequest(result.ServiceUrl,
                            "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:" + result.ServiceName + ":1\">" +
                            "<NewRemoteHost></NewRemoteHost>" +
                            "<NewExternalPort>" + port.ToString() + "</NewExternalPort>" +
                            "<NewProtocol>" + ProtocolType.Udp.ToString().ToUpper(System.Globalization.CultureInfo.InvariantCulture) + "</NewProtocol>" +
                            "<NewInternalPort>" + port.ToString() + "</NewInternalPort>" +
                            "<NewInternalClient>" + client.ToString() + "</NewInternalClient>" +
                            "<NewEnabled>1</NewEnabled>" +
                            "<NewPortMappingDescription>" + description + "</NewPortMappingDescription>" +
                            "<NewLeaseDuration>0</NewLeaseDuration>" +
                            "</u:AddPortMapping>",
                            "AddPortMapping", result.ServiceName);

                        success = true;
                        m_peer.LogDebug("Sent UPnP port forward request");
                    } catch (Exception ex) {
                        m_peer.LogVerbose("UPnP port forward failed: " + ex.Message);
                    }
                }
            }

            if (success) {
                NetUtility.Sleep(50);
            }

            return success;
        }

        /// <summary>
        /// Delete a forwarding rule from the router using UPnP
        /// </summary>
        public bool DeleteForwardingRule(int port)
        {
            if (!CheckAvailability())
                return false;

            bool success = false;
            lock (m_discoveryResults) {
                for (int i = 0; i < m_discoveryResults.Count; i++) {
                    ref DiscoveryResult result = ref m_discoveryResults.Data[i];

                    try {
                        SOAPRequest(result.ServiceUrl,
                            "<u:DeletePortMapping xmlns:u=\"urn:schemas-upnp-org:service:" + result.ServiceName + ":1\">" +
                            "<NewRemoteHost>" + "</NewRemoteHost>" +
                            "<NewExternalPort>" + port + "</NewExternalPort>" +
                            "<NewProtocol>" + ProtocolType.Udp.ToString().ToUpper(System.Globalization.CultureInfo.InvariantCulture) + "</NewProtocol>" +
                            "</u:DeletePortMapping>",
                            "DeletePortMapping", result.ServiceName);

                        success = true;
                    } catch (Exception ex) {
                        m_peer.LogVerbose("UPnP delete forwarding rule failed: " + ex.Message);
                    }
                }
            }
            return success;
        }

        /// <summary>
        /// Retrieve the extern ip using UPnP
        /// </summary>
        public IList<IPAddress> GetExternalIP()
        {
            if (!CheckAvailability()) {
                return null;
            }

            lock (m_discoveryResults) {
                List<IPAddress> addresses = new List<IPAddress>(m_discoveryResults.Count);

                for (int i = 0; i < m_discoveryResults.Count; i++) {
                    ref DiscoveryResult result = ref m_discoveryResults.Data[i];

                    try {
                        XmlDocument xdoc = SOAPRequest(result.ServiceUrl,
                            "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:" + result.ServiceName + ":1\">" +
                            "</u:GetExternalIPAddress>",
                            "GetExternalIPAddress", result.ServiceName);
                        XmlNamespaceManager nsMgr = new XmlNamespaceManager(xdoc.NameTable);
                        nsMgr.AddNamespace("tns", "urn:schemas-upnp-org:device-1-0");
                        string address = xdoc.SelectSingleNode("//NewExternalIPAddress/text()", nsMgr).Value;
                        addresses.Add(IPAddress.Parse(address));
                    } catch (Exception ex) {
                        m_peer.LogVerbose("Failed to get external IP for " + result.Sender + ": " + ex.Message);
                    }
                }

                return addresses;
            }
        }

        private XmlDocument SOAPRequest(string url, string soap, string function, string serviceName)
        {
            string req = "<?xml version=\"1.0\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<s:Body>" +
                soap +
                "</s:Body>" +
                "</s:Envelope>";

            WebRequest r = WebRequest.Create(url);
            r.Method = "POST";
            byte[] b = System.Text.Encoding.UTF8.GetBytes(req);
            r.Headers.Add("SOAPACTION", "\"urn:schemas-upnp-org:service:" + serviceName + ":1#" + function + "\""); 
            r.ContentType = "text/xml; charset=\"utf-8\"";
            r.ContentLength = b.Length;
            r.GetRequestStream().Write(b, 0, b.Length);
            using (WebResponse wres = r.GetResponse()) {
                XmlDocument resp = new XmlDocument();
                Stream ress = wres.GetResponseStream();
                resp.Load(ress);
                return resp;
            }
        }
    }
}

#endif