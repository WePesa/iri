using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using slf4net;

// 1.1.2.3

namespace com.iota.iri.conf
{

    ///
    /// <summary> * All those settings are modificable at runtime,
    /// * but for most of them the node needs to be restarted. </summary>
    /// 
	public class Configuration
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(Configuration));

		private static readonly IDictionary<string, string> conf = new ConcurrentDictionary<string, string>();

		public enum DefaultConfSettings
		{
			API_PORT,
			API_HOST,
			TANGLE_RECEIVER_PORT,
			CORS_ENABLED,
			TESTNET, // not used yet
			HEADLESS,
			REMOTEAPILIMIT,
			NEIGHBORS,
			DEBUG,
			EXPERIMENTAL // experimental features.
		}

		static Configuration()
		{
		// defaults
			conf.Add(DefaultConfSettings.API_PORT.name(), "14265");
			conf.Add(DefaultConfSettings.API_HOST.name(), "localhost");
			conf.Add(DefaultConfSettings.TANGLE_RECEIVER_PORT.name(), "14265");
			conf.Add(DefaultConfSettings.CORS_ENABLED.name(), "*");
			conf.Add(DefaultConfSettings.TESTNET.name(), "false");
			conf.Add(DefaultConfSettings.HEADLESS.name(), "false");
			conf.Add(DefaultConfSettings.DEBUG.name(), "false");
			conf.Add(DefaultConfSettings.REMOTEAPILIMIT.name(), "");
			conf.Add(DefaultConfSettings.EXPERIMENTAL.name(), "false");
		}

		public static string allSettings()
		{
			StringBuilder settings = new StringBuilder();
			conf.Keys.forEach(t -> settings.Append("Set '").append(t).append("'\t -> ").append(conf[t]).append("\n"));
			return settings.ToString();
		}

		public static void put(string k, string v)
		{
			log.Debug("Setting {} with {}", k, v);
			conf.Add(k, v);
		}

		public static void put(DefaultConfSettings d, string v)
		{
			log.Debug("Setting {} with {}", d.name(), v);
			conf.Add(d.name(), v);
		}

		public static string @string(string k)
		{
			return conf[k];
		}

		public static float floating(string k)
		{
			return Convert.ToSingle(conf[k]);
		}

		public static double doubling(string k)
		{
			return Convert.ToDouble(conf[k]);
		}

		public static int integer(string k)
		{
			return Convert.ToInt32(conf[k]);
		}

		public static bool booling(string k)
		{
			return Convert.ToBoolean(conf[k]);
		}

		public static string @string(DefaultConfSettings d)
		{
			return @string(d.name());
		}

		public static int integer(DefaultConfSettings d)
		{
			return integer(d.name());
		}

		public static bool booling(DefaultConfSettings d)
		{
			return booling(d.name());
		}
	}

}