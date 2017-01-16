using System;
using System.IO;
using System.Threading;
using com.iota.iri.conf;
using slf4net;

// 1.1.2.3

namespace com.iota.iri
{


    using StringUtils = org.apache.commons.lang3.StringUtils;
    using Logger = org.slf4j.Logger;
    using LoggerFactory = org.slf4j.LoggerFactory;

    using Configuration = com.iota.iri.conf.Configuration;
    using DefaultConfSettings = com.iota.iri.conf.Configuration.DefaultConfSettings;
    using API = com.iota.iri.service.API;
    using Node = com.iota.iri.service.Node;
    using TipsManager = com.iota.iri.service.TipsManager;
    using Storage = com.iota.iri.service.storage.Storage;
    using CmdLineParser = com.sanityinc.jargs.CmdLineParser;
    using Option = com.sanityinc.jargs.CmdLineParser.Option;

    using LoggerContext = ch.qos.logback.classic.LoggerContext;
    using StatusPrinter = ch.qos.logback.core.util.StatusPrinter;

    ///
    /// <summary> * Main IOTA Reference Implementation starting class </summary>
    /// 
    public class IRI
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(IRI));

        public const string NAME = "IRI";
        public const string VERSION = "1.1.2.3";


        static void Main(string[] args)
        {

            log.Info("Welcome to {} {}", NAME, VERSION);
            validateParams(args);
            shutdownHook();

            if (!Configuration.booling(DefaultConfSettings.HEADLESS))
            {
                showIotaLogo();
            }

            try
            {

                Storage.instance().init();
                Node.instance().init();
                TipsManager.instance().init();
                API.instance().init();

            }

            catch (Exception e)
            {
                log.Error("Exception during IOTA node initialisation: ", e);
                Environment.Exit(-1);
            }
            log.Info("IOTA Node initialised correctly.");
        }


        private static void validateParams(string[] args)
        {

            if (args == null || args.Length < 2)
            {
                log.Error("Invalid arguments list. Provide Api port number (i.e. '-p 14265').");
                printUsage();
            }

            CmdLineParser parser = new CmdLineParser();

            Option<string> port = parser.addStringOption('p', "port");          
            Option<string> rport = parser.addStringOption('r', "receiver-port");
            Option<string> cors = parser.addStringOption('c', "enabled-cors");
            Option<bool?> headless = parser.addBooleanOption("headless");
            Option<bool?> debug = parser.addBooleanOption('d', "debug");
            Option<bool?> remote = parser.addBooleanOption("remote");
            Option<string> remoteLimitApi = parser.addStringOption("remote-limit-api");
            Option<string> neighbors = parser.addStringOption('n', "neighbors");
            Option<bool?> experimental = parser.addBooleanOption('e', "experimental");
            Option<bool?> help = parser.addBooleanOption('h', "help");

            try
            {
                parser.parse(args);
            }
            catch (CmdLineParser.OptionException e)
            {
                log.Error("CLI error: ", e);
                printUsage();
                Environment.Exit(2);
            }

            // mandatory args
            string cport = parser.getOptionValue(port);
            if (cport == null)
            {
                log.Error("Invalid arguments list. Provide at least 1 neighbor with -n or --neighbors '<list>'");
                printUsage();
            }
            Configuration.put(DefaultConfSettings.API_PORT, cport);

            // optional flags
            if (parser.getOptionValue(help) != null)
            {
                printUsage();
            }

            string cns = parser.getOptionValue(neighbors);
            if (cns == null)
            {
                log.Warn("No neighbor has been specified. Server starting nodeless.");
                cns = StringUtils.EMPTY;
            }
            Configuration.put(DefaultConfSettings.NEIGHBORS, cns);

            string vcors = parser.getOptionValue(cors);
            if (vcors != null)
            {
                log.Debug("Enabled CORS with value : {} ", vcors);
                Configuration.put(DefaultConfSettings.CORS_ENABLED, vcors);
            }

            string vremoteapilimit = parser.getOptionValue(remoteLimitApi);
            if (vremoteapilimit != null)
            {
                log.Debug("The following api calls are not allowed : {} ", vremoteapilimit);
                Configuration.put(DefaultConfSettings.REMOTEAPILIMIT, vremoteapilimit);
            }

            string vrport = parser.getOptionValue(rport);
            if (vrport != null)
            {
                Configuration.put(DefaultConfSettings.TANGLE_RECEIVER_PORT, vrport);
            }

            if (parser.getOptionValue(headless) != null)
            {
                Configuration.put(DefaultConfSettings.HEADLESS, "true");
            }

            if (parser.getOptionValue(remote) != null)
            {
                log.Info("Remote access enabled. Binding API socket to listen any interface.");
                Configuration.put(DefaultConfSettings.API_HOST, "0.0.0.0");
            }

            if (parser.getOptionValue(experimental) != null)
            {
                log.Info("Experimental IOTA features turned on.");
                Configuration.put(DefaultConfSettings.EXPERIMENTAL, "true");
            }

            if (Convert.ToInt32(cport) < 1024)
            {
                log.Warn("Warning: api port value seems too low.");
            }

            if (parser.getOptionValue(debug) != null)
            {
                Configuration.put(DefaultConfSettings.DEBUG, "true");
                log.Info(Configuration.allSettings());
                StatusPrinter.print((LoggerContext)LoggerFactory.ILoggerFactory);
            }
        }

        private static void printUsage()
        {
            // + "[{-t,--testnet} false] " // -> TBDiscussed (!)
            log.Info("Usage: java -jar {}-{}.jar " + "[{-p,--port} 14265] " + "[{-r,--receiver-port} 14265] " + "[{-c,--enabled-cors} *] " + "[{-h}] [{--headless}] " + "[{-d,--debug}] " + "[{-e,--experimental}]" + "[{--remote}]" + "[{-n,--neighbors} '<list of neighbors>'] ", NAME, VERSION);
            Environment.Exit(0);
        }

        private static void shutdownHook()
		{
			Runtime.Runtime.addShutdownHook(new Thread(() => { log.Info("Shutting down IOTA node, please hold tight..."); try { API.instance().shutDown(); TipsManager.instance().shutDown(); Node.instance().shutdown(); Storage.instance().shutdown(); } catch (Exception e) { log.Error("Exception occurred shutting down IOTA node: ", e); } }));
		}

        private static void showIotaLogo()
		{
			const string charset = "UTF8";

			try
			{
				Path path = Paths.get("logo.utf8.ans");
				Files.readAllLines(path, Charset.forName(charset)).forEach(log::info);
			}
			catch (IOException e)
			{
				log.Error("Impossible to display logo. Charset {} not supported by terminal.", charset);
			}
		}
    }

}