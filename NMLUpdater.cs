using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using FrooxEngine;
using FrooxEngine.LogiX;
using HarmonyLib;
using NeosModLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BaseX;
using FrooxEngine.LogiX.WorldModel;
using FrooxEngine.UIX;

namespace NMLUpdater
{
    public class NMLUpdater : NeosMod
    {
        public override string Name => "NMLUpdater";
        public override string Author => "AlexW-578";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/AlexW-578/NMLUpdater";

        private static List<NeosModUpdate> ModsThatNeedUpdating = new List<NeosModUpdate>();

        public class NeosModUpdate
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public bool NeedsUpdate { get; set; }
            public string NewVersion { get; set; }
            public string OldVersion { get; set; }

            public string Sha256 { get; set; }
        }


        private static ModConfiguration Config;

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "Enable/Disable the Mod", () => true);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> ListToFile =
            new ModConfigurationKey<bool>("ModListToFile",
                "Create a json file with all the mods that needs updating.",
                () => false);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> SpawnProgram =
            new ModConfigurationKey<bool>("SpawnProgram",
                "Launches a External Program To update mods.\nMust have ModListToFile Enabled.",
                () => false);

        private static string SpawnProgramDir = "\\nml_updater\\updater.exe";
        private static bool _isSpawned = false;
        
        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);
            if (!Config.GetValue(Enabled))
            {
                return;
            }

            Harmony harmony = new Harmony("co.uk.alexw-578.NMLUpdater");
            const string manifestUrl =
                "https://raw.githubusercontent.com/neos-modding-group/neos-mod-manifest/master/manifest.json";
            string manifest = GetManifest((manifestUrl));
            JObject manifestJson = JObject.Parse(manifest);
            GetModList("./nml_mods", manifestJson);
            if (Config.GetValue(ListToFile))
            {
                if (Directory.Exists("./nm_updater") == false)
                {
                    Directory.CreateDirectory("./nml_updater");
                }

                string text = "";
                foreach (NeosModUpdate mod in ModsThatNeedUpdating)
                {
                    text += $"{JsonConvert.SerializeObject(mod)}\n";
                }


                File.WriteAllText("./nml_updater/mods.json", text);
            }

            if (Config.GetValue(SpawnProgram))
            {
                Platform();
                Engine.Current.OnShutdownRequest += _ => { StartExternalProgram(); };
            }

            harmony.PatchAll();
        }

        private void Platform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                SpawnProgramDir = "/nml_updater/updater.sh";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SpawnProgramDir = "\\nml_updater\\updater.exe";
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
                    Warn($"{neosModUpdate.Name}: New Version:{neosModUpdate.NewVersion} - Old Version: {neosModUpdate.OldVersion}");
                }
            }
        }

        private static NeosModUpdate GetVersionsFromManifest(JObject manifestJson, NeosModBase neosMod)
        {
            NeosModUpdate modUpdate = new NeosModUpdate();
            JToken modList = manifestJson["mods"];
            modUpdate.NeedsUpdate = false;
            modUpdate.Name = neosMod.Name;
            modUpdate.OldVersion = neosMod.Version;
            foreach (JToken mod in modList.Children())
            {
                if (mod.Path.Contains(neosMod.Name) & mod.ToString().Contains(neosMod.Author))
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

        private static void StartExternalProgram()
        {
            Msg("Starting External Mod Updater");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (ModsThatNeedUpdating.Count > 0)
                {
                    if (_isSpawned == false)
                    {
                        var updaterArgs = " --neos-dir " + Directory.GetCurrentDirectory();
                        var cmdArgs = "-u " + Directory.GetCurrentDirectory() + SpawnProgramDir + " " + updaterArgs;
                        Warn(cmdArgs);
                        _isSpawned = true;
                        Process.Start(new ProcessStartInfo("bash", cmdArgs));
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (ModsThatNeedUpdating.Count > 0)
                {
                    if (_isSpawned == false)
                    {
                        var updaterArgs = " --neos-dir " + Directory.GetCurrentDirectory();
                        var cmdArgs = "/c " + Directory.GetCurrentDirectory() + SpawnProgramDir + " " + updaterArgs;
                        Warn(cmdArgs);
                        _isSpawned = true;
                        Process.Start(new ProcessStartInfo("cmd", cmdArgs));
                    }
                }
            }
        }
    }
}