using System;
using System.Net;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri
{

	using Node = com.iota.iri.service.Node;


	public class Neighbor
	{

		private readonly SocketAddress address;

        private int numberOfAllTransactions;
        private int numberOfNewTransactions;
        private int numberOfInvalidTransactions;


		public Neighbor(SocketAddress address)
		{
			this.address = address;
		}


		public virtual void send(DatagramPacket packet)
		{

			try
			{
                IPEndPoint endpoint = new IPEndPoint(0, 0);
                IPEndPoint clonedIPEndPoint = (IPEndPoint)endpoint.Create(address);

                // Node.socket.Send((byte[])(Array)packet.getData(), packet.getLength(), clonedIPEndPoint);

                Node.instance().send((byte[])(Array)packet.getData(), packet.getLength(), clonedIPEndPoint);



				// packet.setSocketAddress(address);
				// Node.socket.send(packet);

                // ??? Node.socket.Send((byte[])(Array)Converter.sbytes(claimPacket), Converter.sizeInBytes(claimPacket.Length), ipEndPoint);
			}

			catch (Exception e)
			{
			// ignore
			}
		}


		public override bool Equals(object obj)
		{
            if (this == obj)
            {
                return true;
            }
            if ((obj == null) || (obj.getClass() != this.getClass()))
            {
                return false;
            }

			return address.Equals(((Neighbor)obj).address);
		}

		public override int GetHashCode()
		{
			return address.GetHashCode();
		}

		public virtual SocketAddress Address
		{
			get
			{
				return address;
			}
		}

        public void incAllTransactions()
        {
            numberOfAllTransactions++;
        }

        public void incNewTransactions()
        {
            numberOfNewTransactions++;
        }

        public void incInvalidTransactions()
        {
            numberOfInvalidTransactions++;
        }

		public virtual int NumberOfAllTransactions
		{
			get
			{
				return numberOfAllTransactions;
			}
		}

		public virtual int NumberOfInvalidTransactions
		{
			get
			{
				return numberOfInvalidTransactions;
			}
		}

		public virtual int NumberOfNewTransactions
		{
			get
			{
				return numberOfNewTransactions;
			}
		}
	}

}