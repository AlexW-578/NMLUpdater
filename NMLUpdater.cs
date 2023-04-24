using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using BaseX;
using FrooxEngine;
using FrooxEngine.LogiX;
using HarmonyLib;
using NeosModLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NMLUpdater
{
	public class NMLUpdater : NeosMod
	{
		public override string Name => "NMLUpdater";
		public override string Author => "AlexW-578";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/AlexW-578/CustomLogixBrowser";

		public class NeosModUpdate
		{
			public string Name { get; set; }
			public string Url { get; set; }
			public bool NeedsUpdate { get; set; }
			public string NewVersion { get; set; }

		}



		private static ModConfiguration Config;

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> enabled =
			new ModConfigurationKey<bool>("enabled", "Enables the mod", () => true);

		public override void OnEngineInit()
		{
			Config = GetConfiguration();
			Config.Save(true);

			const string fileName = @"C:\Users\Alex\Desktop\Neos\app\nml_mods\CustomLogixBrowser.dll";
			const string manifestUrl =
				"https://raw.githubusercontent.com/neos-modding-group/neos-mod-manifest/master/manifest.json";
			string manifest = GetManifest((manifestUrl));
			JObject manifestJson = JObject.Parse(manifest);
			GetModList("./nml_mods", manifestJson);
			Harmony harmony = new Harmony("net.dfgHiatus.AutoImageResize");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(ModConfiguration), "ShutdownHook")]
		class ShutdownPatch
		{
			static void Postfix()
			{
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
				Warn("AlexW-578 Was Here!!!!!!!!!");
			}
		}

		private static string GetChecksumBuffered(String fileName)
		{
			var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Read);
			using (var bufferedStream = new BufferedStream(stream, 1024 * 32))
			{
				var sha = new SHA256Managed();
				byte[] checksum = sha.ComputeHash(bufferedStream);

				return BitConverter.ToString(checksum).Replace("-", String.Empty);
			}
		}

		private static void GetModList(string modDir, JObject manifestJson)
		{
			foreach (NeosModBase mod in ModLoader.Mods())
			{
				NeosModUpdate neosModUpdate = GetVersionsFromManifest(manifestJson, mod);
				Warn(neosModUpdate.Name +" "+ neosModUpdate.NeedsUpdate +" "+ neosModUpdate.Url+" "+neosModUpdate.NewVersion);
			}

		}

		private static NeosModUpdate GetVersionsFromManifest(JObject manifestJson, NeosModBase neosMod)
		{

			NeosModUpdate modUpdate = new NeosModUpdate();
			JToken modList = manifestJson["mods"];
			modUpdate.NeedsUpdate = false;
			modUpdate.Name = neosMod.Name;
			foreach (JToken mod in modList.Children())
			{
				if (mod.ToString().Contains(neosMod.Name) & mod.ToString().Contains(neosMod.Author))
				{
					modUpdate.Url =(string) mod.First["versions"].First.First["artifacts"][0]["url"];
					modUpdate.NewVersion = (string)mod.First["versions"].First.ToString().Split('"')[1];
					if (modUpdate.NewVersion.Equals(neosMod.Version) == false)
					{
						modUpdate.NeedsUpdate = true;
					}
				}

			}
			return modUpdate;
		}


		private static string GetManifest(string url)
		{   
  
			WebRequest request = HttpWebRequest.Create(url);  
  
			WebResponse response = request.GetResponse();  
  
			StreamReader reader = new StreamReader(response.GetResponseStream());  
  
			string responseText = reader.ReadToEnd();
			return responseText;
		}

		private static void DownloadDll(string url,string destinationFile)
		{
			WebRequest request = HttpWebRequest.Create(url);  
  
			WebResponse response = request.GetResponse();  
  
			StreamReader reader = new StreamReader(response.GetResponseStream());
			
			string responseText = reader.ReadToEnd();
 
			File.WriteAllText(destinationFile, responseText);
		}
	}
}