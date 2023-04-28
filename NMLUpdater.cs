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

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> AutoSpawnList =
            new ModConfigurationKey<bool>("AutoSpawnList", "Automatically Spawns a List of updated mods in userspace.",
                () => false);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> ListToFile =
            new ModConfigurationKey<bool>("ModListToFile",
                "Create a json file with all the mods that needs updating.",
                () => true);

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> SpawnProgram =
            new ModConfigurationKey<bool>("SpawnProgram", "Launches a External Program To update mods.",
                () => false);

        private static string SpawnProgramDir = "./nml_updater/updater.exe";

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
                SpawnProgramDir = "./nml_updater/updater.sh";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SpawnProgramDir = "./nml_updater/updater.exe";
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
                        $"{neosModUpdate.Name}: New Version:{neosModUpdate.NewVersion} - Old Version: {neosModUpdate.OldVersion}");
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

        [HarmonyPatch(typeof(Userspace), "OnAttach")]
        class ModSettingsScreen
        {
            [HarmonyPostfix]
            public static void Postfix(Userspace __instance)
            {
                if (Config.GetValue(AutoSpawnList))
                {
                    Slot root = __instance.World.AddSlot("ModList");
                    SpawnModList(root);
                }
            }

            private static void SpawnModList(Slot root)
            {
                root.LocalPosition = new float3(0f, 1.5f, 1f);
                root.AttachComponent<Grabbable>();
                Slot slot = root.AddSlot("Mod Update List");
                slot.Tag = "ModList";
                var ui = new UIBuilder(slot, 600f, 1000f, 0.0005f);
                ui.Panel(color.LightGray, true);
                ui.Style.ForceExpandHeight = false;
                ui.VerticalLayout(8f, 8f, Alignment.TopCenter);
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
                ui.HorizontalHeader(200f, out RectTransform header, out RectTransform content);
                ui.NestInto(header);
                ui.NestInto(ui.Empty("Info"));
                ui.Style.PreferredHeight = 200f;
                ui.Image(color.DarkGray);
                ui.Style.PreferredHeight = 200f;
                ui.Text(
                    $"<color=#ffffff>If setup in the config just restart Neos to update the mods.\nOtherwise rename the [modName].dll.updated to just [modName].dll\n And Replace the old version.</color>",
                    true,
                    Alignment.MiddleCenter);
                ui.NestOut();
                ui.NestInto(content);
                ui.SetFixedHeight(750f);
                ui.ScrollArea();
                ui.VerticalLayout(8f, 8f, Alignment.TopCenter);
                BuildModUpdaterItems(ui);
            }

            private static void BuildModUpdaterItems(UIBuilder ui)
            {
                ui.Style.PreferredHeight = 32f;
                foreach (NeosModUpdate mod in ModsThatNeedUpdating)
                {
                    ui.NestInto(ui.Empty(mod.Name));
                    ui.Style.PreferredHeight = 32f;
                    ui.Image(color.DarkGray);
                    ui.Style.PreferredHeight = 32f;
                    ui.Text(
                        $"<color=#ffffff>I{mod.Name}: Old Version:{mod.OldVersion} -> New Version:{mod.NewVersion}</color>",
                        true,
                        Alignment.MiddleCenter);
                    ui.NestOut();
                }
            }
        }

        private static void StartExternalProgram()
        {
            Msg("Starting External Mod Updater");
            Process.Start(SpawnProgramDir);
        }
    }
}