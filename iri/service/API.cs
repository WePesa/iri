using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using com.iota.iri;
using com.iota.iri.conf;
using com.iota.iri.hash;
using com.iota.iri.model;
using com.iota.iri.service;
using com.iota.iri.service.dto;
using com.iota.iri.service.storage;
using com.iota.iri.utils;
using slf4net;


// 1.1.2.3

namespace com.iota.iri.service
{

	import static io.undertow.Handlers.path;


	using com.iota.iri.service.dto;
	using IOUtils = org.apache.commons.io.IOUtils;
	using Logger = org.slf4j.Logger;
	using LoggerFactory = org.slf4j.LoggerFactory;
	using StreamSinkChannel = org.xnio.channels.StreamSinkChannel;
	using ChannelInputStream = org.xnio.streams.ChannelInputStream;

	using Gson = com.google.gson.Gson;
	using GsonBuilder = com.google.gson.GsonBuilder;
	using IRI = com.iota.iri.IRI;
	using Milestone = com.iota.iri.Milestone;
	using Neighbor = com.iota.iri.Neighbor;
	using Snapshot = com.iota.iri.Snapshot;
	using Configuration = com.iota.iri.conf.Configuration;
	using DefaultConfSettings = com.iota.iri.conf.Configuration.DefaultConfSettings;
	using Curl = com.iota.iri.hash.Curl;
	using PearlDiver = com.iota.iri.hash.PearlDiver;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;
	using Storage = com.iota.iri.service.storage.Storage;
	using StorageAddresses = com.iota.iri.service.storage.StorageAddresses;
	using StorageApprovers = com.iota.iri.service.storage.StorageApprovers;
	using StorageBundle = com.iota.iri.service.storage.StorageBundle;
	using StorageScratchpad = com.iota.iri.service.storage.StorageScratchpad;
	using StorageTags = com.iota.iri.service.storage.StorageTags;
	using StorageTransactions = com.iota.iri.service.storage.StorageTransactions;
	using Converter = com.iota.iri.utils.Converter;

	using Undertow = io.undertow.Undertow;
	using HttpHandler = io.undertow.server.HttpHandler;
	using HttpServerExchange = io.undertow.server.HttpServerExchange;
	using HeaderMap = io.undertow.util.HeaderMap;
	using Headers = io.undertow.util.Headers;
	using HttpString = io.undertow.util.HttpString;

	public class API
	{

		private static readonly ILogger log = LoggerFactory.getLogger(typeof(API));

		private Undertow server;

		private readonly Gson gson = new GsonBuilder().create();
		private readonly PearlDiver pearlDiver = new PearlDiver();

		private readonly AtomicInteger counter = new AtomicInteger(0);

