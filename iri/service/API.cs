using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using com.iota.iri.service.storage;
using iri.utils;
using Newtonsoft.Json;
using slf4net;
using System.Linq;

namespace com.iota.iri.service
{

    //using Gson = com.google.gson.Gson;
    //using GsonBuilder = com.google.gson.GsonBuilder;

	using IRI = com.iota.iri.IRI;
	using Milestone = com.iota.iri.Milestone;
	using Neighbor = com.iota.iri.Neighbor;
	using Snapshot = com.iota.iri.Snapshot;
	using Curl = com.iota.iri.hash.Curl;
	using PearlDiver = com.iota.iri.hash.PearlDiver;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;
	using com.iota.iri.service.dto;
	using Converter = com.iota.iri.utils.Converter;

    //using Undertow = io.undertow.Undertow;
    //using HttpServerExchange = io.undertow.server.HttpServerExchange;
    //using Headers = io.undertow.util.Headers;
    //using IOUtils = org.apache.commons.io.IOUtils;

    //using ChannelInputStream = org.xnio.streams.ChannelInputStream;


	public class API
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(API));

        private static HttpListener server = new HttpListener();
		private const int PORT = 14265;

		private static readonly PearlDiver pearlDiver = new PearlDiver();

		public static void launch()
		{
		    try
		    {
                server.Prefixes.Add(string.Format("http://localhost:{0}/", PORT));
         
                server.Start();

                //                    server =
                //    Undertow.builder().addHttpListener(PORT, "localhost").setHandler(path().addPrefixPath("/", exchange =>
                //    {
                //        ChannelInputStream cis = new ChannelInputStream(exchange.RequestChannel);
                //        exchange.ResponseHeaders.put(Headers.CONTENT_TYPE, "application/json");
                //        long beginningTime = DateTimeExtensions.currentTimeMillis();
                //        string body = IOUtils.ToString(cis, StandardCharsets.UTF_8);
                //        AbstractResponse response = process(body);
                //        sendResponse(exchange, response, beginningTime);
                //    })).build();

                //server.start()


                // string response;
                HttpListenerResponse exchange = null;

		        while (server.IsListening) // !shutdown
		        {	           
                    HttpListenerContext context = server.GetContext();

                    exchange = context.Response;

                    HttpListenerRequest contextRequest = context.Request;

                    if (contextRequest == null || contextRequest.HttpMethod != "POST" || !contextRequest.HasEntityBody)
                    {
                        continue;
                    }

                    long beginningTime = DateTimeExtensions.currentTimeMillis();

                    string requestedString = GetRequestPostData(contextRequest);

                    AbstractResponse response = process(requestedString);

		            sendResponse(exchange, response, beginningTime);
		        }

		    }
            catch // HttpListenerException ex
		    {
		        throw new IOException();
		    }
		}

		// private static readonly Gson gson = new GsonBuilder().create();

        private static AbstractResponse process(string requestString) // throws UnsupportedEncodingException ??
		{

			try
			{

                Dictionary<string, object> request = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestString);
				// IDictionary<string, object> request = gson.fromJson(requestString, typeof(IDictionary));

				string command = (string) request["command"];

				if (command == null)
				{
					return ErrorResponse.create("'command' parameter has not been specified");
				}

				switch (command)
				{

				case "addNeighbors":
				{

					IList<string> uris = (IList<string>) request["uris"];
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
					return getInclusionStateStatement(trans, tps);
				}
				case "getNeighbors":
				{
					return NeighborsStatement;
				}
				case "getNodeInfo":
				{
					return GetNodeInfoResponse.create(IRI.NAME, IRI.VERSION, Environment.ProcessorCount, 0, 0, 0, Milestone.latestMilestone, Milestone.latestMilestoneIndex, Milestone.latestSolidSubtangleMilestone, Milestone.latestSolidSubtangleMilestoneIndex, Node.neighbors.Count, Node.queuedTransactions.Count, DateTimeExtensions.currentTimeMillis(), Storage.tips().Count, Storage.numberOfTransactionsToRequest);
				}
				case "getTips":
				{
					return TipsStatement;
				}
				case "getTransactionsToApprove":
				{

					int depth = (int)((double?) request["depth"]);
					return getTransactionToApproveStatement(depth);
				}
				case "getTrytes":
				{

					IList<string> hashes = (IList<string>) request["hashes"];
					getTrytesStatement(hashes);
				}

				goto case "interruptAttachingToTangle";
				case "interruptAttachingToTangle":
				{
					pearlDiver.interrupt();
					return AbstractResponse.createEmptyResponse();
				}
				case "removeNeighbors":
				{
					IList<string> uris = (IList<string>) request["uris"];
					return removeNeighborsStatement(uris);
				}

				case "storeTransactions":
				{
					IList<string> trytes = (IList<string>) request["trytes"];
					return storeTransactionStatement(trytes);
				}
				default:
					return ErrorResponse.create("Command '" + command + "' is unknown");
				}

			}
			catch (Exception e) // throw UnsupportedEncodingException ???
			{
				log.Error("API Exception: ", e);
				return ExceptionResponse.create(e.Message);
			}
		}

        public static string GetRequestPostData(HttpListenerRequest request)
        {
            //if (!request.HasEntityBody)
            //{
            //    return null;
            //}

            using (Stream body = request.InputStream) // here we have data
            {
                using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }

		private static AbstractResponse removeNeighborsStatement(IList<string> uris)
		{
		    try
		    {
		        int numberOfRemovedNeighbors = 0;

		        foreach (string uriString in uris)
		        {
		            Uri uri = new Uri(uriString);

		            if (uri.Scheme != null && uri.Scheme.Equals("udp"))
		            {
		                var ipEndPoint = new IPEndPoint(Dns.GetHostEntry(uri.Host).AddressList[0], uri.Port);
		                SocketAddress socketAddress = ipEndPoint.Serialize();

		                if (Node.neighbors.Remove(new Neighbor(socketAddress)))
		                {
		                    numberOfRemovedNeighbors++;
		                }
		            }
		        }
		        return RemoveNeighborsResponse.create(numberOfRemovedNeighbors);
		    }
		    catch (Exception e)
		    {
		        throw new UriFormatException();
		    }
		}

		private static void getTrytesStatement(IList<string> hashes)
		{

			List<string> elements = new List<string>();

			foreach (string hash in hashes)
			{
				Transaction transaction = Storage.loadTransaction((new Hash(hash)).Sbytes());
				elements.Add(transaction == null ? "null" : (Converter.trytes(transaction.Trits())));
			}
			GetTrytesResponse.create(elements);
		}

		private static AbstractResponse getTransactionToApproveStatement(int depth)
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

		private static AbstractResponse TipsStatement
		{
			get
			{

				List<string> elements = new List<string>();

				foreach (Hash tip in Storage.tips())
				{
					elements.Add(tip.ToString());
				}
				return GetTipsResponse.create(elements);
			}
		}

		private static AbstractResponse storeTransactionStatement(IList<string> trys)
		{
			foreach (string trytes in trys)
			{
				Transaction transaction = new Transaction(Converter.trits(trytes));
				Storage.storeTransaction(transaction.hash, transaction, false);
			}

			return AbstractResponse.createEmptyResponse();
		}

		private static AbstractResponse NeighborsStatement
		{
			get
			{
				return GetNeighborsResponse.create(Node.neighbors);
			}
		}


		private static AbstractResponse getInclusionStateStatement(IList<string> trans, IList<string> tps)
		{

			List<Hash> transactions = new List<Hash>();

			foreach (string transaction in trans)
			{
				transactions.Add((new Hash(transaction)));
			}


			List<Hash> tips = new List<Hash>();

			foreach (string tip in tps)
			{
				tips.Add(new Hash(tip));
			}

			int numberOfNonMetTransactions = transactions.Count;

			bool[] inclusionStates = new bool[numberOfNonMetTransactions];

			lock (Storage.analyzedTransactionsFlags)
			{

				Storage.clearAnalyzedTransactionsFlags();

				List<long?> nonAnalyzedTransactions = new List<long?>();

				foreach (Hash tip in tips)
				{

					long pointer = Storage.transactionPointer(tip.Sbytes());

					if (pointer <= 0)
					{
						return ErrorResponse.create("One of the tips absents");
					}
					nonAnalyzedTransactions.Add(pointer);
				}

				{
					long? pointer;
					MAIN_LOOP:
					while ((pointer = nonAnalyzedTransactions.Poll()) != null)
					{

						if (Storage.setAnalyzedTransactionFlag((long)pointer))
						{

							Transaction transaction = Storage.loadTransaction((long)pointer);

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
								nonAnalyzedTransactions.Add(transaction.trunkTransactionPointer);
								nonAnalyzedTransactions.Add(transaction.branchTransactionPointer);
							}
						}
					}

					return GetInclusionStatesResponse.create(inclusionStates);
				}
			}
		}


		private static AbstractResponse findTransactionStatement(IDictionary<string, object> request)
		{

			HashSet<long?> bundlesTransactions = new HashSet<long?>();

			if (request.ContainsKey("bundles"))
			{
				foreach (string bundle in (IList<string>) request["bundles"])
				{
					bundlesTransactions.UnionWith(Storage.bundleTransactions(Storage.bundlePointer((new Hash(bundle)).Sbytes())));
				}
			}

			HashSet<long?> addressesTransactions = new HashSet<long?>();

			if (request.ContainsKey("addresses"))
			{
				foreach (string address in (IList<string>) request["addresses"])
				{
					addressesTransactions.UnionWith(Storage.addressTransactions(Storage.addressPointer((new Hash(address)).Sbytes())));
				}
			}


			HashSet<long?> tagsTransactions = new HashSet<long?>();
			if (request.ContainsKey("tags"))
			{
				foreach (string tag in (IList<string>) request["tags"])
				{
				    var thisTag = tag;
					while (thisTag.Length < Curl.HASH_LENGTH / Converter.NUMBER_OF_TRITS_IN_A_TRYTE)
					{
						thisTag += Converter.TRYTE_ALPHABET[0];
					}
					tagsTransactions.UnionWith(Storage.tagTransactions(Storage.tagPointer((new Hash(thisTag)).Sbytes())));
				}
			}

			HashSet<long?> approveeTransactions = new HashSet<long?>();
			;
			if (request.ContainsKey("approvees"))
			{
				foreach (string approvee in (IList<string>) request["approvees"])
				{
					approveeTransactions.UnionWith(Storage.approveeTransactions(Storage.approveePointer((new Hash(approvee)).Sbytes())));
				}
			}

		    // jesus...
			HashSet<long?> foundTransactions = (bundlesTransactions.Count == 0) ? ((addressesTransactions.Count == 0) ? ((tagsTransactions.Count == 0) ? ((approveeTransactions.Count == 0 )? new HashSet<long?>() : approveeTransactions) : tagsTransactions) : addressesTransactions) : bundlesTransactions;

			if (addressesTransactions != null)
			{
                foundTransactions = foundTransactions.Where(item => addressesTransactions.Contains(item)).ToHashSet();
			}
			if (tagsTransactions != null)
			{
                foundTransactions = foundTransactions.Where(item => tagsTransactions.Contains(item)).ToHashSet();
			}
			if (approveeTransactions != null)
			{
                foundTransactions = foundTransactions.Where(item => approveeTransactions.Contains(item)).ToHashSet();
			}


			List<string> elements = new List<string>();

			foreach (long pointer in foundTransactions)
			{
				elements.Add(new Hash(Storage.loadTransaction(pointer).hash, 0, Transaction.HASH_SIZE).ToString());
			}

			return FindTransactionsResponse.create(elements);
		}


		private static AbstractResponse broadcastTransactionStatement(IList<string> trytes2)
		{
			foreach (string tryte in trytes2)
			{

				Transaction transaction = new Transaction(Converter.trits(tryte));
				transaction.weightMagnitude = Curl.HASH_LENGTH;
				Node.broadcast(transaction);
			}

			return AbstractResponse.createEmptyResponse();
		}


		private static AbstractResponse getBalancesStatement(IList<string> addrss, int threshold)
		{

			if (threshold <= 0 || threshold > 100)
			{
				return ErrorResponse.create("Illegal 'threshold'");
			}

			List<Hash> addresses = new List<Hash>();

			foreach (string address in addrss)
			{
				addresses.Add((new Hash(address)));
			}

			IDictionary<Hash, long?> balances = new Dictionary<Hash, long?>();

			foreach (Hash address in addresses)
			{
				balances.Add(address, Snapshot.initialState.ContainsKey(address) ? Snapshot.initialState[address] : Convert.ToInt64(0));
			}

			Hash milestone = Milestone.latestSolidSubtangleMilestone;

			int milestoneIndex = Milestone.latestSolidSubtangleMilestoneIndex;

			lock (Storage.analyzedTransactionsFlags)
			{

				Storage.clearAnalyzedTransactionsFlags();

                List<long?> nonAnalyzedTransactions = new List<long?> { Storage.transactionPointer(milestone.Sbytes()) };

				long? pointer;
				while ((pointer = nonAnalyzedTransactions.Poll()) != null)
				{

					if (Storage.setAnalyzedTransactionFlag((long)pointer))
					{

						Transaction transaction = Storage.loadTransaction((long)pointer);

						if (transaction.value != 0)
						{

							Hash address = new Hash(transaction.address, 0, Transaction.ADDRESS_SIZE);

							long? balance = balances[address];
							if (balance != null)
							{

								balances.Add(address, balance + transaction.value);
							}
						}
						nonAnalyzedTransactions.Add(transaction.trunkTransactionPointer);
						nonAnalyzedTransactions.Add(transaction.branchTransactionPointer);
					}
				}
			}


			List<string> elements = new List<string>();

			foreach (Hash address in addresses)
			{
				elements.Add(balances[address].ToString());
			}

			return GetBalancesResponse.create(elements, milestone, milestoneIndex);
		}


		private static AbstractResponse attachToTangleStatement(Hash trunkTransaction, Hash branchTransaction, int minWeightMagnitude, IList<string> trytes)
		{

			List<Transaction> transactions = new List<Transaction>();

			Hash prevTransaction = null;

			foreach (string tryte in trytes)
			{

				int[] transactionTrits = Converter.trits(tryte);
				Array.Copy((prevTransaction == null ? trunkTransaction : prevTransaction).Trits(), 0, transactionTrits, Transaction.TRUNK_TRANSACTION_TRINARY_OFFSET, Transaction.TRUNK_TRANSACTION_TRINARY_SIZE);
				Array.Copy((prevTransaction == null ? branchTransaction : trunkTransaction).Trits(), 0, transactionTrits, Transaction.BRANCH_TRANSACTION_TRINARY_OFFSET, Transaction.BRANCH_TRANSACTION_TRINARY_SIZE);

				if (pearlDiver.search(transactionTrits, minWeightMagnitude, 0))
				{
					transactions.Clear();
					break;
				}

				Transaction transaction = new Transaction(transactionTrits);
				transactions.Add(transaction);
				prevTransaction = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
			}


			List<string> elements = new List<string>();

			for (int i = transactions.Count; i-- > 0;)
			{
				elements.Add("\"" + Converter.trytes(transactions[i].Trits()) + "\"");
			}
			return AttachToTangleResponse.create(elements);
		}


		private static AbstractResponse addNeighborsStatement(IList<string> uris)
		{
		    try
		    {
		        int numberOfAddedNeighbors = 0;

		        foreach (string uriString in uris)
		        {

		            Uri uri = new Uri(uriString);

		            if (uri.Scheme != null && uri.Scheme.Equals("udp"))
		            {
		                var ipEndPoint = new IPEndPoint(Dns.GetHostEntry(uri.Host).AddressList[0], uri.Port);
		                SocketAddress socketAddress = ipEndPoint.Serialize();

		                Neighbor neighbor = new Neighbor(socketAddress);

		                if (!Node.neighbors.Contains(neighbor))
		                {

		                    Node.neighbors.Add(neighbor);
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


		private static void sendResponse(HttpListenerResponse exchange, AbstractResponse res, long beginningTime)
		{
		    try
		    {
		        res.Duration = (int) (DateTimeExtensions.currentTimeMillis() - beginningTime);

                // string response = gson.toJson(res);
                string response = JsonConvert.SerializeObject(res);

                //exchange.ResponseChannel.write(ByteBuffer.wrap(response.getBytes(StandardCharsets.UTF_8)));
		        // exchange.endExchange();

                byte[] utf8ResponseBytes = Encoding.UTF8.GetBytes(response);

                exchange.Headers.Add("Content-Type", "application/json; charset=utf-8");
                exchange.Headers.Add("Access-Control-Allow-Origin", "*");

                exchange.StatusCode = (int)HttpStatusCode.OK;
                // Get a response stream and write the response to it.
                exchange.ContentLength64 = utf8ResponseBytes.Length;
                // System.IO.Stream output = response.OutputStream;

                using (Stream stream = exchange.OutputStream)
                {   // also BufferedStream supports byte[]
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write(utf8ResponseBytes);
                    }
                }
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		public static void shutDown()
		{
			if (server != null)
			{
                server.Stop();
                server.Close();
			}
		}
	}

}