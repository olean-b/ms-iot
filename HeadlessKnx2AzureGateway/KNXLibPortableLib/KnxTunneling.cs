namespace KNXLibPortableLib
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Utils;

    using Sockets.Plugin;
    using Sockets.Plugin.Abstractions;

    public class KnxTunneling : KnxBase
    {
        private int m_timerPeriod = 60000;
        private byte m_rxSequenceNumber;
        private byte m_sequenceNumber;

        public byte ChannelId { get; private set; }
        public object SequenceNumberLock { get; private set; }
        private Timer StateRequestTimer { get; set; }
        private UdpSocketReceiver UdpClient { get; set; }

        public KnxTunneling(string remoteIpAddress, int remotePort, string localIpAddress, int localPort)
        {
            this.LocalEndpoint = new IPEndPoint(IPAddress.Parse(localIpAddress), localPort);
            this.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIpAddress), remotePort);
            this.ChannelId = 0x00;
            this.SequenceNumberLock = new object();
            this.StateRequestTimer = new Timer(OnStateRequest, null, Timeout.Infinite, this.m_timerPeriod);
        }

        public async void Connect()
        {
            try
            {
                if (this.UdpClient != null)
                {
                    try
                    {
                        await this.UdpClient.StopListeningAsync();
                        this.UdpClient.Dispose();
                    }
                    catch(Exception ex)
                    {
                        if (this.IsDebug)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                        }
                    }
                }

                this.UdpClient = new UdpSocketReceiver();
                this.UdpClient.MessageReceived += OnMessageReceived;
            }
            catch (SocketException ex)
            {
                if (this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    return;
                }
            }

            await this.UdpClient.StartListeningAsync(this.LocalEndpoint.Port);

            try
            {
                this.ConnectRequest();
            }
            catch(Exception ex)
            {
                if (this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private void OnMessageReceived(object sender, UdpSocketMessageReceivedEventArgs e)
        {
            try
            {
                switch (Networking.GetServiceType(e.ByteData))
                {
                    case ServiceType.ConnectionResponse:
                        this.ProcessConnectResponse(e.ByteData);
                        break;
                    case ServiceType.ConnectionStateResponse:
                        this.ProcessConnectionStateResponse(e.ByteData);
                        break;
                    case ServiceType.TunnelingAck:
                        this.ProcessTunnelingAck(e.ByteData);
                        break;
                    case ServiceType.DisconnectRequest:
                        this.ProcessDisconnectRequest(e.ByteData);
                        break;
                    case ServiceType.TunnelingRequest:
                        this.ProcessDatagramHeaders(e.ByteData);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private async void OnStateRequest(object state)
        {
            var datagram = DatagramProcessing.CreateStateRequest(this.LocalEndpoint, this.ChannelId);

            try
            {
                await this.SendData(datagram);
            }
            catch(Exception ex)
            {
                if(this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private async void ConnectRequest()
        {
            var datagram = DatagramProcessing.CreateConnectRequest(this.LocalEndpoint);

            await this.SendData(datagram);
        }

        private async void DisconnectRequest()
        {
            var datagram = DatagramProcessing.CreateDisconnectRequest(this.LocalEndpoint, this.ChannelId);

            await this.SendData(datagram);
        }

        public async Task<bool> SendData(byte[] datagram)
        {
            try
            {
                await this.UdpClient.SendToAsync(datagram, this.RemoteEndPoint.Address.ToString(), this.RemoteEndPoint.Port);
                return true;
            }
            catch(Exception ex)
            {
                if(this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
                return false;
            }
        }

        public async void SendTunnelingAck(byte sequenceNumber)
        {
            // HEADER
            var datagram = DatagramProcessing.CreateTunnelingAck(sequenceNumber, this.ChannelId); 

            await this.SendData(datagram);
        }

        public void Disconnect()
        {
            try
            {
                this.TerminateStateRequest();
                this.DisconnectRequest();
            }
            catch(Exception ex)
            {
                if (this.IsDebug)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }            
        }

        private void InitializeStateRequest()
        {
            this.StateRequestTimer.Change(0, this.m_timerPeriod);
        }

        private void TerminateStateRequest()
        {
            this.StateRequestTimer.Change(Timeout.Infinite, this.m_timerPeriod);
        }

        ~KnxTunneling()
        {
            this.StateRequestTimer.Dispose();
        }

        private void ProcessDatagramHeaders(byte[] datagram)
        {
            // HEADER
            // TODO: Might be interesting to take out these magic numbers for the datagram indices
            var knxDatagram = new KnxDatagram
            {
                HeaderLength = datagram[0],
                ProtocolVersion = datagram[1],
                ServiceType = new[] { datagram[2], datagram[3] },
                TotalLength = datagram[4] + datagram[5]
            };

            var channelId = datagram[7];
            if (channelId != this.ChannelId)
                return;

            var sequenceNumber = datagram[8];
            var process = true;
            lock (this.SequenceNumberLock)
            {
                if (sequenceNumber <= this.m_rxSequenceNumber)
                    process = false;

                this.m_rxSequenceNumber = sequenceNumber;
            }

            if (process)
            {
                // TODO: Magic number 10, what is it?
                var cemi = new byte[datagram.Length - 10];
                Array.Copy(datagram, 10, cemi, 0, datagram.Length - 10);

                switch(DatagramProcessing.ProcessCEMI(ref knxDatagram, cemi, this.ThreeLevelGroupAddressing, this.IsDebug))
                {
                    case ResponseType.Event:
                        base.EventReceived(knxDatagram.DestinationAddress, knxDatagram.Data);
                        break;
                    case ResponseType.Status:
                        base.StatusReceived(knxDatagram.DestinationAddress, knxDatagram.Data);
                        break;
                }
            }

            this.SendTunnelingAck(sequenceNumber);
        }

        private async void ProcessDisconnectRequest(byte[] datagram)
        {
            var channelId = datagram[6];
            if (channelId != this.ChannelId)
                return;

            await this.UdpClient.StopListeningAsync();
            this.UdpClient.Dispose();

            base.Disconnected();
        }

        private void ProcessTunnelingAck(byte[] datagram)
        {
            // do nothing
        }

        private void ProcessConnectionStateResponse(byte[] datagram)
        {
            // HEADER
            // 06 10 02 08 00 08 -- 48 21
            var knxDatagram = new KnxDatagram
            {
                HeaderLength = datagram[0],
                ProtocolVersion = datagram[1],
                ServiceType = new[] { datagram[2], datagram[3] },
                TotalLength = datagram[4] + datagram[5],
                ChannelID = datagram[6]
            };

            var response = datagram[7];

            if (response != 0x21)
                return;

            if (this.IsDebug)
                System.Diagnostics.Debug.WriteLine("KnxReceiverTunneling: Received connection state response - No active connection with channel ID {0}", knxDatagram.ChannelID);

            this.Disconnect();
        }

        private void ProcessConnectResponse(byte[] datagram)
        {
            // HEADER
            var knxDatagram = new KnxDatagram
            {
                HeaderLength = datagram[0],
                ProtocolVersion = datagram[1],
                ServiceType = new[] { datagram[2], datagram[3] },
                TotalLength = datagram[4] + datagram[5],
                ChannelID = datagram[6],
                Status = datagram[7]
            };

            if (knxDatagram.ChannelID == 0x00 && knxDatagram.Status == 0x24)
            {
                if (this.IsDebug)
                    System.Diagnostics.Debug.WriteLine("KnxReceiverTunneling: Received connect response - No more connections available");
            }
            else
            {
                this.ChannelId = knxDatagram.ChannelID;
                this.ResetSequenceNumber();

                this.InitializeStateRequest();
                base.Connected();
            }
        }

        public byte GenerateSequenceNumber()
        {
            return this.m_sequenceNumber++;
        }

        public void RevertSingleSequenceNumber()
        {
            this.m_sequenceNumber--;
        }

        public void ResetSequenceNumber()
        {
            this.m_sequenceNumber = 0x00;
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
            lock (this.SequenceNumberLock)
            {
                byte newSequenceNumber = this.GenerateSequenceNumber();
                try
                {
                    var dataLength = DataProcessing.GetDataLength(data);

                    // HEADER
                    var datagram = new byte[10];
                    datagram[00] = 0x06;
                    datagram[01] = 0x10;
                    datagram[02] = 0x04;
                    datagram[03] = 0x20;

                    var totalLength = BitConverter.GetBytes(dataLength + 20);
                    datagram[04] = totalLength[1];
                    datagram[05] = totalLength[0];

                    datagram[06] = 0x04;
                    datagram[07] = this.ChannelId;
                    datagram[08] = newSequenceNumber;
                    datagram[09] = 0x00;

                    return DatagramProcessing.CreateActionDatagramCommon(destinationAddress, data, datagram, this.ActionMessageCode);
                }
                catch
                {
                    this.RevertSingleSequenceNumber();

                    return null;
                }
            }
        }

        private byte[] CreateRequestStatusDatagram(string destinationAddress)
        {
            lock (this.SequenceNumberLock)
            {
                byte newSequenceNumber = this.GenerateSequenceNumber();
                try
                {
                    // HEADER
                    var datagram = new byte[21];
                    datagram[00] = 0x06;
                    datagram[01] = 0x10;
                    datagram[02] = 0x04;
                    datagram[03] = 0x20;
                    datagram[04] = 0x00;
                    datagram[05] = 0x15;

                    datagram[06] = 0x04;
                    datagram[07] = this.ChannelId;
                    datagram[08] = newSequenceNumber;
                    datagram[09] = 0x00;

                    return DatagramProcessing.CreateRequestStatusDatagramCommon(destinationAddress, datagram, 10, this.ActionMessageCode);
                }
                catch
                {
                    this.RevertSingleSequenceNumber();

                    return null;
                }
            }
        }
    }
}
