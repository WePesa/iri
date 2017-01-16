using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using iri.utils;
using slf4net;

// 1.1.2.3

namespace com.iota.iri.service
{


	using Hash = com.iota.iri.model.Hash;
	using StringUtils = org.apache.commons.lang3.StringUtils;

	using Milestone = com.iota.iri.Milestone;
	using Neighbor = com.iota.iri.Neighbor;
	using Configuration = com.iota.iri.conf.Configuration;
	using DefaultConfSettings = com.iota.iri.conf.Configuration.DefaultConfSettings;
	using Curl = com.iota.iri.hash.Curl;
	using Transaction = com.iota.iri.model.Transaction;
	using Storage = com.iota.iri.service.storage.Storage;
	using StorageScratchpad = com.iota.iri.service.storage.StorageScratchpad;
	using StorageTransactions = com.iota.iri.service.storage.StorageTransactions;

    ///
    /// <summary> * The class node is responsible for managing Thread's connection. </summary>
    /// 
	public class Node
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(Node));

		private static readonly Node instance = new Node();

		private const int TRANSACTION_PACKET_SIZE = 1650;
		private const int QUEUE_SIZE = 1000;
		private const int PAUSE_BETWEEN_TRANSACTIONS = 1;

		private DatagramSocket socket;

		private readonly AtomicBoolean shuttingDown = new AtomicBoolean(false);

		private readonly IList<Neighbor> neighbors = new CopyOnWriteArrayList<>();
		private readonly ConcurrentSkipListSet<Transaction> queuedTransactions = weightQueue();

		private readonly DatagramPacket receivingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);
		private readonly DatagramPacket sendingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);
		private readonly DatagramPacket tipRequestingPacket = new DatagramPacket(new sbyte[TRANSACTION_PACKET_SIZE], TRANSACTION_PACKET_SIZE);

		private readonly ExecutorService executor = Executors.newFixedThreadPool(4);

		public virtual void init()
		{
		    try
		    {
		        socket = new DatagramSocket(Configuration.integer(DefaultConfSettings.TANGLE_RECEIVER_PORT));

		        Arrays.stream(StringHelperClass.StringSplit(Configuration.
		        string 
		        (DefaultConfSettings.NEIGHBORS),
		        " ",
		        true)).
		        distinct().filter(s=>
		        !s.Empty).
		        map(Node::uri).map(Optional::get).peek(u=>
		        {
		            if (!"udp".Equals(u.Scheme))
		            {
		                log.warn("WARNING: '{}' is not a valid udp:// uri schema.", u);
		            }
		        }
		    ).
		        filter(u=>
		        "udp".Equals(u.Scheme)).
		        map(u=>
		        new Neighbor(new InetSocketAddress(u.Host, u.Port))).
		        peek(u=>
		        {
		            if (Configuration.booling(DefaultConfSettings.DEBUG))
		            {
		                log.Debug("-> Adding neighbor : {} ", u.Address);
		            }
		        }
		    ).
		        forEach(neighbors::add);

		        executor.submit(spawnReceiverThread());
		        executor.submit(spawnBroadcasterThread());
		        executor.submit(spawnTipRequesterThread());
		        executor.submit(spawnNeighborDNSRefresherThread());

		        executor.shutdown();
		    }
		    catch
		    {
		        throw new Exception();
		    }
		}

		private IDictionary<string, string> neighborIpCache = new Dictionary<string, string>();

		private Runnable spawnNeighborDNSRefresherThread()
		{
			return () =>
			{

				log.Info("Spawning Neighbor DNS Refresher Thread");

				while (!shuttingDown.get())
				{
					log.Info("Checking Neighbors' Ip...");

					try
					{
						neighbors.forEach(n => { string hostname = n.Address.HostName; checkIp(hostname).ifPresent(ip => { log.Info("DNS Checker: Validating DNS Address '{}' with '{}'", hostname, ip); string neighborAddress = neighborIpCache[hostname]; if (neighborAddress == null) { neighborIpCache.Add(neighborAddress, ip); } else { if (neighborAddress.Equals(ip)) { log.Info("{} seems fine.", hostname); } else { log.Info("CHANGED IP for {}! Updating...", hostname); uri("udp://" + hostname).ifPresent(uri => { removeNeighbor(uri); uri("udp://" + ip).ifPresent(nuri => { addNeighbor(nuri); neighborIpCache.Add(hostname, ip); }); }); } } }); });

						Thread.Sleep(1000*60*30);
					}
					catch (Exception e)
					{
						log.Error("Neighbor DNS Refresher Thread Exception:", e);
					}
				}
				log.Info("Shutting down Neighbor DNS Resolver Thread");
			}
		}

		private Optional<string> checkIp(string dnsName)
		{

			if (StringUtils.isEmpty(dnsName))
			{
				return Optional.empty();
			}

			InetAddress inetAddress;
			try
			{
				inetAddress = java.net.InetAddress.getByName(dnsName);
			}
			catch (UnknownHostException e)
			{
				return Optional.empty();
			}

			string hostAddress = inetAddress.HostAddress;

			if (StringUtils.Equals(dnsName, hostAddress)) // not a DNS...
			{
				return Optional.empty();
			}

			return Optional.of(hostAddress);
		}

		private Runnable spawnReceiverThread()
		{
			return () =>
			{

				Curl curl = new Curl();
				int[] receivedTransactionTrits = new int[Transaction.TRINARY_SIZE];
				sbyte[] requestedTransaction = new sbyte[Transaction.HASH_SIZE];

				log.Info("Spawning Receiver Thread");

				SecureRandom rnd = new SecureRandom();
				long randomTipBroadcastCounter = 0;

				while (!shuttingDown.get())
				{

					try
					{
						socket.receive(receivingPacket);

						if (receivingPacket.Length == TRANSACTION_PACKET_SIZE)
						{

							foreach (Neighbor neighbor in neighbors)
							{
								if (neighbor.Address.Equals(receivingPacket.SocketAddress))
								{
									try
									{

										neighbor.incAllTransactions();
										Transaction receivedTransaction = new Transaction(receivingPacket.Data, receivedTransactionTrits, curl);
										if (StorageTransactions.instance().storeTransaction(receivedTransaction.hash, receivedTransaction, false) != 0)
										{
											neighbor.incNewTransactions();
											broadcast(receivedTransaction);
										}

										const long transactionPointer;
										Array.Copy(receivingPacket.Data, Transaction.SIZE, requestedTransaction, 0, Transaction.HASH_SIZE);
										if (Array.Equals(requestedTransaction, receivedTransaction.hash))
										{

											if (Configuration.booling(DefaultConfSettings.EXPERIMENTAL) && ++randomTipBroadcastCounter % 3 == 0)
											{
												log.Info("Experimental: Random Tip Broadcaster.");

												string[] tips = StorageTransactions.instance().tips().stream().map(Hash::toString).ToArray(size => new string[size]);
												string rndTipHash = tips[rnd.Next(tips.Length)];

												transactionPointer = StorageTransactions.instance().transactionPointer(rndTipHash.Bytes);
											}
											else
											{
												transactionPointer = StorageTransactions.instance().transactionPointer(Milestone.latestMilestone.bytes());
											}
										}
										else
										{
											transactionPointer = StorageTransactions.instance().transactionPointer(requestedTransaction);
										}
										if (transactionPointer > Storage.CELLS_OFFSET - Storage.SUPER_GROUPS_OFFSET)
										{
											lock (sendingPacket)
											{
												Array.Copy(StorageTransactions.instance().loadTransaction(transactionPointer).bytes, 0, sendingPacket.Data, 0, Transaction.SIZE);
												StorageScratchpad.instance().transactionToRequest(sendingPacket.Data, Transaction.SIZE);
												neighbor.send(sendingPacket);
											}
										}
									}
									catch (Exception e)
									{
										log.Error("Received an Invalid Transaction. Dropping it...");
										neighbor.incInvalidTransactions();
									}
									break;
								}
							}
						}
						else
						{
							receivingPacket.Length = TRANSACTION_PACKET_SIZE;
						}
					}
					catch (Exception e)
					{
						log.Error("Receiver Thread Exception:", e);
					}
				}
				log.Info("Shutting down spawning Receiver Thread");
			}
		}

		private Runnable spawnBroadcasterThread()
		{
			return () =>
			{

				log.Info("Spawning Broadcaster Thread");

				while (!shuttingDown.get())
				{

					try
					{
						Transaction transaction = queuedTransactions.pollFirst();
						if (transaction != null)
						{

							foreach (Neighbor neighbor in neighbors)
							{
								try
								{
									lock (sendingPacket)
									{
										Array.Copy(transaction.bytes, 0, sendingPacket.Data, 0, Transaction.SIZE);
										StorageScratchpad.instance().transactionToRequest(sendingPacket.Data, Transaction.SIZE);
										neighbor.send(sendingPacket);
									}
								}
								catch (Exception e)
								{
								// ignore
								}
							}
						}
						Thread.Sleep(PAUSE_BETWEEN_TRANSACTIONS);
					}
					catch (Exception e)
					{
						log.Error("Broadcaster Thread Exception:", e);
					}
				}
				log.Info("Shutting down Broadcaster Thread");
			}
		}

		private Runnable spawnTipRequesterThread()
		{
			return () =>
			{

				log.Info("Spawning Tips Requester Thread");

				while (!shuttingDown.get())
				{

					try
					{
						Transaction transaction = StorageTransactions.instance().loadMilestone(Milestone.latestMilestone);
						Array.Copy(transaction.bytes, 0, tipRequestingPacket.Data, 0, Transaction.SIZE);
						Array.Copy(transaction.hash, 0, tipRequestingPacket.Data, Transaction.SIZE, Transaction.HASH_SIZE);

						neighbors.forEach(n -> n.send(tipRequestingPacket));

						Thread.Sleep(5000);
					}
					catch (Exception e)
					{
						log.Error("Tips Requester Thread Exception:", e);
					}
				}
				log.Info("Shutting down Requester Thread");
			}
		}

		private static ConcurrentSkipListSet<Transaction> weightQueue()
		{
			return new ConcurrentSkipListSet<>((transaction1, transaction2) => { if (transaction1.weightMagnitude == transaction2.weightMagnitude) { for (int i = 0; i < Transaction.HASH_SIZE; i++) { if (transaction1.hash[i] != transaction2.hash[i]) { return transaction2.hash[i] - transaction1.hash[i]; } } return 0; } return transaction2.weightMagnitude - transaction1.weightMagnitude; });
		}

		public virtual void broadcast(Transaction transaction)
		{
			queuedTransactions.add(transaction);
			if (queuedTransactions.size() > QUEUE_SIZE)
			{
				queuedTransactions.pollLast();
			}
		}

		public virtual void shutdown()
		{
		    try
		    {
		        shuttingDown.set(true);
		        executor.awaitTermination(6, TimeUnit.SECONDS);
		    }
		    catch
		    {
		        throw new InterruptedException();
		    }
		}

		public virtual void send(DatagramPacket packet)
		{
			try
			{
				socket.send(packet);
			}
			catch (IOException e)
			{
			// ignore
			}
		}

	   // helpers methods

		public virtual bool removeNeighbor(URI uri)
		{
			return neighbors.Remove(new Neighbor(new InetSocketAddress(uri.Host, uri.Port)));
		}

		public virtual bool addNeighbor(URI uri)
		{
			Neighbor neighbor = new Neighbor(new InetSocketAddress(uri.Host, uri.Port));
			if (!Node.instance().Neighbors.Contains(neighbor))
			{
				return Node.instance().Neighbors.Add(neighbor);
			}
			return false;
		}

		public static Optional<URI> uri(string uri)
		{
			try
			{
				return Optional.of(new URI(uri));
			}
			catch (URISyntaxException e)
			{
				log.Error("Uri {} raised URI Syntax Exception", uri);
			}
			return Optional.empty();
		}

		public static Node instance()
		{
			return instance;
		}

		public virtual int queuedTransactionsSize()
		{
			return queuedTransactions.size();
		}

		public virtual int howManyNeighbors()
		{
			return neighbors.Count;
		}

		public virtual IList<Neighbor> Neighbors
		{
			get
			{
				return neighbors;
			}
		}

		private Node()
		{
		}
	}

}