using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

namespace Darkmatter.Core
{
    [Serializable]
    public struct PrizeClaim
    {
        public string riderName;
        public string phoneNumber;


        public string claimedAtUtc;
    }


    public static class PrizeClaimStore
    {
        private const string FileName = "prize-claims.json";


        [Serializable]
        private class Ledger
        {
            public List<PrizeClaim> claims = new List<PrizeClaim>();
        }

        private static Ledger ledger;


        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);


        public static IReadOnlyList<PrizeClaim> Claims
        {
            get
            {
                Load();
                return ledger.claims;
            }
        }


        public static bool Add(string riderName, string phoneNumber, out PrizeClaim stored)
        {
            stored = default;

            riderName = (riderName ?? string.Empty).Trim();
            phoneNumber = (phoneNumber ?? string.Empty).Trim();

            if (riderName.Length == 0 || phoneNumber.Length == 0)
            {
                return false;
            }

            Load();

            stored = new PrizeClaim
            {
                riderName = riderName,
                phoneNumber = phoneNumber,
                claimedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            ledger.claims.Add(stored);
            Save();
            return true;
        }


        private static void Load()
        {
            if (ledger != null)
            {
                return;
            }

            ledger = new Ledger();

            string path = FilePath;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                Ledger read = JsonUtility.FromJson<Ledger>(File.ReadAllText(path));
                if (read != null && read.claims != null)
                {
                    ledger = read;
                    return;
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"Prize claims at {path} could not be read: {error.Message}");
            }


            try
            {
                string aside = path + ".unreadable";
                File.Delete(aside);
                File.Move(path, aside);
                Debug.LogWarning($"Prize claims moved aside to {aside}, starting a fresh list.");
            }
            catch (Exception error)
            {
                Debug.LogError($"Prize claims at {path} could not be moved aside: {error.Message}");
            }
        }

        private static void Save()
        {
            string path = FilePath;

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(ledger, true));
            }
            catch (Exception error)
            {
                Debug.LogError($"Prize claims could not be written to {path}: {error.Message}");
            }
        }
    }
}