		public virtual void init()
		{
		    try
		    {
		        int apiPort = Configuration.integer(Configuration.DefaultConfSettings.API_PORT);
		        string apiHost = Configuration.
		        string 
		        (Configuration.DefaultConfSettings.API_HOST);

		        log.debug("Binding JSON-REST API Undertown server on {}:{}", apiHost, apiPort);

		        server =
		            Undertow.builder()
		                .addHttpListener(apiPort, apiHost)
		                .setHandler(path()
		                    .addPrefixPath("/",
		                        new HttpHandler()
		                        { public
		                        void handleRequest(final HttpServerExchange exchange) throws Exception { if (exchange.
    InIoThread) { exchange.dispatch(this); return;
		                        }
		        processRequest(exchange);
		    }
		    }
		    )).
		        build();
		        server.start();
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		private void processRequest(HttpServerExchange exchange)
		{
		    try
		    {
		        ChannelInputStream cis = new ChannelInputStream(exchange.RequestChannel);
		        exchange.ResponseHeaders.put(Headers.CONTENT_TYPE, "application/json");

		        long beginningTime = System.currentTimeMillis();
		        string body = IOUtils.ToString(cis, StandardCharsets.UTF_8);
		        AbstractResponse response = process(body);
		        sendResponse(exchange, response, beginningTime);
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		private AbstractResponse process(string requestString) // how can it throw UnsupportedEncodingException ???
		{

			try
			{

				IDictionary<string, object> request = gson.fromJson(requestString, typeof(IDictionary));
				if (request == null)
				{
					return ExceptionResponse.create("Invalid request payload: '" + requestString + "'");
				}

				string command = (string) request["command"];
				if (command == null)
				{
					return ErrorResponse.create("COMMAND parameter has not been specified in the request.");
				}

				if (Configuration.string(Configuration.DefaultConfSettings.REMOTEAPILIMIT).Contains(command))
				{
					return AccessLimitedResponse.create("COMMAND " + command + " is not available on this node");
				}

				log.Info("# {} -> Requesting command '{}'", counter.incrementAndGet(), command);

				switch (command)
				{

					case "addNeighbors":
					{
						IList<string> uris = (IList<string>) request["uris"];
						log.debug("Invoking 'addNeighbors' with {}", uris);
						return addNeighborsStatement(uris);
					}
					case "attachToTangle":
					{
						Hash trunkTransaction = new Hash((string) request["trunkTransaction"]);
						Hash branchTransaction = new Hash((string) request["branchTransaction"]);
						int minWeightMagnitude = (int)((double?) request["minWeightMagnitude"]);
						IList<string> trytes = (IList<string>) request["trytes"];

						return attachToTangleStatement(trunkTransaction, branchTransaction, minWeightMagnitude, trytes);
					}
					case "broadcastTransactions":
					{
						IList<string> trytes = (IList<string>) request["trytes"];
						log.debug("Invoking 'broadcastTransactions' with {}", trytes);
						return broadcastTransactionStatement(trytes);
					}
					case "findTransactions":
					{
						return findTransactionStatement(request);
					}
					case "getBalances":
					{
						IList<string> addresses = (IList<string>) request["addresses"];
						int threshold = (int)((double?) request["threshold"]);
						return getBalancesStatement(addresses, threshold);
					}
					case "getInclusionStates":
					{
						IList<string> trans = (IList<string>) request["transactions"];
						IList<string> tps = (IList<string>) request["tips"];

						if (trans == null || tps == null)
						{
							return ErrorResponse.create("getInclusionStates Bad Request.");
						}

						if (invalidSubtangleStatus())
						{
							return ErrorResponse.create("This operations cannot be executed: The subtangle has not been updated yet.");
						}
						return getInclusionStateStatement(trans, tps);
					}
					case "getNeighbors":
					{
						return NeighborsStatement;
					}
					case "getNodeInfo":
					{
						return GetNodeInfoResponse.create(IRI.NAME, IRI.VERSION, Runtime.Runtime.availableProcessors(), Runtime.Runtime.freeMemory(), System.getProperty("java.version"), Runtime.Runtime.maxMemory(), Runtime.Runtime.totalMemory(), Milestone.latestMilestone, Milestone.latestMilestoneIndex, Milestone.latestSolidSubtangleMilestone, Milestone.latestSolidSubtangleMilestoneIndex, Node.instance().howManyNeighbors(), Node.instance().queuedTransactionsSize(), System.currentTimeMillis(), StorageTransactions.instance().tips().size(), StorageScratchpad.instance().NumberOfTransactionsToRequest);
					}
					case "getTips":
					{
						return TipsStatement;
					}
					case "getTransactionsToApprove":
					{
						int depth = (int)((double?) request["depth"]);
						if (invalidSubtangleStatus())
						{
							return ErrorResponse.create("This operations cannot be executed: The subtangle has not been updated yet.");
						}
						return getTransactionToApproveStatement(depth);
					}
					case "getTrytes":
					{
						IList<string> hashes = (IList<string>) request["hashes"];
						log.debug("Executing getTrytesStatement: {}", hashes);
						return getTrytesStatement(hashes);
					}

					case "interruptAttachingToTangle":
					{
						pearlDiver.cancel();
						return AbstractResponse.createEmptyResponse();
					}
					case "removeNeighbors":
					{
						IList<string> uris = (IList<string>) request["uris"];
						log.debug("Invoking 'removeNeighbors' with {}", uris);
						return removeNeighborsStatement(uris);
					}

					case "storeTransactions":
					{
						IList<string> trytes = (IList<string>) request["trytes"];
						log.debug("Invoking 'storeTransactions' with {}", trytes);
						return storeTransactionStatement(trytes);
					}
					default:
						return ErrorResponse.create("Command [" + command + "] is unknown");
				}

			}
			catch (Exception e)
			{
				log.error("API Exception: ", e);
				return ExceptionResponse.create(e.LocalizedMessage);
			}
		}

		public static bool invalidSubtangleStatus()
		{
			return (Milestone.latestSolidSubtangleMilestoneIndex == Milestone.MILESTONE_START_INDEX);
		}

		private AbstractResponse removeNeighborsStatement(IList<string> uris)
		{
		    try
		    {
		        AtomicInteger numberOfRemovedNeighbors = new AtomicInteger(0);
		        uris.stream()
		            .map(com.iota.iri.service.Node::uri)
		            .map(Optional::get)
		            .filter(u => "udp".Equals(u.Scheme))
		            .forEach(u=>
		        {
		            if (Node.instance().removeNeighbor(u))
		            {
		                numberOfRemovedNeighbors.incrementAndGet();
		            }
		        }
		    )
		        ;
		        return RemoveNeighborsResponse.create(numberOfRemovedNeighbors.get());
		    }
		    catch
		    {
		        throw new UriFormatException();
		    }
		}

		private AbstractResponse getTrytesStatement(IList<string> hashes)
		{
			IList<string> elements = new LinkedList<>();
			foreach (string hash in hashes)
			{
				Transaction transaction = StorageTransactions.instance().loadTransaction((new Hash(hash)).bytes());
				if (transaction != null)
				{
					elements.Add(Converter.trytes(transaction.trits()));
				}
			}
			return GetTrytesResponse.create(elements);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private AbstractResponse getTransactionToApproveStatement(int depth)
		{
			Hash trunkTransactionToApprove = TipsManager.transactionToApprove(null, depth);
			if (trunkTransactionToApprove == null)
			{
				return ErrorResponse.create("The subtangle is not solid");
			}
			Hash branchTransactionToApprove = TipsManager.transactionToApprove(trunkTransactionToApprove, depth);
			if (branchTransactionToApprove == null)
			{
				return ErrorResponse.create("The subtangle is not solid");
			}
			return GetTransactionsToApproveResponse.create(trunkTransactionToApprove, branchTransactionToApprove);
		}

		private AbstractResponse TipsStatement
		{
			get
			{
				return GetTipsResponse.create(StorageTransactions.instance().tips().stream().map(Hash::toString).collect(Collectors.toList()));
			}
		}

		private AbstractResponse storeTransactionStatement(IList<string> trys)
		{
			foreach (string trytes in trys)
			{
				Transaction transaction = new Transaction(Converter.trits(trytes));
				StorageTransactions.instance().storeTransaction(transaction.hash, transaction, false);
			}
			return AbstractResponse.createEmptyResponse();
		}

		private AbstractResponse NeighborsStatement
		{
			get
			{
				return GetNeighborsResponse.create(Node.instance().Neighbors);
			}
		}

		private AbstractResponse getInclusionStateStatement(IList<string> trans, IList<string> tps)
		{

			IList<Hash> transactions = trans.stream().map(s -> new Hash(s)).collect(Collectors.toList());
			IList<Hash> tips = tps.stream().map(s -> new Hash(s)).collect(Collectors.toList());

			int numberOfNonMetTransactions = transactions.Count;
			bool[] inclusionStates = new bool[numberOfNonMetTransactions];

			lock (StorageScratchpad.instance().AnalyzedTransactionsFlags)
			{

				StorageScratchpad.instance().clearAnalyzedTransactionsFlags();

				LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>();
				foreach (Hash tip in tips)
				{

					long pointer = StorageTransactions.instance().transactionPointer(tip.bytes());
					if (pointer <= 0)
					{
						return ErrorResponse.create("One of the tips absents");
					}
					nonAnalyzedTransactions.AddLast(pointer);
				}

				{
					long? pointer;
					MAIN_LOOP:
					while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
					{

						if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
						{

							Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);
							if (transaction.type == Storage.PREFILLED_SLOT)
							{
								return ErrorResponse.create("The subtangle is not solid");
							}
							else
							{

								Hash transactionHash = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
								for (int i = 0; i < inclusionStates.Length; i++)
								{

									if (!inclusionStates[i] && transactionHash.Equals(transactions[i]))
									{

										inclusionStates[i] = true;

										if (--numberOfNonMetTransactions <= 0)
										{
											goto MAIN_LOOP;
										}
									}
								}
								nonAnalyzedTransactions.AddLast(transaction.trunkTransactionPointer);
								nonAnalyzedTransactions.AddLast(transaction.branchTransactionPointer);
							}
						}
					}
					return GetInclusionStatesResponse.create(inclusionStates);
				}
			}
		}

		private AbstractResponse findTransactionStatement(IDictionary<string, object> request)
		{
			Set<long?> bundlesTransactions = new HashSet<>();
			if (request.ContainsKey("bundles"))
			{
				foreach (string bundle in (IList<string>) request["bundles"])
				{
					bundlesTransactions.addAll(StorageBundle.instance().bundleTransactions(StorageBundle.instance().bundlePointer((new Hash(bundle)).bytes())));
				}
			}

			Set<long?> addressesTransactions = new HashSet<>();
			if (request.ContainsKey("addresses"))
			{
				IList<string> addresses = (IList<string>) request["addresses"];
				log.debug("Searching: {}", addresses.stream().reduce((a, b) -> a += ',' + b));

				foreach (string address in addresses)
				{
					if (address.Length != 81)
					{
						log.error("Address {} doesn't look a valid address", address);
					}
					addressesTransactions.addAll(StorageAddresses.instance().addressTransactions(StorageAddresses.instance().addressPointer((new Hash(address)).bytes())));
				}
			}

			Set<long?> tagsTransactions = new HashSet<>();
			if (request.ContainsKey("tags"))
			{
				foreach (string tag in (IList<string>) request["tags"])
				{
					while (tag.Length < Curl.HASH_LENGTH / Converter.NUMBER_OF_TRITS_IN_A_TRYTE)
					{
						tag += Converter.TRYTE_ALPHABET[0];
					}
					tagsTransactions.addAll(StorageTags.instance().tagTransactions(StorageTags.instance().tagPointer((new Hash(tag)).bytes())));
				}
			}

			Set<long?> approveeTransactions = new HashSet<>();

			if (request.ContainsKey("approvees"))
			{
				foreach (string approvee in (IList<string>) request["approvees"])
				{
					approveeTransactions.addAll(StorageApprovers.instance().approveeTransactions(StorageApprovers.instance().approveePointer((new Hash(approvee)).bytes())));
				}
			}

		    // need refactoring
			Set<long?> foundTransactions = bundlesTransactions.Empty ? (addressesTransactions.Empty ? (tagsTransactions.Empty ? (approveeTransactions.Empty ? new HashSet<>() : approveeTransactions) : tagsTransactions) : addressesTransactions) : bundlesTransactions;

			if (!addressesTransactions.Empty)
			{
				foundTransactions.retainAll(addressesTransactions);
			}
			if (!tagsTransactions.Empty)
			{
				foundTransactions.retainAll(tagsTransactions);
			}
			if (!approveeTransactions.Empty)
			{
				foundTransactions.retainAll(approveeTransactions);
			}

			IList<string> elements = foundTransactions.stream().map(pointer -> new Hash(StorageTransactions.instance().loadTransaction(pointer).hash, 0, Transaction.HASH_SIZE).ToString()).collect(Collectors.toCollection(LinkedList::new));

			return FindTransactionsResponse.create(elements);
		}

		private AbstractResponse broadcastTransactionStatement(IList<string> trytes2)
		{
			foreach (string tryte in trytes2)
			{
				Transaction transaction = new Transaction(Converter.trits(tryte));
				transaction.weightMagnitude = Curl.HASH_LENGTH;
				Node.instance().broadcast(transaction);
			}
			return AbstractResponse.createEmptyResponse();
		}

		private AbstractResponse getBalancesStatement(IList<string> addrss, int threshold)
		{

			if (threshold <= 0 || threshold > 100)
			{
				return ErrorResponse.create("Illegal 'threshold'");
			}

			IList<Hash> addresses = addrss.stream().map(address -> (new Hash(address))).collect(Collectors.toCollection(LinkedList::new));

			IDictionary<Hash, long?> balances = new Dictionary<>();
			foreach (Hash address in addresses)
			{
				balances.Add(address, Snapshot.initialState.ContainsKey(address) ? Snapshot.initialState[address] : Convert.ToInt64(0));
			}

			Hash milestone = Milestone.latestSolidSubtangleMilestone;
			int milestoneIndex = Milestone.latestSolidSubtangleMilestoneIndex;

			lock (StorageScratchpad.instance().AnalyzedTransactionsFlags)
			{

				StorageScratchpad.instance().clearAnalyzedTransactionsFlags();

				LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>(Collections.singleton(StorageTransactions.instance().transactionPointer(milestone.bytes())));
				long? pointer;
				while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
				{

					if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
					{

						Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);

						if (transaction.value != 0)
						{

							Hash address = new Hash(transaction.address, 0, Transaction.ADDRESS_SIZE);
							long? balance = balances[address];
							if (balance != null)
							{

								balances.Add(address, balance + transaction.value);
							}
						}
						nonAnalyzedTransactions.AddLast(transaction.trunkTransactionPointer);
						nonAnalyzedTransactions.AddLast(transaction.branchTransactionPointer);
					}
				}
			}

			IList<string> elements = addresses.stream().map(address -> balances[address].ToString()).collect(Collectors.toCollection(LinkedList::new));

			return GetBalancesResponse.create(elements, milestone, milestoneIndex);
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		private AbstractResponse attachToTangleStatement(Hash trunkTransaction, Hash branchTransaction, int minWeightMagnitude, IList<string> trytes)
		{
			IList<Transaction> transactions = new LinkedList<>();

			Hash prevTransaction = null;

			foreach (string tryte in trytes)
			{

				int[] transactionTrits = Converter.trits(tryte);
				Array.Copy((prevTransaction == null ? trunkTransaction : prevTransaction).trits(), 0, transactionTrits, Transaction.TRUNK_TRANSACTION_TRINARY_OFFSET, Transaction.TRUNK_TRANSACTION_TRINARY_SIZE);
				Array.Copy((prevTransaction == null ? branchTransaction : trunkTransaction).trits(), 0, transactionTrits, Transaction.BRANCH_TRANSACTION_TRINARY_OFFSET, Transaction.BRANCH_TRANSACTION_TRINARY_SIZE);

				if (!pearlDiver.search(transactionTrits, minWeightMagnitude, 0))
				{
					transactions.Clear();
					break;
				}
				Transaction transaction = new Transaction(transactionTrits);
				transactions.Add(transaction);
				prevTransaction = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
			}

			IList<string> elements = new LinkedList<>();
			for (int i = transactions.Count; i-- > 0;)
			{
				elements.Add(Converter.trytes(transactions[i].trits()));
			}
			return AttachToTangleResponse.create(elements);
		}

		private AbstractResponse addNeighborsStatement(IList<string> uris)
		{
		    try
		    {
		        int numberOfAddedNeighbors = 0;
		        foreach (string uriString in uris)
		        {
		            URI uri = new URI(uriString);
		            if ("udp".Equals(uri.Scheme))
		            {
		                Neighbor neighbor = new Neighbor(new InetSocketAddress(uri.Host, uri.Port));
		                if (!Node.instance().Neighbors.Contains(neighbor))
		                {
		                    Node.instance().Neighbors.Add(neighbor);
		                    numberOfAddedNeighbors++;
		                }
		            }
		        }
		        return AddedNeighborsResponse.create(numberOfAddedNeighbors);
		    }
		    catch
		    {
		        throw new UriFormatException();
		    }
		}

		private void sendResponse(HttpServerExchange exchange, AbstractResponse res, long beginningTime)
		{
		    try
		    {
		        res.Duration = (int) (System.currentTimeMillis() - beginningTime);
		        string response = gson.toJson(res);

		        if (res is ErrorResponse)
		        {
		            exchange.StatusCode = 400; // bad request
		        }
		        else if (res is AccessLimitedResponse)
		        {
		            exchange.StatusCode = 401; // api method not allowed
		        }
		        else if (res is ExceptionResponse)
		        {
		            exchange.StatusCode = 500; // internal error
		        }

		        setupResponseHeaders(exchange);

		        ByteBuffer responseBuf = ByteBuffer.wrap(response.getBytes(StandardCharsets.UTF_8));
		        exchange.ResponseContentLength = responseBuf.array().length;
		        StreamSinkChannel sinkChannel = exchange.ResponseChannel;
		        sinkChannel.WriteSetter.set(channel->
		        {
		            if (responseBuf.remaining() > 0)
		                try
		                {
		                    sinkChannel.write(responseBuf);
		                    if (responseBuf.remaining() == 0)
		                    {
		                        exchange.endExchange();
		                    }
		                }
		                catch (IOException e)
		                {
		                    log.error("Error writing response", e);
		                    exchange.endExchange();
		                }
		            else
		            {
		                exchange.endExchange();
		            }
		        }
		    )
		        ;
		        sinkChannel.resumeWrites();
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		private static void setupResponseHeaders(HttpServerExchange exchange)
		{
			HeaderMap headerMap = exchange.ResponseHeaders;
			headerMap.add(new HttpString("Access-Control-Allow-Origin"), Configuration.string(Configuration.DefaultConfSettings.CORS_ENABLED));
			headerMap.add(new HttpString("Keep-Alive"), "timeout=500, max=100");
		}

		public virtual void shutDown()
		{
			if (server != null)
			{
				server.stop();
			}
		}

		private static API instance = new API();

		public static API instance()
		{
			return instance;
		}
	}

}