using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using iri.utils;

namespace com.iota.iri.service
{

	using Milestone = com.iota.iri.Milestone;
	using Neighbor = com.iota.iri.Neighbor;
	using Curl = com.iota.iri.hash.Curl;
	using Transaction = com.iota.iri.model.Transaction;


	public class Node
	{

		private const int TRANSACTION_PACKET_SIZE = 1650;

		private const int QUEUE_SIZE = 1000;
		private const int PAUSE_BETWEEN_TRANSACTIONS = 1;

		private const int REQUESTED_TRANSACTION_OFFSET = Transaction.SIZE;

        public static UdpClient socket;
		private static bool shuttingDown;

		public static readonly CopyOnWriteArrayList<Neighbor> neighbors = new CopyOnWriteArrayList<Neighbor>();

        public static readonly ConcurrentOrderedList<Transaction> queuedTransactions = new ConcurrentOrderedList<Transaction>(
		    new FuncComparer<Transaction>((transaction1, transaction2) =>
		    {
		        if (transaction1.weightMagnitude == transaction2.weightMagnitude)
		        {
		            for (int i = 0; i < Transaction.HASH_SIZE; i++)
		            {
		                if (transaction1.hash[i] != transaction2.hash[i])
		                {
		                    return transaction2.hash[i] - transaction1.hash[i];
		                }
		            }

		            return 0;
		        }
		        else
		        {
		            return transaction2.weightMagnitude - transaction1.weightMagnitude; 	            
		        }
		    }));

		private static readonly DatagramPacket receivingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);
		private static readonly DatagramPacket sendingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);
		private static readonly DatagramPacket tipRequestingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);


		public static void launch(string[] args)
		{
		    try
		    {
                socket = new UdpClient(Convert.ToInt32(args[0]));

		        foreach (string arg in args)
		        {

		            Uri uri = new Uri(arg);

		            if (uri.Scheme != null && uri.Scheme.Equals("udp"))
		            {
		                var ipEndPoint = new IPEndPoint(Dns.GetHostEntry(uri.Host).AddressList[0], uri.Port);
		                SocketAddress socketAddress = ipEndPoint.Serialize();
		                neighbors.Add(new Neighbor(socketAddress));
		            }
		        }

		        (new Thread(
		            () =>
		            {
		                int[] receivedTransactionTrits = new int[Transaction.TRINARY_SIZE];
		                Curl curl = new Curl();
		                sbyte[] requestedTransaction = new sbyte[Transaction.HASH_SIZE];

		                while (!shuttingDown)
		                {
		                    try
		                    {
                                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                var receivedResults = (sbyte[])(Array)socket.Receive(ref remoteEndPoint);
      
                                receivingPacket.setSocketAddress(remoteEndPoint.Serialize());
                                receivingPacket.setData(receivedResults);  

		                        if (receivingPacket.getLength() == TRANSACTION_PACKET_SIZE)
		                        {
		                            foreach (Neighbor neighbor in neighbors)
		                            {
		                                if (neighbor.Address.Equals(receivingPacket.getSocketAddress()))
		                                {
		                                    try
		                                    {
		                                        neighbor.numberOfAllTransactions++;
		                                        Transaction receivedTransaction = new Transaction(receivingPacket.getData(), receivedTransactionTrits, curl);

		                                        if (Storage.storeTransaction(receivedTransaction.hash, receivedTransaction, false) != 0)
		                                        {
		                                            neighbor.numberOfNewTransactions++;
		                                            broadcast(receivedTransaction);
		                                        }

		                                        long transactionPointer;
		                                        Array.Copy(receivingPacket.getData(), REQUESTED_TRANSACTION_OFFSET,
		                                            requestedTransaction,
		                                            0, Transaction.HASH_SIZE);
		                                        if (Array.Equals(requestedTransaction, receivedTransaction.hash))
		                                        {
		                                            transactionPointer =
		                                                Storage.transactionPointer(Milestone.latestMilestone.Sbytes());
		                                        }
		                                        else
		                                        {
		                                            transactionPointer = Storage.transactionPointer(requestedTransaction);

		                                        }

		                                        if (transactionPointer > Storage.CELLS_OFFSET - Storage.SUPER_GROUPS_OFFSET)
		                                        {
		                                            Array.Copy(Storage.loadTransaction(transactionPointer).bytes, 0,
		                                                sendingPacket.getData(), 0, Transaction.SIZE);
		                                            Storage.TransactionToRequest(sendingPacket.getData(),
		                                                REQUESTED_TRANSACTION_OFFSET);
		                                            neighbor.send(sendingPacket);
		                                        }
		                                    }
		                                    catch (Exception e)
		                                    {
		                                        e.ToString();
		                                        neighbor.numberOfInvalidTransactions++;
		                                    }
		                                    break;
		                                }
		                            }
		                        }
		                        else
		                        {
		                            receivingPacket.setLength(TRANSACTION_PACKET_SIZE);
		                        }
		                    }
		                    catch (Exception e)
		                    {
		                        e.ToString();
		                    }
		                }
		            })).Start();

		        // ignore
		        (new Thread(() =>
		        {
		            while (!shuttingDown)
		            {
		                try
		                {
		                    Transaction transaction = queuedTransactions.RemoveAndGet(0);

		                    if (transaction != null)
		                    {
		                        foreach (Neighbor neighbor in neighbors)
		                        {
		                            try
		                            {
		                                Array.Copy(transaction.bytes, 0, sendingPacket.getData(), 0, Transaction.SIZE);
		                                Storage.TransactionToRequest(sendingPacket.getData(), REQUESTED_TRANSACTION_OFFSET);
		                                neighbor.send(sendingPacket);
		                            }
		                            catch (Exception e)
		                            {
		                            }
		                        }
		                    }
		                    Thread.Sleep(PAUSE_BETWEEN_TRANSACTIONS);
		                }
		                catch (Exception e)
		                {
		                    e.ToString();
		                }
		            }
		        })).Start();

		        (new Thread(() =>
		        {
		            while (!shuttingDown)
		            {
		                try
		                {
		                    Transaction transaction =
		                        Storage.loadTransaction(Storage.transactionPointer(Milestone.latestMilestone.Sbytes()));
		                    Array.Copy(transaction.bytes, 0, tipRequestingPacket.getData(), 0, Transaction.SIZE);
		                    Array.Copy(transaction.hash, 0, tipRequestingPacket.getData(), Transaction.SIZE, Transaction.HASH_SIZE);
		                    foreach (Neighbor neighbor in neighbors)
		                    {
		                        neighbor.send(tipRequestingPacket);

		                    }

		                    Thread.Sleep(5000);
		                }
		                catch (Exception e)
		                {
		                    e.ToString();
		                }
		            }
		        })).Start();
		    }
		    catch
		    {
		        throw new Exception();
		    }
		}


		public static void broadcast(Transaction transaction)
		{
			queuedTransactions.Add(transaction);

			if (queuedTransactions.Count > QUEUE_SIZE)
			{
			    queuedTransactions.RemoveAt(queuedTransactions.Count - 1);
			}
		}

		public static void shutDown()
		{
			shuttingDown = true;
		}
	}

}