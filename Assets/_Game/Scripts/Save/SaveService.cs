using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Starbind.Save
{
    /// <summary>Local JSON save (GDD §8.4, §14.1, TECH_ARCHITECTURE §9): versioned, checksummed,
    /// atomic write (tmp -> replace, keeps .bak), falls back to the backup and then to defaults instead
    /// of crashing on a damaged or mismatched file.</summary>
    public sealed class SaveService
    {
        [Serializable]
        sealed class Envelope
        {
            public int saveVersion;
            public string checksum;
            public string payload;
        }

        public readonly string Path;
        public string LastError { get; private set; }
        public bool LoadedFromBackup { get; private set; }

        public SaveService(string directory, string fileName = "save_v1.json")
        {
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, fileName);
        }

        public static string Checksum(string s)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(s))
                h = (h ^ b) * 16777619;
            return h.ToString("x8");
        }

        public void Save(ProfileData data)
        {
            data.saveVersion = ProfileData.CurrentVersion;
            string payload = JsonUtility.ToJson(data);
            var env = new Envelope { saveVersion = data.saveVersion, payload = payload, checksum = Checksum(payload) };
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(env, true));
            if (File.Exists(Path))
            {
                string bak = Path + ".bak";
                if (File.Exists(bak))
                    File.Delete(bak);
#if UNITY_WEBGL && !UNITY_EDITOR
                // browser file system (IDBFS): plain moves instead of File.Replace
                File.Move(Path, bak);
                File.Move(tmp, Path);
#else
                File.Replace(tmp, Path, bak);
#endif
            }
            else
                File.Move(tmp, Path);
#if UNITY_WEBGL && !UNITY_EDITOR
            StarbindSyncFileSystem();                         // flush persistentDataPath to IndexedDB now
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void StarbindSyncFileSystem();
#endif

        /// <summary>Never throws: returns defaults when nothing valid exists.</summary>
        public ProfileData Load()
        {
            LoadedFromBackup = false;
            LastError = null;
            if (TryRead(Path, out var data))
                return data;
            if (TryRead(Path + ".bak", out data))
            {
                LoadedFromBackup = true;
                return data;
            }
            return new ProfileData();
        }

        bool TryRead(string file, out ProfileData data)
        {
            data = null;
            try
            {
                if (!File.Exists(file))
                    return false;
                var env = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
                if (env == null || string.IsNullOrEmpty(env.payload))
                    throw new InvalidDataException("empty envelope");
                if (Checksum(env.payload) != env.checksum)
                    throw new InvalidDataException("checksum mismatch");
                if (env.saveVersion > ProfileData.CurrentVersion)
                    throw new InvalidDataException($"save version {env.saveVersion} is newer than {ProfileData.CurrentVersion}");
                data = new ProfileData(); // fields missing from older saves keep their defaults
                JsonUtility.FromJsonOverwrite(env.payload, data);
                Migrate(data, env.saveVersion);
                return data != null;
            }
            catch (Exception e)
            {
                LastError = file + ": " + e.Message;
                Debug.LogWarning("[SaveService] " + LastError);
                return false;
            }
        }

        static void Migrate(ProfileData data, int fromVersion)
        {
            // v1 is the first format; future versions add steps here (fromVersion -> CurrentVersion).
            data.saveVersion = ProfileData.CurrentVersion;
            data.unlockedSpells ??= new System.Collections.Generic.List<string>();
            data.autoSpells ??= new System.Collections.Generic.List<string>();
            data.completedQuests ??= new System.Collections.Generic.List<string>();
            data.screenFlash = UnityEngine.Mathf.Clamp01(data.screenFlash);
        }
    }
}
