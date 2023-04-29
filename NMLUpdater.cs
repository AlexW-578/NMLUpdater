using System.IO;
using System.Net;
using NeosModLoader;
using Newtonsoft.Json.Linq;


namespace NMLUpdater
{
    public class NMLUpdater : NeosMod
    {
        public override string Name => "NMLUpdater";
        public override string Author => "AlexW-578";
        public override string Version => "1.0.1";
        public override string Link => "https://github.com/AlexW-578/NMLUpdater";

        private class NeosModUpdate
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string NewVersion { get; set; }
            public string OldVersion { get; set; }
            public string Sha256 { get; set; }
        }


        private static ModConfiguration Config;

        [AutoRegisterConfigKey] private static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "Enable/Disable the Mod", () => true);


        public override void OnEngineInit()
        {
            Config = GetConfiguration();
            Config.Save(true);
            if (!Config.GetValue(Enabled))
            {
                return;
            }

            const string manifestUrl =
                "https://raw.githubusercontent.com/neos-modding-group/neos-mod-manifest/master/manifest.json";
            string manifest = GetManifest((manifestUrl));
            GetVersionsFromManifest(JObject.Parse(manifest));
        }


        private static void GetVersionsFromManifest(JObject manifestJson)
        {
            foreach (NeosModBase neosMod in ModLoader.Mods())
            {
                NeosModUpdate modUpdate = new NeosModUpdate();
                JToken modList = manifestJson["mods"];
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

                        // These are here due to the fact that some people are not consistant on how they use version numbers
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
                            Warn(
                                $"\n{modUpdate.Name}: New Version:{modUpdate.NewVersion} - Old Version: {modUpdate.OldVersion}\nUrl: {modUpdate.Url}\nSha256: {modUpdate.Sha256}");
                        }
                    }
                }
            }
        }

        private static string GetManifest(string url)
        {
            WebRequest request = HttpWebRequest.Create(url);

            WebResponse response = request.GetResponse();

            StreamReader reader = new StreamReader(response.GetResponseStream());

            string responseText = reader.ReadToEnd();
            return responseText;
        }
    }
}