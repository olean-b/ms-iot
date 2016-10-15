namespace KNXLibPortableLib
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using KNXLibPortableLib.Utils;

    using Sockets.Plugin;
    using Sockets.Plugin.Abstractions;

    public class KnxRouting : KnxBase
    {
        public const string DefaultMulticastAddress = "224.0.23.12";
        private const int DefaultMulticastPort = 3671;

        /// <summary>
        /// Get addresses from the method 
        /// public IEnumerable<IPAddress> GetIPAddress()
        ///    {
        ///        foreach (var Host in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
        ///        {
        ///            string IP = Host.DisplayName;
        ///            yield return IPAddress.Parse(IP);
        ///         }
        ///     }
        /// </summary>
        public IEnumerable<IPAddress> IpV4Addresses { get; private set; }
        private List<UdpSocketMulticastClient> UdpClients { get; set; }

        public KnxRouting(IEnumerable<IPAddress> ipv4addresses)
            : this(DefaultMulticastAddress, DefaultMulticastPort, ipv4addresses)
        {

        }

        public KnxRouting(string multicastAddress, int multicastPort, IEnumerable<IPAddress> ipv4addresses)
        {
            this.UdpClients = new List<UdpSocketMulticastClient>();
            LocalEndpoint = new IPEndPoint(IPAddress.Any, multicastPort);
            RemoteEndPoint = Networking.CreateRemoteEndpoint(multicastAddress, multicastPort);
            this.IpV4Addresses = ipv4addresses;
        }

        public async void Connect()
        {
            try
            {
                foreach (IPAddress localIp in this.IpV4Addresses)
                {
                    var client = new UdpSocketMulticastClient(); //UdpClient(new IPEndPoint(localIp, _localEndpoint.Port));
                    this.UdpClients.Add(client);
                    client.MessageReceived += this.OnMessageReceived;
                    await client.JoinMulticastGroupAsync(RemoteEndPoint.Address.ToString(), RemoteEndPoint.Port); //.JoinMulticastGroup(ConnectionConfiguration.IpAddress, localIp);
                }
            }
            catch (SocketException ex)
            {
                if (IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            base.Connected();
        }

        private void OnMessageReceived(object sender, UdpSocketMessageReceivedEventArgs e)
        {
            try
            {
                KnxDatagram knxDatagram;
                switch (DatagramProcessing.ProcessDatagram(e.ByteData, ThreeLevelGroupAddressing, IsDebug, out knxDatagram))
                {
                    case ResponseType.Event:
                        base.EventReceived(knxDatagram.DestinationAddress, knxDatagram.Data);
                        break;
                    case ResponseType.Status:
                        base.StatusReceived(knxDatagram.DestinationAddress, knxDatagram.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        public async Task<bool> SendData(byte[] datagram)
        {
            try
            {
                foreach (var client in this.UdpClients)
                    await client.SendMulticastAsync(datagram);
            }
            catch (Exception ex)
            {
                if (IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        public async void Disconnect()
        {
            foreach (var client in this.UdpClients)
            {
                await client.DisconnectAsync();
                client.Dispose();
            }

            base.Disconnected();
        }

        protected async override void SendAction(string destinationAddress, byte[] data)
        {
            await this.SendData(this.CreateActionDatagram(destinationAddress, data));
        }

        protected async override void SendRequestStatus(string destinationAddress)
        {
            await this.SendData(this.CreateRequestStatusDatagram(destinationAddress));
        }

        private byte[] CreateActionDatagram(string destinationAddress, byte[] data)
        {
            var dataLength = DataProcessing.GetDataLength(data);

            // HEADER
            var datagram = new byte[6];
            datagram[0] = 0x06;
            datagram[1] = 0x10;
            datagram[2] = 0x05;
            datagram[3] = 0x30;
            var totalLength = BitConverter.GetBytes(dataLength + 16);
            datagram[4] = totalLength[1];
            datagram[5] = totalLength[0];

            return DatagramProcessing.CreateActionDatagramCommon(destinationAddress, data, datagram, ActionMessageCode);
        }

        private byte[] CreateRequestStatusDatagram(string destinationAddress)
        {
            // TODO: Test this

            // HEADER
            var datagram = new byte[17];
            datagram[00] = 0x06;
            datagram[01] = 0x10;
            datagram[02] = 0x05;
            datagram[03] = 0x30;
            datagram[04] = 0x00;
            datagram[05] = 0x11;

            return DatagramProcessing.CreateRequestStatusDatagramCommon(destinationAddress, datagram, 6, ActionMessageCode);
        }
    }
}
