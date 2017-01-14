using System;
using System.Threading;
using slf4net;

namespace com.iota.iri
{

	using API = com.iota.iri.service.API;
	using Node = com.iota.iri.service.Node;
	using Storage = com.iota.iri.service.Storage;
	using TipsManager = com.iota.iri.service.TipsManager;

    ///
    /// <summary> * Main IOTA Reference Implementation starting class </summary>
    /// 
	public class IRI
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(IRI));

		public const string NAME = "IRI";
		public const string VERSION = "1.1.0";

		static void Main(string[] args)
		{

			log.Info("Welcome to {} {}", NAME, VERSION);

			validateParams(args);

			try
			{

				Storage.launch();
				Node.launch(args);
				TipsManager.launch();
				API.launch();

			}
			catch (Exception e)
			{
				log.Error("Exception during IOTA node initialisation: ", e);
			}
		}


		private static void validateParams(string[] args)
		{
            
			if (args.Length > 0 && string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase))
			{
				log.Info("Usage: java -jar {}-{}.jar [port number] <list of neighbors>", NAME, VERSION);
				Environment.Exit(0);
			}

			if (args.Length < 2)
			{
				log.Error("Invalid arguments list. Provide port number and at least one udp node address.");
                throw new InvalidOperationException();
			}

			if (Convert.ToInt32(args[0]) < 1024)
			{
				log.Warn("Warning: port value seems too low.");
			}
		}

		static IRI()
		{
			  AppDomain.CurrentDomain.ProcessExit += (s, ea) =>
			  {
			    
			          log.Info("Shutting down IOTA node, please hold tight...");
			          try
			          {
			              API.shutDown();
			              TipsManager.shutDown();
			              Node.shutDown();
			              Storage.shutDown();

			          }
			          catch (Exception e)
			          {
			              log.Error("Exception occurred shutting down IOTA node: ", e);
			          }
			      
			  };
		}
	}

}