using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
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

        private static List<NeosModUpdate> ModsThatNeedUpdating = new List<NeosModUpdate>();

        private class NeosModUpdate
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public bool NeedsUpdate { get; set; }
            public string NewVersion { get; set; }
            public string Sha256 { get; set; }
            public NeosModBase Mod { get; set; }
        }


        private static ModConfiguration Config;

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> AutoUpdate =
            new ModConfigurationKey<bool>("AutoUpdate", "Auto-Update Mods", () => true);

        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);

            const string manifestUrl =
                "https://raw.githubusercontent.com/neos-modding-group/neos-mod-manifest/master/manifest.json";
            string manifest = GetManifest((manifestUrl));
            JObject manifestJson = JObject.Parse(manifest);
            GetModList("./nml_mods", manifestJson);
            Harmony harmony = new Harmony("co.uk.alexw-578.NMLUpdater");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(Engine), "Dispose")]
        class ShutdownPatch
        {
            static bool Prefix()
            {
                Warn("Updating Mods:");
                if (ModsThatNeedUpdating.Count != 0)
                {
                    foreach (NeosModUpdate mod in ModsThatNeedUpdating)
                    {
                        Warn(mod.Name);
                        Warn($"Old Version:{mod.Mod.Version} -> New Version:{mod.NewVersion}");
                        Warn($"URL: {mod.Url}");
                    }
                }

                return true;
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
                if (neosModUpdate.NeedsUpdate)
                {
                    ModsThatNeedUpdating.Add(neosModUpdate);
                    Warn(
                        $"{neosModUpdate.Mod}: {neosModUpdate.NeedsUpdate} - New Version:{neosModUpdate.NewVersion} - Old Version: {neosModUpdate.Mod.Version}");
                }
            }
        }

        private static NeosModUpdate GetVersionsFromManifest(JObject manifestJson, NeosModBase neosMod)
        {
            NeosModUpdate modUpdate = new NeosModUpdate();
            JToken modList = manifestJson["mods"];
            modUpdate.NeedsUpdate = false;
            modUpdate.Name = neosMod.Name;
            modUpdate.Mod = neosMod;
            foreach (JToken mod in modList.Children())
            {
                if (mod.ToString().Contains(neosMod.Name) & mod.ToString().Contains(neosMod.Author))
                {
                    int versions = 0;
                    foreach (JToken version in mod.First["versions"])
                    {
                        string newVersionStr = version.ToString().Split('"')[1];
                        int newVersion = int.Parse(newVersionStr.Replace(".", ""));
                        if (versions < newVersion)
                        {
                            versions = newVersion;
                            modUpdate.NewVersion = newVersionStr;
                            modUpdate.Url = (string)version.First["artifacts"][0]["url"];
                            modUpdate.Sha256 = (string)version.First["artifacts"][0]["sha256"];
                        }
                        else
                        {
                            break;
                        }
                    }

                    string oldVersion = neosMod.Version;
                    if (neosMod.Version.Length > modUpdate.NewVersion.Length)
                    {
                        oldVersion = neosMod.Version.Substring(0, modUpdate.NewVersion.Length);
                    }

                    if (modUpdate.NewVersion.Length > oldVersion.Length)
                    {
                        modUpdate.NewVersion = modUpdate.NewVersion.Substring(0, oldVersion.Length);
                    }

                    if (int.Parse(modUpdate.NewVersion.Replace(".", "")) > int.Parse(oldVersion.Replace(".", "")))
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

        private static void DownloadDll(string url, string destinationFile)
        {
            WebRequest request = HttpWebRequest.Create(url);

            WebResponse response = request.GetResponse();

            StreamReader reader = new StreamReader(response.GetResponseStream());

            string responseText = reader.ReadToEnd();

            File.WriteAllText(destinationFile, responseText);
        }
    }
}