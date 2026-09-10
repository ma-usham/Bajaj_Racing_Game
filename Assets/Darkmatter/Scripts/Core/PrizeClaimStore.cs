using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>One rider's go at the prize: what they typed, and when they typed it.</summary>
[Serializable]
public struct PrizeClaim
{
    public string riderName;
    public string phoneNumber;

    /// <summary>
    /// Round trip ISO 8601, in UTC. Sorts correctly as plain text and reads the same wherever
    /// the file ends up, which a local time string does not.
    /// </summary>
    public string claimedAtUtc;
}

/// <summary>
/// Where the prize claims are kept.
///
/// A file rather than PlayerPrefs, because this is a list that grows: every rider who finishes a
/// lap and fills the form in adds one, and a machine on a stand sees a lot of riders in a day.
/// PlayerPrefs holds one string per key, so a growing list there means reading and rewriting the
/// whole thing under one key anyway, with none of a file's advantages - a file can be copied off
/// the device and opened by someone who is not holding this project.
///
/// Written out on every claim rather than once at the end. A machine on a stand gets switched
/// off at the wall, and a claim that only ever existed in memory is a rider who filled the form
/// in for nothing.
/// </summary>
public static class PrizeClaimStore
{
    private const string FileName = "prize-claims.json";

    /// <summary>
    /// JsonUtility will not serialise a bare list at the top level, so the list is wrapped in
    /// something it will. It also leaves room to put a version or a machine id in the file later
    /// without every claim carrying its own copy.
    /// </summary>
    [Serializable]
    private class Ledger
    {
        public List<PrizeClaim> claims = new List<PrizeClaim>();
    }

    private static Ledger ledger;

    /// <summary>
    /// Where the file lives. Worth logging on a machine you cannot poke at, because
    /// persistentDataPath is somewhere different on every platform.
    /// </summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Every claim collected so far, oldest first.</summary>
    public static IReadOnlyList<PrizeClaim> Claims
    {
        get
        {
            Load();
            return ledger.claims;
        }
    }

    /// <summary>
    /// Trims both fields, adds the claim and writes the file. A claim missing either field is
    /// refused rather than stored blank, because a name with no number is not worth keeping and
    /// a number with no name cannot be handed a prize.
    /// </summary>
    /// <param name="stored">What actually went into the file, trimmed and stamped.</param>
    /// <returns>True if it was stored.</returns>
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

    /// <summary>
    /// Reads the file once per run. Everything after that is served from memory, so a claim
    /// costs one write rather than a read and a write.
    /// </summary>
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

        // Unreadable, and the next claim is about to write straight over it. Keep it anyway: a
        // file that will not parse is still every claim collected up to now, and someone can
        // pick it apart by hand. Losing a day of them to a stray byte is not on.
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
